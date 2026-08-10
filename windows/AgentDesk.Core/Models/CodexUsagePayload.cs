using System.Text.Json.Serialization;

namespace AgentDesk.Core.Models;

public record CodexUsagePayload
{
    [JsonPropertyName("weekly_remaining_percent")]
    public int? WeeklyRemainingPercent { get; init; }

    [JsonPropertyName("reset_text")]
    public string? ResetText { get; init; }

    [JsonPropertyName("reset_date")]
    public string? ResetDate { get; init; }

    [JsonPropertyName("reset_available")]
    public int? ResetAvailable { get; init; }
}
