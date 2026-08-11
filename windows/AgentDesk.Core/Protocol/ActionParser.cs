using System.Text.Json;
using AgentDesk.Core.Models;

namespace AgentDesk.Core.Protocol;

public static class ActionParser
{
    public static ActionPayload Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Action JSON cannot be empty.");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return Parse(doc.RootElement);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON format: {ex.Message}", ex);
        }
    }

    public static ActionPayload Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Action payload must be a JSON object.");
        }

        if (!root.TryGetProperty("version", out var versionProp) || versionProp.GetString() != "1.0")
        {
            throw new ArgumentException("Protocol version must be '1.0'.");
        }

        if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "action")
        {
            throw new ArgumentException("Protocol envelope type must be 'action'.");
        }

        if (!root.TryGetProperty("payload", out var payloadProp) || payloadProp.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Action envelope must contain object payload.");
        }

        if (!payloadProp.TryGetProperty("action", out var actionProp) || actionProp.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("Action payload must specify string action.");
        }

        var action = actionProp.GetString() ?? string.Empty;
        if (!DeviceAction.All.Contains(action))
        {
            throw new ArgumentException($"Unsupported action: '{action}'.");
        }

        string? targetId = null;
        if (payloadProp.TryGetProperty("target_id", out var targetIdProp))
        {
            if (targetIdProp.ValueKind == JsonValueKind.String)
            {
                targetId = targetIdProp.GetString();
            }
            else if (targetIdProp.ValueKind != JsonValueKind.Null)
            {
                throw new ArgumentException("target_id must be a string or null.");
            }
        }

        return new ActionPayload
        {
            Action = action,
            TargetId = targetId
        };
    }
}
