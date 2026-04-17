using System.Text.Json;

namespace AetherCore.WebSockets
{
    public record WsEnvelope(string Type, JsonElement? Payload);
}
