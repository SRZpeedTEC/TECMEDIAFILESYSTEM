using System.IO;

public class ProgressStream : Stream
{
    private readonly Stream _inner;
    private readonly IProgress<long> _progress;
    private long _bytesSoFar;

    public ProgressStream(Stream inner, IProgress<long> progress)
    { _inner = inner; _progress = progress; }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
                                                   CancellationToken ct = default)
    {
        int n = await _inner.ReadAsync(buffer, ct);
        _bytesSoFar += n;
        _progress.Report(_bytesSoFar);
        return n;
    }


    public override int Read(byte[] b, int o, int c) =>
        throw new NotSupportedException();
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
