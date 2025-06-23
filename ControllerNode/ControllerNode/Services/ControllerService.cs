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
using System.IO;
using System.Text.Json;
using ControllerNode.Models;
using System.Runtime.CompilerServices;


namespace ControllerNode.Services
{
    public class ControllerService
    {
        private readonly int blockSize;
        private Dictionary<string, List<BlockRef>> fileTable;  // Metadatos: archivos y ubicación de sus bloques
        private readonly IStorageNode[] nodes;
        private readonly long[] nextIndex; // contador por nodo
        private readonly Dictionary<string, int> fileSize = new();

        private readonly string _metaPath;

        // Devuelve true si ese documento está en la tabla
        public bool Exists(string fileName) =>
            fileTable.ContainsKey(fileName);

        // Devuelve el tamaño original en bytes (para Content-Length)
        public long GetFileSize(string fileName) =>
            fileSize.TryGetValue(fileName, out var size) ? size : 0;




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
            await SaveMetadataAsync();
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

        private async Task<byte[]> ReadOrReconstructBlockAsync(
        string fileName,
        int dataIndex,
        CancellationToken ct)
            {
            // Obtiene todas las referencias de bloques de ese archivo
            var blocks = fileTable[fileName];

            // Encuentra la referencia concreta del bloque de datos
            var dataRef = blocks.First(br => !br.IsParity && br.BlockNumber == dataIndex);

            byte[]? block = null;

            // 1) Intentar leerlo directo si el nodo está online
            if (await nodes[dataRef.NodeIndex].IsOnlineAsync(ct))
            {
                block = await nodes[dataRef.NodeIndex]
                    .ReadBlockAsync(dataRef.NodeBlockIndex, ct)
                    .ConfigureAwait(false);
            }

            // 2) Si no existe o el nodo está offline, reconstruir con XOR
            if (block == null)
            {
                int stripe = dataRef.StripeIndex;
                // Referencia al bloque de paridad de esa franja
                var parityRef = blocks.First(br => br.IsParity && br.StripeIndex == stripe);

                // Leer la paridad
                byte[] parity = await nodes[parityRef.NodeIndex]
                    .ReadBlockAsync(parityRef.NodeBlockIndex, ct)
                    .ConfigureAwait(false);

                // Obtener referencias de todos los datos de la franja
                var dataRefsInStripe = blocks
                    .Where(br => !br.IsParity && br.StripeIndex == stripe);

                // XOR para reconstruir
                var rebuilt = new byte[blockSize];
                for (int i = 0; i < blockSize; i++)
                {
                    byte x = parity[i];
                    foreach (var dRef in dataRefsInStripe)
                    {
                        if (dRef.BlockNumber == dataIndex) continue;
                        byte[]? dBlock = await nodes[dRef.NodeIndex]
                            .ReadBlockAsync(dRef.NodeBlockIndex, ct)
                            .ConfigureAwait(false);
                        if (dBlock != null) x ^= dBlock[i];
                    }
                    rebuilt[i] = x;
                }

                block = rebuilt;
            }

            return block!;
        }


        public async IAsyncEnumerable<byte[]> StreamDocumentAsync(
    string fileName,
    [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Tamaño real del documento (sin padding)
            long totalLength = fileSize[fileName];

            // Número de bloques necesarios (ceil)
            int totalBlocks = (int)((totalLength + blockSize - 1) / blockSize);

            for (int dataIndex = 0; dataIndex < totalBlocks; dataIndex++)
            {
                // 1) Leer o reconstruir bloque completo
                byte[] fullBlock = await ReadOrReconstructBlockAsync(fileName, dataIndex, ct)
                    .ConfigureAwait(false);

                // 2) Si es el último bloque, recortar al tamaño restante
                if (dataIndex == totalBlocks - 1)
                {
                    int remainder = (int)(totalLength - (long)dataIndex * blockSize);
                    if (remainder < fullBlock.Length)
                    {
                        var last = new byte[remainder];
                        Array.Copy(fullBlock, 0, last, 0, remainder);
                        yield return last;
                        continue;
                    }
                }

                // 3) En el resto de bloques, envío completo
                yield return fullBlock;
            }
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
            await SaveMetadataAsync(); 
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

        private async Task SaveMetadataAsync()
        {
            var doc = new MetadataDoc
            {
                FileTable = fileTable,
                FileSize = fileSize
            };
            await using var fs = File.Create(_metaPath);
            await JsonSerializer.SerializeAsync(fs, doc, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }


        private async Task LoadMetadataAsync()
        {
            // si no hay JSON, arrancamos limpios
            if (!File.Exists(_metaPath))
            {
                fileTable = new Dictionary<string, List<BlockRef>>();
                fileSize.Clear();
                return;
            }

            await using var fs = File.OpenRead(_metaPath);
            var doc = await JsonSerializer.DeserializeAsync<MetadataDoc>(fs, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true
            }) ?? new MetadataDoc();

            // reasignamos fileTable (no es readonly)
            fileTable = doc.FileTable ?? new();

            // en lugar de fileSize = doc.FileSize, hacemos:
            fileSize.Clear();
            if (doc.FileSize != null)
            {
                foreach (var kv in doc.FileSize)
                {
                    fileSize[kv.Key] = kv.Value;
                }
            }

            // recalculamos nextIndex igual que antes...
            for (int i = 0; i < nodes.Length; i++)
            {
                long maxPos = fileTable
                    .SelectMany(kv => kv.Value)
                    .Where(br => br.NodeIndex == i)
                    .Select(br => (long)br.NodeBlockIndex)
                    .DefaultIfEmpty(-1)
                    .Max();

                nextIndex[i] = maxPos + 1;
            }
        }





        public ControllerService(IStorageNode[] storageNodes, int blockSizeBytes)
        {
            nodes = storageNodes;
            blockSize = blockSizeBytes;
            fileTable = new();
            nextIndex = new long[nodes.Length];
            _metaPath = Path.Combine(AppContext.BaseDirectory, "filetable.json");
            if (File.Exists(_metaPath))
                LoadMetadataAsync().Wait();   // carga fileTable y fileSize antes de cualquier operación


        }
    }
}
