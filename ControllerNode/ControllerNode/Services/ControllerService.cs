using ControllerNode.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ControllerNode.Interfaces;
using ControllerNode.Models;
using System.Security.Cryptography;

namespace ControllerNode.Services
{
    public class ControllerService
    {
        private readonly int blockSize;
        private Dictionary<string, List<BlockRef>> fileTable;  // Metadatos: archivos y ubicación de sus bloques
        private readonly IStorageNode[] nodes;
        private readonly long[] nextIndex; // contador por nodo
        private readonly Dictionary<string, int> fileSize = new(); 



        // Agrega un documento (archivo) al sistema distribuido
        public async Task AddDocumentAsync(string fileName, byte[] contentBytes, CancellationToken ct = default)
        {
            if (fileTable.ContainsKey(fileName))
            {
                Console.WriteLine($"[AddDocument] El archivo '{fileName}' ya existe.");
                return;
            }

            int totalBytes = contentBytes.Length;  
            fileSize[fileName] = totalBytes;         
            int totalDataBlocks = (totalBytes + blockSize - 1) / blockSize;
            int paddingBytes = totalDataBlocks * blockSize - totalBytes;

            if (paddingBytes > 0)
            {
                Array.Resize(ref contentBytes, totalBytes + paddingBytes);
                totalBytes = contentBytes.Length;
            }

            List<BlockRef> blockRefs = new List<BlockRef>();
            int blocksPerStripe = nodes.Length - 1;
            int totalStripes = (totalDataBlocks + blocksPerStripe - 1) / blocksPerStripe;
            int dataBlockIndex = 0;

            for (int stripe = 0; stripe < totalStripes; stripe++)
            {
                int parityNode = (nodes.Length - 1) - (stripe % nodes.Length);
                List<byte[]> stripeDataBlocks = new List<byte[]>();

                var tasks = new List<Task>();

                foreach (int nodeIndex in Enumerable.Range(0, nodes.Length))
                {
                    if (nodeIndex == parityNode) continue;
                    if (dataBlockIndex >= totalDataBlocks) break;

                    byte[] blockData = new byte[blockSize];
                    Array.Copy(contentBytes, dataBlockIndex * blockSize, blockData, 0, blockSize);

                    long blockPos = nextIndex[nodeIndex]++;      // reservar índice y luego ++
                    tasks.Add(nodes[nodeIndex].WriteBlockAsync(blockPos, blockData, ct));

                    blockRefs.Add(new BlockRef(fileName, dataBlockIndex, stripe,
                                               false, nodeIndex, (int)blockPos));
                    stripeDataBlocks.Add(blockData);
                    dataBlockIndex++;
                }

                await Task.WhenAll(tasks);

                byte[] parityBlock = new byte[blockSize];
                for (int i = 0; i < blockSize; i++)
                {
                    byte xor = 0;
                    foreach (var block in stripeDataBlocks)
                        xor ^= block[i];
                    parityBlock[i] = xor;
                }

                long parityPos = nextIndex[parityNode];
                await nodes[parityNode].WriteBlockAsync(parityPos, parityBlock, ct);
                nextIndex[parityNode]++;

                blockRefs.Add(new BlockRef(fileName, -1, stripe, true, parityNode, (int)parityPos));
            }

            fileTable[fileName] = blockRefs;
            Console.WriteLine($"[AddDocument] Archivo '{fileName}' agregado con {totalDataBlocks} bloques de datos en {totalStripes} franjas.");

            Console.WriteLine(
            $"[AddDocument] SHA1={Convert.ToHexString(SHA1.HashData(contentBytes.AsSpan(0, totalBytes)))}");
        }

