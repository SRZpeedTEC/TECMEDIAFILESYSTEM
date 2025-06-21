using System;
using System.Text;
using System.Collections.Generic;
using ControllerNode.Models;

public class StorageNode
{
    public int Id;
    public bool IsOnline = true;               // Simula si el nodo está activo
    public List<byte[]> Blocks = new List<byte[]>();  // Almacén de bloques en este nodo

    public StorageNode(int id)
    {
        Id = id;
    }
}



public class ControllerNodeProject
{
    private int blockSize = 4;  // Tamaño de bloque en bytes para la simulación (ejemplo: 4 bytes)
    private StorageNode[] nodes;
    private Dictionary<string, List<BlockRef>> fileTable;  // Metadatos: archivos y ubicación de sus bloques

    public ControllerNodeProject(int numNodes = 4)
    {
        // Inicializa nodos simulados
        nodes = new StorageNode[numNodes];
        for (int i = 0; i < numNodes; i++)
        {
            nodes[i] = new StorageNode(i);
        }
        fileTable = new Dictionary<string, List<BlockRef>>();
    }

    // Agrega un documento (archivo) al sistema distribuido
    public void AddDocument(string fileName, string content)
    {
        if (fileTable.ContainsKey(fileName))
        {
            Console.WriteLine($"[AddDocument] El archivo '{fileName}' ya existe.");
            return;
        }

        // Convertir el contenido a bytes para almacenar
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        int totalBytes = contentBytes.Length;

        // Calcular número de bloques de datos necesarios, padding si no encaja exactamente
        int totalDataBlocks = (totalBytes + blockSize - 1) / blockSize; // ceil division
        int paddingBytes = totalDataBlocks * blockSize - totalBytes;
        if (paddingBytes > 0)
        {
            // Extender el arreglo con bytes 0 para completar el último bloque
            Array.Resize(ref contentBytes, totalBytes + paddingBytes);
            totalBytes = contentBytes.Length;
        }

        // Preparar lista de referencias de bloques para este archivo
        List<BlockRef> blockRefs = new List<BlockRef>();

        // Recorremos cada franja (stripe)
        int blocksPerStripe = nodes.Length - 1;  // N-1 bloques de datos por franja
        int totalStripes = (totalDataBlocks + blocksPerStripe - 1) / blocksPerStripe;
        int dataBlockIndex = 0;  // índice global de bloque de datos en el archivo

        for (int stripe = 0; stripe < totalStripes; stripe++)
        {
            // Determinar índice de nodo para paridad (rotación de paridad)
            int parityNode = (nodes.Length - 1) - (stripe % nodes.Length);
            // Lista temporal para almacenar los bloques de datos de esta franja (para calcular paridad)
            List<byte[]> stripeDataBlocks = new List<byte[]>();

            // Asignar bloques de datos a nodos (excepto nodo de paridad)
            for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                if (nodeIndex == parityNode) continue;  // Saltar nodo de paridad
                if (dataBlockIndex < totalDataBlocks)
                {
                    // Extraer porción de bytes para este bloque
                    byte[] blockData = new byte[blockSize];
                    Array.Copy(contentBytes, dataBlockIndex * blockSize, blockData, 0, blockSize);
                    // Almacenar el bloque en el nodo correspondiente
                    StorageNode node = nodes[nodeIndex];
                    node.Blocks.Add(blockData);
                    int nodeBlockPosition = node.Blocks.Count - 1;
                    // Registrar metadatos de este bloque
                    BlockRef bref = new BlockRef(fileName, dataBlockIndex, stripe, false, nodeIndex, nodeBlockPosition);
                    blockRefs.Add(bref);
                    stripeDataBlocks.Add(blockData);
                    // Avanzar al siguiente bloque de datos del archivo
                    dataBlockIndex++;
                }
            }

            // Calcular paridad XOR de los bloques de datos de esta franja
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
            // Almacenar bloque de paridad en el nodo designado
            StorageNode parityNodeObj = nodes[parityNode];
            parityNodeObj.Blocks.Add(parityBlock);
            int parityBlockPosition = parityNodeObj.Blocks.Count - 1;
            BlockRef parityRef = new BlockRef(fileName, -1, stripe, true, parityNode, parityBlockPosition);
            blockRefs.Add(parityRef);
        }

