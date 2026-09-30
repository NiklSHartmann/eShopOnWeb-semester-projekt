using System.Text.Json;

namespace Messaging.Shared;

public static class MessageSerializer
{
    // Web defaults = camelCase property names, case-insensitive reading.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static byte[] Serialize<T>(T message) =>
        JsonSerializer.SerializeToUtf8Bytes(message, Options);

    public static T Deserialize<T>(ReadOnlyMemory<byte> body) =>
        JsonSerializer.Deserialize<T>(body.Span, Options)
        ?? throw new JsonException($"Message body deserialized to null for {typeof(T).Name}.");
}
