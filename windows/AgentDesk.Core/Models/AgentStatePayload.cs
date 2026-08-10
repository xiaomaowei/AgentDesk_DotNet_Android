using System.Text.Json.Serialization;

namespace AgentDesk.Core.Models;

public record AgentStatePayload
{
    [JsonPropertyName("agent")]
    public string Agent { get; init; } = string.Empty;

    [JsonPropertyName("project")]
    public string Project { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("elapsed")]
    public long Elapsed { get; init; }

    [JsonPropertyName("requires_action")]
    public bool RequiresAction { get; init; }

    [JsonPropertyName("actions")]
    public List<string> Actions { get; init; } = new();

    [JsonPropertyName("target_id")]
    public string? TargetId { get; init; }

    [JsonPropertyName("conversation_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConversationName { get; init; }

    [JsonPropertyName("conversation_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ConversationTokens { get; init; }

    [JsonPropertyName("steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Steps { get; init; }

    [JsonPropertyName("current_step")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CurrentStep { get; init; }

    [JsonPropertyName("recent_events")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RecentEvent>? RecentEvents { get; init; }

    [JsonPropertyName("models")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Models { get; init; }

    [JsonPropertyName("codex_usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CodexUsagePayload? CodexUsage { get; init; }

    [JsonPropertyName("antigravity_usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AntigravityUsagePayload? AntigravityUsage { get; init; }

    [JsonPropertyName("current_turn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CurrentTurn? CurrentTurn { get; init; }
}
