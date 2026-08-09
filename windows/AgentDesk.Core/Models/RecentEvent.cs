using System.Text.Json.Serialization;

namespace AgentDesk.Core.Models;

public static class RecentEventKind
{
    public const string Command = "command";
    public const string Tool = "tool";
    public const string Reply = "reply";
    public const string Status = "status";
}

public record RecentEvent
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }
}
