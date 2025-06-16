using System;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

public class StorageNodeHTTP
{
    public int Id;
    public string BaseUrl;           // Base URL of the DiskNode (e.g. "http://localhost:5001")
    public bool IsOnline = true;     // Simulate node availability
    public long NextBlockIndex = 0;  // Next available block index for this node

    public StorageNodeHTTP(int id, string baseUrl)
    {
        Id = id;
        BaseUrl = baseUrl;
    }
}

public class BlockRefHTTP
{
    public string FileName;
    public int BlockNumber;     // Data block index in the file (0,1,2,... or -1 for parity)
    public int StripeIndex;     // Stripe index within the file
    public bool IsParity;       // True if this is a parity block
    public int NodeIndex;       // Which node stores this block
    public long NodeBlockIndex; // Index of the block on the storage node (global per node)

    public BlockRefHTTP(string fileName, int blockNum, int stripe, bool isParity,
                    int nodeIdx, long nodeBlockIdx)
    {
        FileName = fileName;
        BlockNumber = blockNum;
        StripeIndex = stripe;
        IsParity = isParity;
        NodeIndex = nodeIdx;
        NodeBlockIndex = nodeBlockIdx;
    }
}

public class ControllerNodeHTTPS
{
    private int blockSize = 4;  // Block size in bytes (must match DiskNode's block size)
    private StorageNodeHTTP[] nodes;
    private Dictionary<string, List<BlockRefHTTP>> fileTable;
    private static readonly HttpClient httpClient = new HttpClient();

    public ControllerNodeHTTPS()
    {
        // Configure 4 disk node URLs (these can be adjusted or made configurable)
        string[] baseUrls = {
            "http://localhost:5001",
            "http://localhost:5002",
            "http://localhost:5003",
            "http://localhost:5004"
        };
        int numNodes = baseUrls.Length;
        nodes = new StorageNodeHTTP[numNodes];
        for (int i = 0; i < numNodes; i++)
        {
            nodes[i] = new StorageNodeHTTP(i, baseUrls[i]);
        }
        fileTable = new Dictionary<string, List<BlockRefHTTP>>();
    }

