using System.Text;
using System.Text.Json;

namespace AgentDesk.Core.Translators;

public record TranscriptParseResult
{
    public long? ConversationTokens { get; init; }
    public CommentaryItem? LatestCommentary { get; init; }
}

public record CommentaryItem
{
    public required string Id { get; init; }
    public required string Content { get; init; }
}

public static class TranscriptParser
{
    public const int MaxTailBytes = 256 * 1024; // 256 KiB

    public static TranscriptParseResult ParseTail(string? transcriptPath, int maxBytes = MaxTailBytes)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath) || !File.Exists(transcriptPath))
        {
            return new TranscriptParseResult();
        }

        byte[] buffer;
        bool isTailCut = false;
        try
        {
            using var fs = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long length = fs.Length;
            if (length <= 0)
            {
                return new TranscriptParseResult();
            }

            if (length > maxBytes)
            {
                fs.Seek(length - maxBytes, SeekOrigin.Begin);
                buffer = new byte[maxBytes];
                isTailCut = true;
            }
            else
            {
                buffer = new byte[(int)length];
            }

            int bytesRead = 0;
            while (bytesRead < buffer.Length)
            {
                int read = fs.Read(buffer, bytesRead, buffer.Length - bytesRead);
                if (read <= 0) break;
                bytesRead += read;
            }

            if (bytesRead < buffer.Length)
            {
                Array.Resize(ref buffer, bytesRead);
            }
        }
        catch
        {
            return new TranscriptParseResult();
        }

        string text = Encoding.UTF8.GetString(buffer);
        if (isTailCut)
        {
            int idx = text.IndexOf('\n');
            if (idx >= 0)
            {
                text = text[(idx + 1)..];
            }
            else
            {
                text = string.Empty;
            }
        }

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        long? latestTokens = null;
        CommentaryItem? latestCommentary = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;

                var tokens = ExtractTokens(root);
                if (tokens.HasValue && tokens.Value >= 0)
                {
                    latestTokens = tokens.Value;
                }

                var commentary = ExtractCommentary(root);
                if (commentary != null)
                {
                    latestCommentary = commentary;
                }
            }
            catch
            {
                // Fail open on invalid JSON line
            }
        }

        return new TranscriptParseResult
        {
            ConversationTokens = latestTokens,
            LatestCommentary = latestCommentary
        };
    }

    private static long? ExtractTokens(JsonElement root)
    {
        if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
        {
            string? pType = GetStringProp(payload, "type");
            if (pType == "token_count")
            {
                if (payload.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object &&
                    info.TryGetProperty("total_token_usage", out var usage) && usage.ValueKind == JsonValueKind.Object &&
                    usage.TryGetProperty("total_tokens", out var totalTokensProp))
                {
                    return ReadTokenValue(totalTokensProp);
                }
            }
        }

        string? topType = GetStringProp(root, "type");
        if (topType == "token_count")
        {
            if (root.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object &&
                info.TryGetProperty("total_token_usage", out var usage) && usage.ValueKind == JsonValueKind.Object &&
                usage.TryGetProperty("total_tokens", out var totalTokensProp))
            {
                return ReadTokenValue(totalTokensProp);
            }
        }

        return null;
    }

    private static long? ReadTokenValue(JsonElement prop)
    {
        if (prop.ValueKind == JsonValueKind.Number)
        {
            if (prop.TryGetInt64(out long lVal))
            {
                return lVal >= 0 ? lVal : null;
            }
            if (prop.TryGetDouble(out double dVal))
            {
                if (dVal >= 0 && !double.IsNaN(dVal) && !double.IsInfinity(dVal))
                {
                    return (long)dVal;
                }
            }
        }
        return null;
    }

    private static CommentaryItem? ExtractCommentary(JsonElement root)
    {
        string? topType = GetStringProp(root, "type");
        if (topType != "response_item") return null;

        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
            return null;

        string? pType = GetStringProp(payload, "type");
        string? role = GetStringProp(payload, "role");
        string? phase = GetStringProp(payload, "phase");

        if (pType == "message" && role == "assistant" && phase == "commentary")
        {
            string? id = GetStringProp(payload, "id");
            if (string.IsNullOrWhiteSpace(id)) return null;

            if (payload.TryGetProperty("content", out var contentProp))
            {
                string? text = ExtractTextContent(contentProp);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return new CommentaryItem
                    {
                        Id = id.Trim(),
                        Content = text.Trim()
                    };
                }
            }
        }

        return null;
    }

    private static string? ExtractTextContent(JsonElement contentEl)
    {
        if (contentEl.ValueKind == JsonValueKind.String)
        {
            return contentEl.GetString();
        }

        if (contentEl.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in contentEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrEmpty(s))
                    {
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(s);
                    }
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    string? text = null;
                    if (item.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                    {
                        text = textProp.GetString();
                    }
                    else if (item.TryGetProperty("content", out var cProp) && cProp.ValueKind == JsonValueKind.String)
                    {
                        text = cProp.GetString();
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(text);
                    }
                }
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        if (contentEl.ValueKind == JsonValueKind.Object)
        {
            if (contentEl.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            {
                return textProp.GetString();
            }
            if (contentEl.TryGetProperty("content", out var cProp) && cProp.ValueKind == JsonValueKind.String)
            {
                return cProp.GetString();
            }
        }

        return null;
    }

    private static string? GetStringProp(JsonElement el, string propertyName)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }
}
