namespace DiskNode.Services;
using System.Xml;




public class BlockStorage
{

    // ATRIBUTES
    private readonly string path;
    private readonly int blockSize;
    private readonly int blockLimit;

    // GETTERS
    public int GetBlockSize() => blockSize;      

    public BlockStorage(IConfiguration config)      // Constructor that initializes the storage path and block size from configuration
    {
        path = config["DiskNode:Path"];
        var totalSize = int.Parse(config["DiskNode:TotalSizeMB"]) * 1024 * 1024;

        this.blockSize = int.Parse(config["DiskNode:BlockSizeKB"]) * 1024;
        this.blockLimit = totalSize / blockSize;

        Directory.CreateDirectory(path);
    }

    private string GetBlockPath(long index)=> Path.Combine(path, $"block_{index:D8}.bin");

    public async Task WriteAsync(long index, Stream source, CancellationToken cancellationToken)        // Method to write a block of data to the storage
    {
        if (index >= blockLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "The limit was reached");          
        }

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "The index can't be negative");
        }

        await using var fileToSeek = new FileStream(GetBlockPath(index), FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        cancellationToken.ThrowIfCancellationRequested();
        await source.CopyToAsync(fileToSeek, 81920, cancellationToken);

    }

    public async Task<FileStream> ReadAsync(long index, CancellationToken cancellationToken)     // Methot to read a block of data from the storage
    {
        var blockPath = GetBlockPath(index);

        if (!File.Exists(blockPath))
        {
            throw new FileNotFoundException("Archivo no encontrado", blockPath); 
        }
        return new FileStream(blockPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
    }

    public Task DeleteAsync(long index, CancellationToken ct)
    {
        if (index < 0 || index >= blockLimit)
            throw new ArgumentOutOfRangeException(nameof(index));

        string path = GetBlockPath(index);
        if (File.Exists(path)) File.Delete(path);

        return Task.CompletedTask;
    }



}

