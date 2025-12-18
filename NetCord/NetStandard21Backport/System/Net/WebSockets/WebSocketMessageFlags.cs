// ReSharper disable once CheckNamespace
namespace System.Net.WebSockets;

[Flags]
public enum WebSocketMessageFlags
{
    None = 0,
    EndOfMessage = 1 << 0,
    DisableCompression = 1 << 1,
}
