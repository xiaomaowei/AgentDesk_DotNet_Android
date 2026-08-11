using System.Text.Json.Serialization;

namespace AgentDesk.Core.Models;

public record AntigravityUsagePayload
{
    [JsonPropertyName("weekly_remaining_percent")]
    public int? WeeklyRemainingPercent { get; init; }

    [JsonPropertyName("weekly_refresh_text")]
    public string? WeeklyRefreshText { get; init; }

    [JsonPropertyName("five_hour_remaining_percent")]
    public int? FiveHourRemainingPercent { get; init; }

    [JsonPropertyName("five_hour_refresh_text")]
    public string? FiveHourRefreshText { get; init; }

    [JsonPropertyName("gemini_five_hour_remaining_percent")]
    public int? GeminiFiveHourRemainingPercent { get; init; }

    [JsonPropertyName("gemini_five_hour_refresh_text")]
    public string? GeminiFiveHourRefreshText { get; init; }

    [JsonPropertyName("claude_five_hour_remaining_percent")]
    public int? ClaudeFiveHourRemainingPercent { get; init; }

    [JsonPropertyName("claude_five_hour_refresh_text")]
    public string? ClaudeFiveHourRefreshText { get; init; }
}
