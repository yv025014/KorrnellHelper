using System.Text.Json.Serialization;

namespace KorrnellHelper.Infrastructure.Line;

internal sealed class PushRequest
{
    [JsonPropertyName("to")]
    public required string To { get; init; }

    [JsonPropertyName("messages")]
    public required ReplyMessage[] Messages { get; init; }
}
