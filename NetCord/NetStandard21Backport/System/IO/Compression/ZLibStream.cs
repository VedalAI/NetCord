// ReSharper disable once CheckNamespace
namespace System.IO.Compression;

public class ZLibStream : Stream
{
    private readonly Joveler.Compression.ZLib.ZLibStream _inner;

    public ZLibStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
    {
        if (mode == CompressionMode.Compress)
        {
            var opts = new Joveler.Compression.ZLib.ZLibCompressOptions
            {
                LeaveOpen = leaveOpen
            };
            _inner = new Joveler.Compression.ZLib.ZLibStream(stream, opts);
        }
        else
        {
            var opts = new Joveler.Compression.ZLib.ZLibDecompressOptions
            {
                LeaveOpen = leaveOpen
            };
            _inner = new Joveler.Compression.ZLib.ZLibStream(stream, opts);
        }
    }

    public ZLibStream(Stream stream, CompressionLevel level, bool leaveOpen = false)
    {
        var opts = new Joveler.Compression.ZLib.ZLibCompressOptions
        {
            Level = level switch
            {
                CompressionLevel.Optimal => Joveler.Compression.ZLib.ZLibCompLevel.Default,
                CompressionLevel.Fastest => Joveler.Compression.ZLib.ZLibCompLevel.BestSpeed,
                CompressionLevel.NoCompression => Joveler.Compression.ZLib.ZLibCompLevel.NoCompression,
                // CompressionLevel.SmallestSize => Joveler.Compression.ZLib.ZLibCompLevel.BestCompression,
                _ => Joveler.Compression.ZLib.ZLibCompLevel.Default
            },
            LeaveOpen = leaveOpen
        };
        _inner = new Joveler.Compression.ZLib.ZLibStream(stream, opts);
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
}
