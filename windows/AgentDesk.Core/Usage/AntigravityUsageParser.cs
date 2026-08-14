using System.Globalization;
using System.Text.RegularExpressions;
using AgentDesk.Core.Models;

namespace AgentDesk.Core.Usage;

public static class AntigravityUsageParser
{
    private static readonly Regex AnsiEscapeRegex = new(
        @"\x1b(?:\[[0-?]*[ -/]*[@-~]|\][^\x07]*(?:\x07|\x1b\\))",
        RegexOptions.Compiled);

    private static readonly Regex WindowRegex = new(
        @"^(weekly|five\s+hour)\s+limit\s+remaining(?:\s*[:\-]\s*(?<status>.*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PercentRegex = new(
        @"(?<!\d)(\d{1,3}(?:\.\d+)?)\s*%",
        RegexOptions.Compiled);

    private static readonly Regex RemainingRegex = new(
        @"^(?<percent>\d{1,3}(?:\.\d+)?)\s*%\s*remaining(?:\s*(?:·|•|-)?\s*(?<refresh>.*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private class UsageBucket
    {
        public string Group { get; set; } = "USAGE";
        public string Window { get; set; } = string.Empty;
        public int? RemainingPercent { get; set; }
        public string RefreshText { get; set; } = string.Empty;
    }

    public static AntigravityUsagePayload? Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        string cleanText = AnsiEscapeRegex.Replace(output, "").Replace("\r", "");
        var lines = cleanText.Split('\n');

        var buckets = new List<UsageBucket>();
        string currentGroup = "USAGE";
        string currentWindow = "";
        int? pendingPercent = null;
        string pendingRefresh = "";

        void FlushBucket()
        {
            if (!string.IsNullOrEmpty(currentWindow) && (pendingPercent.HasValue || pendingRefresh.Equals("disabled", StringComparison.OrdinalIgnoreCase)))
            {
                buckets.Add(new UsageBucket
                {
                    Group = currentGroup,
                    Window = currentWindow,
                    RemainingPercent = pendingPercent.HasValue ? Math.Clamp(pendingPercent.Value, 0, 100) : null,
                    RefreshText = pendingRefresh
                });
            }
            pendingPercent = null;
            pendingRefresh = "";
            currentWindow = "";
        }

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Handle tab-delimited print mode format: e.g. Group<TAB>Window<TAB>Percent/Status<TAB>ResetTime
            if (line.Contains('\t'))
            {
                FlushBucket();
                var parts = line.Split('\t').Select(p => p.Trim()).ToArray();
                if (parts.Length >= 2)
                {
                    string group = parts[0];
                    string windowCol = parts[1];
                    string statusOrPercentCol = parts.Length > 2 ? parts[2] : "";
                    string refreshOrDetailCol = parts.Length > 3 ? parts[3] : "";

                    string? window = NormalizeWindow(windowCol);
                    if (window != null)
                    {
                        int? percent = null;
                        string refresh = "";

                        if (IsDisabledStatus(statusOrPercentCol) || IsDisabledStatus(refreshOrDetailCol) || IsDisabledStatus(windowCol))
                        {
                            refresh = "disabled";
                            percent = null;
                        }
                        else
                        {
                            percent = ExtractPercent(statusOrPercentCol) ?? ExtractPercent(refreshOrDetailCol) ?? ExtractPercent(windowCol);

                            if (!string.IsNullOrEmpty(refreshOrDetailCol))
                            {
                                refresh = refreshOrDetailCol;
                            }
                            else if (percent.HasValue && !string.IsNullOrEmpty(statusOrPercentCol) && !PercentRegex.IsMatch(statusOrPercentCol) && !double.TryParse(statusOrPercentCol.TrimEnd('%').Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                            {
                                refresh = statusOrPercentCol;
                            }
                        }

                        if (percent.HasValue || refresh.Equals("disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            buckets.Add(new UsageBucket
                            {
                                Group = group,
                                Window = window,
                                RemainingPercent = percent.HasValue ? Math.Clamp(percent.Value, 0, 100) : null,
                                RefreshText = refresh
                            });
                        }
                    }
                }
                continue;
            }

            // Legacy interactive format parsing
            if (Regex.IsMatch(line, @"^Models within this group:\s*(.+)$", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var windowMatch = WindowRegex.Match(line);
            if (windowMatch.Success)
            {
                FlushBucket();
                currentWindow = windowMatch.Groups[1].Value.Equals("weekly", StringComparison.OrdinalIgnoreCase) ? "weekly" : "five-hour";
                if (windowMatch.Groups["status"].Success && IsDisabledStatus(windowMatch.Groups["status"].Value))
                {
                    pendingRefresh = "disabled";
                    FlushBucket();
                    continue;
                }
                var inlinePercent = PercentRegex.Match(line[windowMatch.Index..]);
                if (inlinePercent.Success && double.TryParse(inlinePercent.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pInline))
                {
                    pendingPercent = Math.Clamp((int)pInline, 0, 100);
                }
                continue;
            }

            if (IsGroupHeading(line))
            {
                FlushBucket();
                currentGroup = line;
                continue;
            }

            if (string.IsNullOrEmpty(currentWindow)) continue;

            if (IsDisabledStatus(line))
            {
                pendingRefresh = "disabled";
                pendingPercent = null;
                FlushBucket();
                continue;
            }

            var remainingMatch = RemainingRegex.Match(line);
            if (remainingMatch.Success)
            {
                if (double.TryParse(remainingMatch.Groups["percent"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pRem))
                {
                    pendingPercent = Math.Clamp((int)pRem, 0, 100);
                }
                pendingRefresh = (remainingMatch.Groups["refresh"].Value ?? "").Trim();
                if (!string.IsNullOrEmpty(pendingRefresh))
                {
                    FlushBucket();
                }
                continue;
            }

            if (!pendingPercent.HasValue)
            {
                var percentMatch = PercentRegex.Match(line);
                if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pVal))
                {
                    pendingPercent = Math.Clamp((int)pVal, 0, 100);
                    continue;
                }
            }

            if (pendingPercent.HasValue && line.Equals("Quota available", StringComparison.OrdinalIgnoreCase))
            {
                pendingRefresh = line;
                FlushBucket();
            }
        }

        FlushBucket();

        if (buckets.Count == 0) return null;

        var firstWeekly = buckets.FirstOrDefault(b => b.Window == "weekly");
        var firstFiveHour = buckets.FirstOrDefault(b => b.Window == "five-hour");

        var geminiGroup = buckets.Where(b => b.Group.Contains("GEMINI", StringComparison.OrdinalIgnoreCase)).ToList();
        var geminiFiveHour = geminiGroup.FirstOrDefault(b => b.Window == "five-hour");

        var claudeGroup = buckets.Where(b => b.Group.Contains("CLAUDE", StringComparison.OrdinalIgnoreCase)).ToList();
        var claudeFiveHour = claudeGroup.FirstOrDefault(b => b.Window == "five-hour");

        return new AntigravityUsagePayload
        {
            WeeklyRemainingPercent = firstWeekly?.RemainingPercent,
            WeeklyRefreshText = firstWeekly?.RefreshText ?? string.Empty,
            FiveHourRemainingPercent = firstFiveHour?.RemainingPercent,
            FiveHourRefreshText = firstFiveHour?.RefreshText ?? string.Empty,

            GeminiFiveHourRemainingPercent = geminiFiveHour?.RemainingPercent,
            GeminiFiveHourRefreshText = geminiFiveHour?.RefreshText ?? string.Empty,

            ClaudeFiveHourRemainingPercent = claudeFiveHour?.RemainingPercent,
            ClaudeFiveHourRefreshText = claudeFiveHour?.RefreshText ?? string.Empty,
        };
    }

    private static string? NormalizeWindow(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Contains("weekly", StringComparison.OrdinalIgnoreCase))
        {
            return "weekly";
        }
        if (text.Contains("five", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("5-hour", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("5 hour", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("5h", StringComparison.OrdinalIgnoreCase))
        {
            return "five-hour";
        }
        return null;
    }

    private static int? ExtractPercent(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = PercentRegex.Match(text);
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pVal))
        {
            return (int)pVal;
        }
        string trimmed = text.Trim();
        if (double.TryParse(trimmed.TrimEnd('%').Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num) && num >= 0 && num <= 100)
        {
            return (int)num;
        }
        return null;
    }

    private static bool IsDisabledStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string trimmed = text.Trim();
        return trimmed.Equals("disabled", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("(disabled)", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("quota disabled", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("not available", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("n/a", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(trimmed, @"^disabled\b", RegexOptions.IgnoreCase);
    }

    private static bool IsGroupHeading(string line)
    {
        return Regex.IsMatch(line, @"^[A-Z][A-Z0-9 &/+_.-]{2,}$") && !WindowRegex.IsMatch(line);
    }
}
