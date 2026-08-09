using System.Text.Json.Serialization;

namespace AgentDesk.Core.Models;

public static class TurnItemKind
{
    public const string Commentary = "commentary";
    public const string Tool = "tool";
    public const string Approval = "approval";
    public const string Final = "final";
}

public static class TurnItemPhase
{
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Waiting = "waiting";
    public const string Delivered = "delivered";
}

public record TurnItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    [JsonIgnore]
    public string Sig { get; set; } = string.Empty;
}
