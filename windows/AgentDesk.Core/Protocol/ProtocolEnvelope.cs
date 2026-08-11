using System.Text.Json;
using System.Text.Json.Serialization;
using AgentDesk.Core.Models;

namespace AgentDesk.Core.Protocol;

public static class ProtocolSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };
}

public record ProtocolEnvelope<T>
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("payload")]
    public T Payload { get; init; } = default!;
}

public record ActionPayload
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("target_id")]
    public string? TargetId { get; init; }
}

public record ActionResultPayload
{
    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; }

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;
}

public record DashboardSnapshot
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("current")]
    public ProtocolEnvelope<AgentStatePayload>? Current { get; init; }

    [JsonPropertyName("projects")]
    public List<ProtocolEnvelope<AgentStatePayload>> Projects { get; init; } = new();
}