    // Add a document (file) into the distributed storage
    public void AddDocument(string fileName, string content)
    {
        if (fileTable.ContainsKey(fileName))
        {
            Console.WriteLine($"[AddDocument] El archivo '{fileName}' ya existe.");
            return;
        }
        // Convert content to bytes
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        int totalBytes = contentBytes.Length;
        // Calculate number of data blocks needed, add padding if needed
        int totalDataBlocks = (totalBytes + blockSize - 1) / blockSize;
        int paddingBytes = totalDataBlocks * blockSize - totalBytes;
        if (paddingBytes > 0)
        {
            Array.Resize(ref contentBytes, totalBytes + paddingBytes);
            totalBytes = contentBytes.Length;
        }
        List<BlockRefHTTP> BlockRefHTTPs = new List<BlockRefHTTP>();
        int blocksPerStripe = nodes.Length - 1;  // N-1 data blocks per stripe (one parity)
        int totalStripes = (totalDataBlocks + blocksPerStripe - 1) / blocksPerStripe;
        int dataBlockIndex = 0;
        for (int stripe = 0; stripe < totalStripes; stripe++)
        {
            // Determine which node will hold parity for this stripe (rotating parity)
            int parityNode = (nodes.Length - 1) - (stripe % nodes.Length);
            // Collect data blocks for this stripe to compute parity
            List<byte[]> stripeDataBlocks = new List<byte[]>();
            // Assign data blocks to each node except the parity node
            for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                if (nodeIndex == parityNode) continue;  // skip parity node for data
                if (dataBlockIndex < totalDataBlocks)
                {
                    // Prepare the data block bytes for this portion
                    byte[] blockData = new byte[blockSize];
                    Array.Copy(contentBytes, dataBlockIndex * blockSize, blockData, 0, blockSize);
                    // Send the block to the storage node via HTTP PUT
                    StorageNodeHTTP node = nodes[nodeIndex];
                    long blockIndex = node.NextBlockIndex;
                    string url = $"{node.BaseUrl}/blocks/{blockIndex}";
                    try
                    {
                        var response = httpClient.PutAsync(url, new ByteArrayContent(blockData))
                                                 .GetAwaiter().GetResult();
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"[AddDocument] Error: Node {nodeIndex} PUT block failed (HTTP {response.StatusCode}).");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AddDocument] Error: could not write to Node {nodeIndex} - {ex.Message}");
                        return;
                    }
                    // Update node's next index and record metadata
                    node.NextBlockIndex++;
                    BlockRefHTTP dataRef = new BlockRefHTTP(fileName, dataBlockIndex, stripe, false,
                                                    nodeIndex, blockIndex);
                    BlockRefHTTPs.Add(dataRef);
                    stripeDataBlocks.Add(blockData);
                    dataBlockIndex++;
                }
            }
            // Compute XOR parity over the stripe's data blocks
            byte[] parityBlock = new byte[blockSize];
            for (int i = 0; i < blockSize; i++)
            {
                byte xorSum = 0;
                foreach (byte[] dataBlock in stripeDataBlocks)
                {
                    xorSum ^= dataBlock[i];
                }
                parityBlock[i] = xorSum;
            }
            // Store parity block on the designated parity node via HTTP
            StorageNodeHTTP pNode = nodes[parityNode];
            long parityIndex = pNode.NextBlockIndex;
            string parityUrl = $"{pNode.BaseUrl}/blocks/{parityIndex}";
            try
            {
                var response = httpClient.PutAsync(parityUrl, new ByteArrayContent(parityBlock))
                                             .GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[AddDocument] Error: Node {parityNode} PUT parity failed (HTTP {response.StatusCode}).");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AddDocument] Error: could not write parity to Node {parityNode} - {ex.Message}");
                return;
            }
            pNode.NextBlockIndex++;
            BlockRefHTTP parityRef = new BlockRefHTTP(fileName, -1, stripe, true, parityNode, parityIndex);
            BlockRefHTTPs.Add(parityRef);
        }
        // Save file metadata
        fileTable[fileName] = BlockRefHTTPs;
        Console.WriteLine($"[AddDocument] Archivo '{fileName}' agregado con {totalDataBlocks} bloques de datos en {totalStripes} franjas (paridad RAID5).");
    }

    // Retrieve a document's content (uses XOR reconstruction if a node is offline)
    public string GetDocument(string fileName)
    {
        if (!fileTable.ContainsKey(fileName))
        {
            Console.WriteLine($"[GetDocument] Archivo '{fileName}' no existe.");
            return null;
        }
        List<BlockRefHTTP> blocks = fileTable[fileName];
        // Determine total number of original data blocks
        int totalDataBlocks = 0;
        foreach (BlockRefHTTP br in blocks)
        {
            if (!br.IsParity && br.BlockNumber >= 0)
            {
                totalDataBlocks = Math.Max(totalDataBlocks, br.BlockNumber + 1);
            }
        }
        byte[] resultBytes = new byte[totalDataBlocks * blockSize];
        // Retrieve each data block in order
        for (int dataIndex = 0; dataIndex < totalDataBlocks; dataIndex++)
        {
            // Find the BlockRefHTTP for this data block
            BlockRefHTTP dataRef = blocks.Find(br => !br.IsParity && br.BlockNumber == dataIndex);
            if (dataRef == null) continue;  // safety check
            StorageNodeHTTP node = nodes[dataRef.NodeIndex];
            byte[] dataBlock = null;
            if (node.IsOnline)
            {
                // Try to fetch the block from the node via HTTP GET
                string url = $"{node.BaseUrl}/blocks/{dataRef.NodeBlockIndex}";
                try
                {
                    dataBlock = httpClient.GetByteArrayAsync(url).GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                    // If the request fails (node down or block missing), treat as offline
                    dataBlock = null;
                }
            }
            if (dataBlock == null)
            {
                // Block not available (node offline or error) – reconstruct using parity
                int stripe = dataRef.StripeIndex;
                Console.WriteLine($"[GetDocument] * Nodo{dataRef.NodeIndex} offline o bloque #{dataIndex} no disponible. Reconstruyendo de franja {stripe}...");
                // Get all block references in this stripe (3 data + 1 parity)
                BlockRefHTTP parityRef = blocks.Find(br => br.IsParity && br.StripeIndex == stripe);
                List<BlockRefHTTP> dataRefsInStripe = blocks.FindAll(br => !br.IsParity && br.StripeIndex == stripe);
                // Fetch parity block from its node
                byte[] parityBlock;
                try
                {
                    parityBlock = httpClient.GetByteArrayAsync($"{nodes[parityRef.NodeIndex].BaseUrl}/blocks/{parityRef.NodeBlockIndex}")
                                             .GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                    Console.WriteLine($"[GetDocument] Error: Parity block missing for stripe {stripe}.");
                    return null;
                }
                // XOR all available blocks with parity to reconstruct missing block
                byte[] reconstructed = new byte[blockSize];
                for (int i = 0; i < blockSize; i++)
                {
                    byte xorSum = parityBlock[i];
                    foreach (BlockRefHTTP dRef in dataRefsInStripe)
                    {
                        if (dRef.BlockNumber == dataIndex) continue;  // skip the missing block itself
                        try
                        {
                            byte[] dBlock = httpClient.GetByteArrayAsync(
                                                $"{nodes[dRef.NodeIndex].BaseUrl}/blocks/{dRef.NodeBlockIndex}"
                                            ).GetAwaiter().GetResult();
                            xorSum ^= dBlock[i];
                        }
                        catch (Exception)
                        {
                            // If any other block is unexpectedly missing, reconstruction will fail
                            Console.WriteLine($"[GetDocument] Warning: couldn't read block {dRef.BlockNumber} from Node{dRef.NodeIndex}.");
                        }
                    }
                    reconstructed[i] = xorSum;
                }
                dataBlock = reconstructed;
                Console.WriteLine($"[GetDocument] * Bloque #{dataIndex} reconstruido via XOR (franja {stripe}).");
            }
            // Copy the retrieved or reconstructed block into the result buffer
            Array.Copy(dataBlock, 0, resultBytes, dataIndex * blockSize, blockSize);
        }
        // Convert byte array to string (assuming UTF8 text) and trim any padding nulls
        string resultContent = Encoding.UTF8.GetString(resultBytes).TrimEnd('\0');
        Console.WriteLine($"[GetDocument] Archivo '{fileName}' recuperado, tamaño {resultContent.Length} bytes.");
        return resultContent;
    }

    // Remove a document and its blocks from the system (logical removal)
    public void RemoveDocument(string fileName)
    {
        if (!fileTable.ContainsKey(fileName))
        {
            Console.WriteLine($"[RemoveDocument] Archivo '{fileName}' no encontrado.");
            return;
        }
        List<BlockRefHTTP> blocks = fileTable[fileName];
        // **Note**: We do not physically delete blocks on DiskNodes (no DELETE endpoint provided).
        // We simply remove the metadata so the file is no longer tracked in the system.
        fileTable.Remove(fileName);
        Console.WriteLine($"[RemoveDocument] Archivo '{fileName}' eliminado del sistema (metadata removida).");
    }

    // Simple test method to validate operations
    public void Test()
    {
        Console.WriteLine("=== Iniciando prueba del ControllerNode con nodos DiskNode reales ===");
        string file = "Documento1";
        string contenido = "Hola, este es un archivo de prueba para RAID5.";
        // Add document
        AddDocument(file, contenido);
        // Show internal distribution (metadata)
        if (fileTable.ContainsKey(file))
        {
            Console.WriteLine($"Distribución de '{file}':");
            foreach (BlockRefHTTP br in fileTable[file])
            {
                string tipo = br.IsParity ? "Paridad" : $"Datos#{br.BlockNumber}";
                Console.WriteLine($"  - {tipo}: Nodo{br.NodeIndex} -> Índice {br.NodeBlockIndex}");
            }
        }
        // Retrieve document (no failures)
        string contenidoRecuperado = GetDocument(file);
        Console.WriteLine($"Contenido recuperado: \"{contenidoRecuperado}\"");
        // Simulate a node failure (e.g., node 0 offline)
        nodes[0].IsOnline = false;
        Console.WriteLine(">> Nodo 0 simulado como FUERA DE LÍNEA.");
        // Retrieve document with a node offline (uses XOR reconstruction)
        string contenidoRecuperado2 = GetDocument(file);
        Console.WriteLine($"Contenido recuperado con nodo 0 off-line: \"{contenidoRecuperado2}\"");
        // Restore node 0
        nodes[0].IsOnline = true;
        // Remove document
        RemoveDocument(file);
        // Attempt to retrieve after removal
        string contenidoRecuperado3 = GetDocument(file);
        Console.WriteLine($"Archivo tras eliminar: {(contenidoRecuperado3 == null ? "No disponible" : contenidoRecuperado3)}");
        Console.WriteLine("=== Fin de la prueba ===");
    }
}
