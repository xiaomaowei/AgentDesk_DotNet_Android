using System.Text.RegularExpressions;

namespace AgentDesk.Core.Translators;

public static class ModelHelper
{
    private static readonly (Regex Pattern, string Display)[] SlugPatterns = new[]
    {
        (new Regex(@"gemini-3[.-]6-flash", RegexOptions.IgnoreCase), "Gemini 3.6 Flash"),
        (new Regex(@"gemini-3[.-]6-pro", RegexOptions.IgnoreCase), "Gemini 3.6 Pro"),
        (new Regex(@"gemini-3[.-]6", RegexOptions.IgnoreCase), "Gemini 3.6"),
        (new Regex(@"gemini-2[.-]5-flash", RegexOptions.IgnoreCase), "Gemini 2.5 Flash"),
        (new Regex(@"gemini-2[.-]5-pro", RegexOptions.IgnoreCase), "Gemini 2.5 Pro"),
        (new Regex(@"gemini", RegexOptions.IgnoreCase), "Gemini"),
        (new Regex(@"claude-sonnet-4-6", RegexOptions.IgnoreCase), "Claude Sonnet 4.6"),
        (new Regex(@"claude-sonnet-4-5", RegexOptions.IgnoreCase), "Claude Sonnet 4.5"),
        (new Regex(@"claude-opus-4", RegexOptions.IgnoreCase), "Claude Opus 4"),
        (new Regex(@"claude-sonnet", RegexOptions.IgnoreCase), "Claude Sonnet"),
        (new Regex(@"claude-haiku", RegexOptions.IgnoreCase), "Claude Haiku"),
        (new Regex(@"claude-opus", RegexOptions.IgnoreCase), "Claude Opus"),
        (new Regex(@"claude", RegexOptions.IgnoreCase), "Claude"),
        (new Regex(@"gpt-5[.-]6-sol", RegexOptions.IgnoreCase), "Sol"),
        (new Regex(@"gpt-5[.-]6-luna", RegexOptions.IgnoreCase), "Luna"),
        (new Regex(@"gpt-5[.-]6-terra", RegexOptions.IgnoreCase), "Terra"),
        (new Regex(@"\bsol\b", RegexOptions.IgnoreCase), "Sol"),
        (new Regex(@"\bluna\b", RegexOptions.IgnoreCase), "Luna"),
        (new Regex(@"\bterra\b", RegexOptions.IgnoreCase), "Terra"),
        (new Regex(@"gpt-5[.-]\d", RegexOptions.IgnoreCase), "GPT-5"),
        (new Regex(@"gpt-4[.-]o", RegexOptions.IgnoreCase), "GPT-4o"),
        (new Regex(@"o3", RegexOptions.IgnoreCase), "o3"),
        (new Regex(@"o4", RegexOptions.IgnoreCase), "o4")
    };

    private static readonly Dictionary<string, string> EffortDisplay = new(StringComparer.OrdinalIgnoreCase)
    {
        ["high"] = "High",
        ["medium"] = "Medium",
        ["low"] = "Low"
    };

    private static readonly string[] EffortSuffixes = new[] { " High", " Medium", " Low" };

    public static string GetModelBaseName(string label)
    {
        if (string.IsNullOrEmpty(label)) return string.Empty;
        foreach (var suffix in EffortSuffixes)
        {
            if (label.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return label[..^suffix.Length];
            }
        }
        return label;
    }

    public static bool HasEffortSuffix(string label)
    {
        if (string.IsNullOrEmpty(label)) return false;
        foreach (var suffix in EffortSuffixes)
        {
            if (label.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static string NormalizeModelSlug(string slug, string? effort = null)
    {
        if (string.IsNullOrWhiteSpace(slug)) return string.Empty;
        var cleanSlug = slug.Trim();
        foreach (var (pattern, display) in SlugPatterns)
        {
            if (pattern.IsMatch(cleanSlug))
            {
                var effortClean = effort?.Trim();
                if ((display == "Sol" || display == "Luna" || display == "Terra") && !string.IsNullOrEmpty(effortClean))
                {
                    if (EffortDisplay.TryGetValue(effortClean, out var suffix))
                    {
                        var full = $"{display} {suffix}";
                        return full.Length > 32 ? full[..32] : full;
                    }
                }
                return display.Length > 32 ? display[..32] : display;
            }
        }

        var fallback = cleanSlug.Replace("-", " ").Replace("_", " ");
        var textInfo = System.Globalization.CultureInfo.InvariantCulture.TextInfo;
        fallback = textInfo.ToTitleCase(fallback);
        return fallback.Length > 32 ? fallback[..32] : fallback;
    }

    private static readonly Regex AgyModelRegex = new(
        @"\bagy(?:\.exe)?\b.*?(?:--model)(?:\s*=\s*|\s+)(?:""([^""]+)""|'([^']+)'|([^\s""'`]+))",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static string? ExtractAgyModel(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var matches = AgyModelRegex.Matches(command);
        string? lastModel = null;
        foreach (Match match in matches)
        {
            for (int i = 1; i <= 3; i++)
            {
                if (match.Groups[i].Success && !string.IsNullOrWhiteSpace(match.Groups[i].Value))
                {
                    lastModel = match.Groups[i].Value.Trim();
                    break;
                }
            }
        }
        return lastModel;
    }
}
