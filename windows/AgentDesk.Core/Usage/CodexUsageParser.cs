using System.Globalization;
using System.Text.Json;
using AgentDesk.Core.Models;

namespace AgentDesk.Core.Usage;

public static class CodexUsageParser
{
    public static CodexUsagePayload? Parse(JsonElement root, DateTimeOffset? now = null)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;

        if (!root.TryGetProperty("rate_limit", out var rateLimit) || rateLimit.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Primary window is used by default. Secondary window is only used as a documented fallback if primary_window is absent.
        if (!rateLimit.TryGetProperty("primary_window", out var primaryWindow) || primaryWindow.ValueKind != JsonValueKind.Object)
        {
            if (!rateLimit.TryGetProperty("secondary_window", out primaryWindow) || primaryWindow.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
        }

        if (!primaryWindow.TryGetProperty("used_percent", out var usedPercentEl) ||
            !primaryWindow.TryGetProperty("reset_at", out var resetAtEl))
        {
            return null;
        }

        if (!usedPercentEl.TryGetDouble(out var usedPercent) || !resetAtEl.TryGetInt64(out var resetAt))
        {
            return null;
        }

        int availableCount = 0;
        if (root.TryGetProperty("rate_limit_reset_credits", out var resetCredits) && resetCredits.ValueKind == JsonValueKind.Object)
        {
            if (resetCredits.TryGetProperty("available_count", out var availEl) && availEl.TryGetInt32(out var availVal))
            {
                availableCount = Math.Max(0, availVal);
            }
        }

        int remaining = Math.Clamp(100 - (int)Math.Round(usedPercent), 0, 100);

        var resetDateTime = DateTimeOffset.FromUnixTimeSeconds(resetAt).ToLocalTime();
        string resetDateStr = resetDateTime.ToString("M/d HH:mm", CultureInfo.InvariantCulture);
        string resetText = $"Re: {resetDateStr}";

        return new CodexUsagePayload
        {
            WeeklyRemainingPercent = remaining,
            ResetText = resetText,
            ResetDate = resetDateStr,
            ResetAvailable = availableCount
        };
    }
}
