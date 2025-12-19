namespace NetCord.Gateway.Compression;

public interface IGatewayCompression : IDisposable
{
    internal static IGatewayCompression CreateDefault()
    {
        return Zstandard.TryLoad() ? new ZstandardGatewayCompression() : throw new Exception("Cannot load libzstd"); // new ZLibGatewayCompression();
    }

    public string Name { get; }

    public ReadOnlySpan<byte> Decompress(ReadOnlySpan<byte> payload);

    public void Initialize();
}