        // Guardar metadatos del archivo en la tabla
        fileTable[fileName] = blockRefs;
        Console.WriteLine($"[AddDocument] Archivo '{fileName}' agregado con {totalDataBlocks} bloques de datos " +
                          $"en {totalStripes} franjas (paridad distribuida).");
    }

    // Elimina un documento y sus bloques de todos los nodos
    public void RemoveDocument(string fileName)
    {
        if (!fileTable.ContainsKey(fileName))
        {
            Console.WriteLine($"[RemoveDocument] Archivo '{fileName}' no encontrado.");
            return;
        }
        List<BlockRef> blocks = fileTable[fileName];
        // Eliminar bloques de cada nodo (simulado)
        foreach (BlockRef br in blocks)
        {
            StorageNode node = nodes[br.NodeIndex];
            // Marcar como eliminado (aquí ponemos null para mantener índice, podría hacerse limpieza)
            node.Blocks[br.NodeBlockIndex] = null;
        }
        // Remover entrada del archivo de la tabla
        fileTable.Remove(fileName);
        Console.WriteLine($"[RemoveDocument] Archivo '{fileName}' eliminado del sistema.");
    }

    // Reconstruye el contenido de un archivo (usando paridad si falta un nodo)
    public string GetDocument(string fileName)
    {
        if (!fileTable.ContainsKey(fileName))
        {
            Console.WriteLine($"[GetDocument] Archivo '{fileName}' no existe.");
            return null;
        }
        List<BlockRef> blocks = fileTable[fileName];

        // Determinar número total de bloques de datos originales
        // (Contar bloques que no son paridad, cada uno tiene BlockNumber >= 0)
        int totalDataBlocks = 0;
        foreach (BlockRef br in blocks)
        {
            if (!br.IsParity && br.BlockNumber >= 0)
            {
                totalDataBlocks = Math.Max(totalDataBlocks, br.BlockNumber + 1);
            }
        }

        // Buffer para reconstruir bytes del archivo completo
        byte[] resultBytes = new byte[totalDataBlocks * blockSize];

        // Reconstruir bloque por bloque en orden
        for (int dataIndex = 0; dataIndex < totalDataBlocks; dataIndex++)
        {
            // Encontrar la referencia del bloque de datos con este índice
            BlockRef dataRef = blocks.Find(br => !br.IsParity && br.BlockNumber == dataIndex);
            if (dataRef == null) continue; // seguridad: debería existir
            StorageNode node = nodes[dataRef.NodeIndex];
            byte[] dataBlock;
            if (node.IsOnline && node.Blocks[dataRef.NodeBlockIndex] != null)
            {
                // Leer bloque directamente si el nodo está disponible
                dataBlock = node.Blocks[dataRef.NodeBlockIndex];
            }
            else
            {
                // El bloque no está disponible (nodo caído), reconstruir usando XOR
                int stripe = dataRef.StripeIndex;
                // Obtener todos los bloques de la franja (3 datos + 1 paridad)
                // Filtrar referencias de esta franja
                BlockRef parityRef = blocks.Find(br => br.IsParity && br.StripeIndex == stripe);
                List<BlockRef> dataRefsInStripe = blocks.FindAll(br => !br.IsParity && br.StripeIndex == stripe);
                // XOR de bloques disponibles con paridad para obtener el bloque faltante
                byte[] parityBlock = nodes[parityRef.NodeIndex].Blocks[parityRef.NodeBlockIndex];
                byte[] reconstructed = new byte[blockSize];
                for (int i = 0; i < blockSize; i++)
                {
                    byte xorSum = 0;
                    // Comenzar con paridad
                    xorSum ^= parityBlock[i];
                    // XOR con todos los demás bloques de datos disponibles de la franja
                    foreach (BlockRef dRef in dataRefsInStripe)
                    {
                        StorageNode dNode = nodes[dRef.NodeIndex];
                        byte[] dBlock = dNode.Blocks[dRef.NodeBlockIndex];
                        if (dRef.BlockNumber == dataIndex)
                        {
                            // Saltar el bloque que estamos reconstruyendo (no disponible)
                            continue;
                        }
                        xorSum ^= dBlock[i];
                    }
                    reconstructed[i] = xorSum;
                }
                dataBlock = reconstructed;
                Console.WriteLine($"[GetDocument] * Reconstruido bloque {dataIndex} (franja {stripe}) " +
                                  $"usando XOR de paridad y otros datos.");
            }
            // Copiar el bloque (reconstruido o leído) al buffer de resultado
            Array.Copy(dataBlock, 0, resultBytes, dataIndex * blockSize, blockSize);
        }

        // Convertir bytes a string (asumiendo UTF8) y recortar padding nulo si existe
        string resultContent = Encoding.UTF8.GetString(resultBytes);
        resultContent = resultContent.TrimEnd('\0');  // quitar caracteres nulos de padding
        Console.WriteLine($"[GetDocument] Archivo '{fileName}' recuperado, tamaño {resultContent.Length} bytes.");
        return resultContent;
    }

    // Método de prueba simple para validar las operaciones
    public void Test()
    {
        Console.WriteLine("=== Iniciando prueba del ControllerNode con RAID5 simulado ===");
        string file = "Documento1";
        string contenido = "Hola, este es un archivo de prueba para RAID5.";
        // Agregar documento
        AddDocument(file, contenido);
        // Mostrar distribución interna (metadatos)
        if (fileTable.ContainsKey(file))
        {
            Console.WriteLine($"Distribución de '{file}':");
            foreach (BlockRef br in fileTable[file])
            {
                string tipo = br.IsParity ? "Paridad" : $"Datos#{br.BlockNumber}";
                Console.WriteLine($"  - {tipo}: Nodo{br.NodeIndex} -> Posición {br.NodeBlockIndex}");
            }
        }
        // Recuperar documento (sin fallos)
        string contenidoRecuperado = GetDocument(file);
        Console.WriteLine($"Contenido recuperado: \"{contenidoRecuperado}\"");
        // Simular caída de un nodo (ej: nodo 0 fuera de línea)
        nodes[0].IsOnline = false;
        Console.WriteLine(">> Nodo 0 simulado como FUERA DE LÍNEA.");
        // Recuperar documento con un nodo caído (usará reconstrucción XOR)
        string contenidoRecuperado2 = GetDocument(file);
        Console.WriteLine($"Contenido recuperado con nodo0 off-line: \"{contenidoRecuperado2}\"");
        // Restaurar nodo 0
        nodes[0].IsOnline = true;
        // Eliminar documento
        RemoveDocument(file);
        // Intentar recuperar después de eliminar
        string contenidoRecuperado3 = GetDocument(file);
        Console.WriteLine($"Archivo tras eliminar: {(contenidoRecuperado3 == null ? "No disponible" : contenidoRecuperado3)}");
        Console.WriteLine("=== Fin de la prueba ===");
    }

    //conectarse con la app 

    public List<string> GetNombresDocumentos()
    {
        return new List<string>(fileTable.Keys);
    }

}


