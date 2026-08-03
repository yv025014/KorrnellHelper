using System.Text.Json.Serialization;

namespace KorrnellHelper.Api.Line;

public sealed class LineWebhookPayload
{
    [JsonPropertyName("events")]
    public LineEvent[] Events { get; init; } = [];
}

public sealed class LineEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("message")]
    public LineMessage? Message { get; init; }

    [JsonPropertyName("source")]
    public LineSource? Source { get; init; }

    [JsonPropertyName("replyToken")]
    public string? ReplyToken { get; init; }
}

public sealed class LineMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public sealed class LineSource
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("userId")]
    public string? UserId { get; init; }
}
