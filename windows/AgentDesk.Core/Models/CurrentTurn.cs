using System.Text.Json.Serialization;

namespace AgentDesk.Core.Models;

public record CurrentTurn
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("started_at")]
    public string StartedAt { get; init; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public List<TurnItem> Items { get; init; } = new();
}
