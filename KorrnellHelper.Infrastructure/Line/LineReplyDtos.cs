using System.Text.Json.Serialization;

namespace KorrnellHelper.Infrastructure.Line;

internal sealed class ReplyRequest
{
    [JsonPropertyName("replyToken")]
    public required string ReplyToken { get; init; }

    [JsonPropertyName("messages")]
    public required ReplyMessage[] Messages { get; init; }
}

internal sealed class ReplyMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
