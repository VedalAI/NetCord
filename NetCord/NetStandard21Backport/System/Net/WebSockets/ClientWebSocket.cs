// ReSharper disable once CheckNamespace
namespace System.Net.WebSockets;

public static class ClientWebSocketExtensions
{
    extension(ClientWebSocket self)
    {
        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, WebSocketMessageFlags messageFlags, CancellationToken cancellationToken)
        {
            return self.SendAsync(buffer, messageType, messageFlags.HasFlag(WebSocketMessageFlags.EndOfMessage), cancellationToken);
        }
    }
}