        public async Task<byte[]?> GetDocumentAsync(string fileName, CancellationToken ct = default)
        {
            // Verificar si el archivo existe en la tabla de archivos
            if (!fileTable.ContainsKey(fileName))
            {
                Console.WriteLine($"[GetDocument] Archivo '{fileName}' no existe.");
                return null;
            }

            // Comprobar qué nodos de almacenamiento están en línea (disponibles)
            var tasks = nodes.Select(n => n.IsOnlineAsync(ct)).ToArray();
            await Task.WhenAll(tasks);
            var offline = tasks.Select(t => !t.Result).ToArray();  // Array de nodos fuera de línea

            // Obtener todas las referencias de bloques del archivo y calcular el total de bloques de datos
            var blocks = fileTable[fileName];
            int totalDataBlocks = 0;
            foreach (var br in blocks)
            {
                if (!br.IsParity && br.BlockNumber >= 0)
                    totalDataBlocks = Math.Max(totalDataBlocks, br.BlockNumber + 1);
            }

            // Buffer para ensamblar el archivo completo (incluyendo padding, se recorta después)
            byte[] resultBytes = new byte[totalDataBlocks * blockSize];

            // Agrupar referencias de bloques por franja (stripe) para procesar cada franja por separado
            var stripes = blocks.GroupBy(br => br.StripeIndex);
            foreach (var stripeGroup in stripes.OrderBy(g => g.Key))
            {
                int stripe = stripeGroup.Key;
                // Referencia al bloque de paridad de esta franja y lista de bloques de datos
                BlockRef parityRef = stripeGroup.First(br => br.IsParity);
                var dataRefs = stripeGroup.Where(br => !br.IsParity).ToList();

                // Preparar diccionario para almacenar los datos de bloques de esta franja (clave: número de bloque de datos)
                Dictionary<int, byte[]> stripeDataBytes = new();

                // Determinar si hay bloques de datos ausentes debido a nodos fuera de línea
                BlockRef? missingDataRef = null;
                int missingCount = 0;
                foreach (var dr in dataRefs)
                {
                    if (offline[dr.NodeIndex])
                    {
                        // Este bloque de datos no está disponible (nodo fuera de línea)
                        missingCount++;
                        if (missingDataRef == null)
                            missingDataRef = dr;
                    }
                }
                // Si más de un bloque de datos falta en la misma franja, no se puede reconstruir (RAID5 tolera solo 1 fallo)
                if (missingCount > 1)
                {
                    Console.WriteLine($"[GetDocument] No se puede reconstruir el bloque {missingDataRef?.BlockNumber} (franja {stripe}).");
                    return null;
                }

                // Leer en paralelo todos los bloques de datos disponibles (nodos en línea) de esta franja
                var readTasks = new List<(BlockRef Ref, Task<byte[]?> Task)>();
                foreach (var dr in dataRefs)
                {
                    if (!offline[dr.NodeIndex])
                    {
                        // Iniciar lectura asíncrona del bloque de datos desde su nodo
                        readTasks.Add((dr, nodes[dr.NodeIndex].ReadBlockAsync(dr.NodeBlockIndex, ct)));
                    }
                }
                await Task.WhenAll(readTasks.Select(t => t.Task));

                // Procesar resultados de las lecturas de la franja
                foreach (var (dr, task) in readTasks)
                {
                    byte[]? data = task.Result;
                    if (data == null)
                    {
                        // Si un bloque no se pudo leer (null), considerarlo como bloque faltante
                        missingCount++;
                        if (missingDataRef == null)
                        {
                            missingDataRef = dr;
                        }
                        else
                        {
                            // Si ya había un bloque faltante, este sería el segundo (no recuperable)
                            Console.WriteLine($"[GetDocument] No se puede reconstruir el bloque {missingDataRef.BlockNumber} (franja {stripe}).");
                            return null;
                        }
                    }
                    else
                    {
                        // Almacenar datos leídos del bloque de datos disponible
                        stripeDataBytes[dr.BlockNumber] = data;
                    }
                }

                // Si un bloque de datos falta, intentar reconstruirlo usando XOR (RAID5)
                if (missingDataRef != null)
                {
                    // Verificar que el bloque de paridad esté disponible para la reconstrucción
                    if (offline[parityRef.NodeIndex])
                    {
                        Console.WriteLine($"[GetDocument] No se puede reconstruir el bloque {missingDataRef.BlockNumber} (franja {stripe}).");
                        return null;
                    }
                    // Leer el bloque de paridad desde su nodo de almacenamiento
                    byte[]? parityBlock = await nodes[parityRef.NodeIndex].ReadBlockAsync(parityRef.NodeBlockIndex, ct);
                    if (parityBlock == null)
                    {
                        Console.WriteLine($"[GetDocument] No se puede reconstruir el bloque {missingDataRef.BlockNumber} (franja {stripe}).");
                        return null;
                    }
                    // Reconstruir el bloque faltante aplicando XOR entre el bloque de paridad y los bloques de datos disponibles
                    byte[] reconstructedBlock = new byte[blockSize];
                    for (int i = 0; i < blockSize; i++)
                    {
                        byte xor = parityBlock[i];
                        foreach (var dataBytes in stripeDataBytes.Values)
                        {
                            xor ^= dataBytes[i];  // XOR de todos los bytes i-ésimos de los bloques disponibles
                        }
                        reconstructedBlock[i] = xor;
                    }
                    // Agregar el bloque reconstruido al diccionario como si se hubiera leído
                    stripeDataBytes[missingDataRef.BlockNumber] = reconstructedBlock;
                    Console.WriteLine($"[GetDocument] * Reconstruido bloque {missingDataRef.BlockNumber} (franja {stripe}) usando XOR.");
                }

                // Copiar todos los bloques de datos de esta franja al buffer de resultado en su posición correspondiente
                foreach (var kv in stripeDataBytes)
                {
                    int blockNumber = kv.Key;
                    byte[] blockData = kv.Value;
                    Array.Copy(blockData, 0, resultBytes, blockNumber * blockSize, blockSize);
                }
            }

            // Recortar el buffer al tamaño real del archivo para eliminar bytes de relleno (padding)
            if (fileSize.TryGetValue(fileName, out int realSize))
            {
                Array.Resize(ref resultBytes, realSize);
            }

            // Calcular y mostrar el hash SHA1 del archivo ensamblado para verificar integridad
            Console.WriteLine($"[GetDocument] SHA1={Convert.ToHexString(SHA1.HashData(resultBytes))}");

            return resultBytes;
        }


