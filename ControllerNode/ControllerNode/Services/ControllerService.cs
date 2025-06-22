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
            if (!fileTable.ContainsKey(fileName))
            {
                Console.WriteLine($"[GetDocument] Archivo '{fileName}' no existe.");
                return null;
            }

            var blocks = fileTable[fileName];
            int totalDataBlocks = 0;
            foreach (var br in blocks)
            {
                if (!br.IsParity && br.BlockNumber >= 0)
                    totalDataBlocks = Math.Max(totalDataBlocks, br.BlockNumber + 1);
            }

            byte[] resultBytes = new byte[totalDataBlocks * blockSize];

            for (int dataIndex = 0; dataIndex < totalDataBlocks; dataIndex++)
            {
                var dataRef = blocks.Find(br => !br.IsParity && br.BlockNumber == dataIndex);
                if (dataRef == null) continue;

                byte[]? dataBlock = null;
                try
                {
                    dataBlock = await nodes[dataRef.NodeIndex].ReadBlockAsync(dataRef.NodeBlockIndex, ct);
                }
                catch { }

                if (dataBlock == null)
                {
                    int stripe = dataRef.StripeIndex;
                    var parityRef = blocks.Find(br => br.IsParity && br.StripeIndex == stripe);
                    var dataRefsInStripe = blocks.FindAll(br => !br.IsParity && br.StripeIndex == stripe);

                    byte[]? parityBlock = await nodes[parityRef.NodeIndex].ReadBlockAsync(parityRef.NodeBlockIndex, ct);
                    if (parityBlock == null)
                    {
                        Console.WriteLine($"[GetDocument] No se puede reconstruir el bloque {dataIndex} (franja {stripe}).");
                        return null;
                    }

                    byte[] reconstructed = new byte[blockSize];
                    for (int i = 0; i < blockSize; i++)
                    {
                        byte xor = parityBlock[i];
                        foreach (var dRef in dataRefsInStripe)
                        {
                            if (dRef.BlockNumber == dataIndex) continue;

                            byte[]? dBlock = await nodes[dRef.NodeIndex].ReadBlockAsync(dRef.NodeBlockIndex, ct);
                            if (dBlock != null)
                                xor ^= dBlock[i];
                        }
                        reconstructed[i] = xor;
                    }

                    dataBlock = reconstructed;
                    Console.WriteLine($"[GetDocument] * Reconstruido bloque {dataIndex} (franja {stripe}) usando XOR.");
                }

                Array.Copy(dataBlock, 0, resultBytes, dataIndex * blockSize, blockSize);
            }

            if (fileSize.TryGetValue(fileName, out int real))
            {
                Array.Resize(ref resultBytes, real);
            }

            Console.WriteLine(
            $"[GetDocument] SHA1={Convert.ToHexString(SHA1.HashData(resultBytes))}");

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
