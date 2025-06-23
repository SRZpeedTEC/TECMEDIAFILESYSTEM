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
        public bool Exists(string fileName) 
        {
            return fileTable.ContainsKey(fileName); 
        }

        // Devuelve el tamaño original en bytes (para Content-Length)
        public long GetFileSize(string fileName)
        {
            return fileSize.TryGetValue(fileName, out var size) ? size : 0;
        }

        // Agrega un documento (archivo) al sistema distribuido
        public async Task AddDocumentAsync(string fileName, byte[] contentBytes, CancellationToken ct = default)
        {
            if (Exists(fileName))
            {
                Console.WriteLine($"[AddDocument] El archivo '{fileName}' ya existe.");
                return;
            }

            int totalBytes = contentBytes.Length;  
            fileSize[fileName] = totalBytes; // se le da tamano al archivo         
            int totalDataBlocks = (totalBytes + blockSize - 1) / blockSize;
            int paddingBytes = totalDataBlocks * blockSize - totalBytes;    // Bytes de relleno (padding) para completar el último bloque

            if (paddingBytes > 0)
            {
                Array.Resize(ref contentBytes, totalBytes + paddingBytes);  // Agrega padding al final del contenido
                totalBytes = contentBytes.Length;
            }

            List<BlockRef> blockRefs = new List<BlockRef>();
            int blocksPerStripe = nodes.Length - 1; // número de bloques de datos por franja (stripe), excluyendo el bloque de paridad
            int totalStripes = (totalDataBlocks + blocksPerStripe - 1) / blocksPerStripe;
            int dataBlockIndex = 0;

            for (int stripe = 0; stripe < totalStripes; stripe++)
            {

                int parityNode = (nodes.Length - 1) - (stripe % nodes.Length);  // nodo que almacenará el bloque de paridad
                List<byte[]> stripeDataBlocks = new List<byte[]>();
                var tasks = new List<Task>();


                for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++) 
                {
                if (nodeIndex == parityNode) continue;

                    if (dataBlockIndex >= totalDataBlocks) break;

                    byte[] blockData = new byte[blockSize];
                    Array.Copy(contentBytes, dataBlockIndex * blockSize, blockData, 0, blockSize); 

                    long blockPos = nextIndex[nodeIndex]++;      // reservar índice y luego ++
                    tasks.Add(nodes[nodeIndex].WriteBlockAsync(blockPos, blockData, ct));   // escribir bloque en el nodo

                    blockRefs.Add(new BlockRef(fileName, dataBlockIndex, stripe, false, nodeIndex, (int)blockPos));  // agregar referencia de bloque a la lista

                    stripeDataBlocks.Add(blockData);
                    dataBlockIndex++;
                }

                await Task.WhenAll(tasks);

                byte[] parityBlock = new byte[blockSize];

                for (int i = 0; i < blockSize; i++)
                {
                    byte xor = 0;

                    foreach (var block in stripeDataBlocks)
                    {
                        xor ^= block[i];
                        parityBlock[i] = xor;
                    }
                }

                long parityPos = nextIndex[parityNode];
                await nodes[parityNode].WriteBlockAsync(parityPos, parityBlock, ct);    
                nextIndex[parityNode]++;

                blockRefs.Add(new BlockRef(fileName, -1, stripe, true, parityNode, (int)parityPos));    
            }

            fileTable[fileName] = blockRefs;
            await SaveMetadataAsync();
            Console.WriteLine($"[AddDocument] Archivo '{fileName}' agregado con {totalDataBlocks} bloques de datos en {totalStripes} franjas.");

            
        }

        public async Task<byte[]?> GetDocumentAsync(string fileName, CancellationToken ct = default)
        {
            // Verificar si el archivo existe en la tabla de archivos
            if (!Exists(fileName))
            {
                Console.WriteLine($"[GetDocument] Archivo '{fileName}' no existe.");
                return null;
            }

            // Comprobar qué nodos de almacenamiento están en línea
            var tasks = nodes.Select(node => node.IsOnlineAsync(ct)).ToArray();
            await Task.WhenAll(tasks);
            var offline = tasks.Select(task => !task.Result).ToArray();  // Array de nodos fuera de línea

            // Obtener todas las referencias de bloques del archivo y calcular el total de bloques de datos
            var blocks = fileTable[fileName];
            int totalDataBlocks = 0;

            foreach (var blockRef in blocks)    
            {
                if (!blockRef.IsParity)  // C  
                {
                    totalDataBlocks = blockRef.BlockNumber + 1;  // C
                }                   
            }

            // Buffer para ensamblar el archivo completo
            byte[] resultBytes = new byte[totalDataBlocks * blockSize];

            // Agrupar referencias de bloques por franja (stripe) para procesar cada franja por separado
            var stripes = blocks.GroupBy(blockRef => blockRef.StripeIndex); 
            foreach (var stripeBlockGroup in stripes.OrderBy(group => group.Key))
            {
                int stripe = stripeBlockGroup.Key;

                // Referencia al bloque de paridad de esta franja y lista de bloques de datos
                BlockRef parityRef = stripeBlockGroup.First(blockRef => blockRef.IsParity); // Separar bloque de paridad
                var dataRefs = stripeBlockGroup.Where(blockRef => !blockRef.IsParity).ToList(); // Enlistar bloque de datos

                // Preparar diccionario para almacenar los datos de bloques de esta franja, clave: número de bloque de datos
                Dictionary<int, byte[]> stripeDataBytes = new();

                // Determinar si hay bloques de datos ausentes debido a nodos fuera de línea
                BlockRef? missingDataRef = null;
                int missingCount = 0;

                foreach (var dataRef in dataRefs)
                {
                    if (offline[dataRef.NodeIndex])
                    {
                        // Este bloque de datos no está disponible
                        missingCount++;
                        if (missingDataRef == null)
                        {
                            missingDataRef = dataRef;
                        }
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
                foreach (var dataRef in dataRefs)
                {
                    if (!offline[dataRef.NodeIndex])
                    {
                        // Iniciar lectura asíncrona del bloque de datos desde su nodo
                        readTasks.Add((dataRef, nodes[dataRef.NodeIndex].ReadBlockAsync(dataRef.NodeBlockIndex, ct)));
                    }
                }
                await Task.WhenAll(readTasks.Select(task => task.Task));

                // Procesar resultados de las lecturas de la franja
                foreach (var (dataRef, task) in readTasks)
                {
                    byte[]? data = task.Result;
                    if (data == null)
                    {
                        // Si un bloque no se pudo leer, considerarlo como bloque faltante
                        missingCount++;
                        if (missingDataRef == null)
                        {
                            missingDataRef = dataRef;
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
                        stripeDataBytes[dataRef.BlockNumber] = data;
                    }
                }

                // Si un bloque de datos falta, intentar reconstruirlo usando XOR
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
                foreach (var entry in stripeDataBytes)
                {
                    int blockNumber = entry.Key;
                    byte[] blockData = entry.Value;
                    Array.Copy(blockData, 0, resultBytes, blockNumber * blockSize, blockSize);
                }
            }

            // Recortar el buffer al tamaño real del archivo para eliminar bytes de relleno
            if (fileSize.TryGetValue(fileName, out int realSize))
            {
                Array.Resize(ref resultBytes, realSize);
            }

            // Calcular y mostrar el hash SHA1 del archivo ensamblado para verificar integridad
            Console.WriteLine($"[GetDocument] SHA1={Convert.ToHexString(SHA1.HashData(resultBytes))}");

            return resultBytes;
        }


        // Elimina un documento del sistema distribuido
        public async Task RemoveDocumentAsync(string fileName, CancellationToken ct = default)
        {
            if (!fileTable.ContainsKey(fileName))
            {
                Console.WriteLine($"[RemoveDocument] Archivo '{fileName}' no encontrado.");
                return;
            }

            var blocks = fileTable[fileName];

            // Comprobar qué nodos de almacenamiento están en línea
            foreach (var blockRef in blocks)
            {
                try
                {
                   
                    await nodes[blockRef.NodeIndex].WriteBlockAsync(blockRef.NodeBlockIndex, new byte[blockSize], ct);

                }
                catch
                {
                    Console.WriteLine($"[RemoveDocument] No se pudo borrar bloque {blockRef.NodeBlockIndex} en nodo {blockRef.NodeIndex}");
                }
            }

            // Eliminar las referencias del archivo de la tabla
            fileTable.Remove(fileName);
            fileSize.Remove(fileName);
            await SaveMetadataAsync(); 
            Console.WriteLine($"[RemoveDocument] Archivo '{fileName}' eliminado del sistema.");
        }

        // Devuelve todos los nodos de almacenamiento
        public IStorageNode[] GetStorageNodes() 
        {
            return nodes;
        }

        // Lista los nombres de los documentos almacenados filtrados
        public IEnumerable<string> ListDocuments(string? q = null) => q is null ? fileTable.Keys : fileTable.Keys.Where(file => file.Contains(q, StringComparison.OrdinalIgnoreCase));
        
        // Devuelve el estado de los nodos de almacenamiento
        public IEnumerable<object> GetRaidStatus() => nodes.Select((node, i) => new { id = i, online = node.IsOnlineAsync(CancellationToken.None).Result, nextIndex = nextIndex[i] });

        // Guarda la metadata en un archivo JSON
        private async Task SaveMetadataAsync()
        {
            var doc = new MetadataDoc
            {
                FileTable = fileTable,
                FileSize = fileSize
            };
            await using var fileSystem = File.Create(_metaPath);
            await JsonSerializer.SerializeAsync(fileSystem, doc, new JsonSerializerOptions
            {
                WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        private async Task LoadMetadataAsync()  // Carga la metadata desde el archivo JSON
        {
            
            if (!File.Exists(_metaPath))
            {
                fileTable = new Dictionary<string, List<BlockRef>>();   // inicializar fileTable si no existe
                fileSize.Clear();
                return;
            }

            await using var fileSystem = File.OpenRead(_metaPath);
            var doc = await JsonSerializer.DeserializeAsync<MetadataDoc>(fileSystem, new JsonSerializerOptions      
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, AllowTrailingCommas = true }) ?? new MetadataDoc(); // deserializar metadata


            fileTable = doc.FileTable ?? new();

            
            fileSize.Clear();
            if (doc.FileSize != null)
            {
                foreach (var entry in doc.FileSize)
                {
                    fileSize[entry.Key] = entry.Value;
                }
            }
          
            for (int i = 0; i < nodes.Length; i++)  
            {
                
                long maxPos = fileTable.SelectMany(entry => entry.Value).Where(blockRef => blockRef.NodeIndex == i).Select(blockRef => (long)blockRef.NodeBlockIndex).DefaultIfEmpty(-1).Max();   
            }
        }

        // Constructor que recibe los nodos de almacenamiento y el tamaño del bloque
        public ControllerService(IStorageNode[] storageNodes, int blockSizeBytes) 
        {
            nodes = storageNodes;
            blockSize = blockSizeBytes;
            fileTable = new();
            nextIndex = new long[nodes.Length];
            _metaPath = Path.Combine(AppContext.BaseDirectory, "filetable.json");
            if (File.Exists(_metaPath))
            {
                LoadMetadataAsync().Wait();   // carga fileTable y fileSize antes de cualquier operación
            }
        }
    }
}