        public async Task RemoveDocumentAsync(string fileName, CancellationToken ct = default)
        {
            if (!fileTable.ContainsKey(fileName))
            {
                Console.WriteLine($"[RemoveDocument] Archivo '{fileName}' no encontrado.");
                return;
            }

            var blocks = fileTable[fileName];

            foreach (var br in blocks)
            {
                try
                {
                   
                    await nodes[br.NodeIndex].WriteBlockAsync(br.NodeBlockIndex, new byte[blockSize], ct);

                }
                catch
                {
                    Console.WriteLine($"[RemoveDocument] No se pudo borrar bloque {br.NodeBlockIndex} en nodo {br.NodeIndex}");
                }
            }




            fileTable.Remove(fileName);
            fileSize.Remove(fileName);
            Console.WriteLine($"[RemoveDocument] Archivo '{fileName}' eliminado del sistema.");
        }

        public IStorageNode[] GetStorageNodes()
        {
            return nodes;
        }

        public IEnumerable<string> ListDocuments(string? q = null) =>
        q is null ? fileTable.Keys
                  : fileTable.Keys.Where(f => f.Contains(q, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<object> GetRaidStatus() =>
            nodes.Select((n, i) => new {
                id = i,
                online = n.IsOnlineAsync(CancellationToken.None).Result,
                nextIndex = nextIndex[i]
            });




        public ControllerService(IStorageNode[] storageNodes, int blockSizeBytes)
        {
            nodes = storageNodes;
            blockSize = blockSizeBytes;
            fileTable = new();
            nextIndex = new long[nodes.Length];

        }
    }
}
