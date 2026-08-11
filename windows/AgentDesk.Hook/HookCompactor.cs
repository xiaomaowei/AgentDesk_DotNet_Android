using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentDesk.Hook;

public static class HookCompactor
{
    public const int MaxPayloadBytes = 60 * 1024; // 60 KiB = 61,440 bytes

    private static readonly HashSet<string> AllowedToolInputKeys = new(StringComparer.Ordinal)
    {
        "command", "description", "path", "file_path", "plan"
    };

    private static readonly JsonSerializerOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static byte[] CompactPayload(byte[] rawBytes, JsonObject eventObj)
    {
        string? hookEventName = GetString(eventObj["hook_event_name"]);
        bool isPostToolUse = string.Equals(hookEventName, "PostToolUse", StringComparison.Ordinal);

        // 1. If small non-PostToolUse event with rawBytes <= MaxPayloadBytes, return rawBytes intact
        if (rawBytes.Length <= MaxPayloadBytes && !isPostToolUse)
        {
            return rawBytes;
        }

        var working = eventObj.DeepClone() as JsonObject ?? new JsonObject();

        // 2. For PostToolUse, remove top-level tool_response field
        if (isPostToolUse && working.ContainsKey("tool_response"))
        {
            var respNode = working["tool_response"];
            working.Remove("tool_response");

            int respBytes = 0;
            if (respNode != null)
            {
                try
                {
                    respBytes = JsonSerializer.SerializeToUtf8Bytes(respNode, WriterOptions).Length;
                }
                catch
                {
                    respBytes = 0;
                }
            }

            working["_tool_response_omitted"] = true;
            working["_tool_response_bytes"] = respBytes;
        }

        byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(working, WriterOptions);
        if (encoded.Length <= MaxPayloadBytes)
        {
            return encoded;
        }

        // 3. First compaction pass: project tool_input & truncate prompt/last_assistant_message
        if (working.ContainsKey("tool_input"))
        {
            var tInObj = GetJsonObject(working["tool_input"]);
            if (tInObj != null)
            {
                working["tool_input"] = CompactToolInput(tInObj, maxStrLen: 4000);
            }
            else
            {
                var tInStr = GetString(working["tool_input"]);
                if (tInStr != null)
                {
                    working["tool_input"] = TruncateString(tInStr, 4000);
                }
            }
        }

        TruncatePropertyIfString(working, "prompt", 8000);
        TruncatePropertyIfString(working, "last_assistant_message", 4000);

        encoded = JsonSerializer.SerializeToUtf8Bytes(working, WriterOptions);
        if (encoded.Length <= MaxPayloadBytes)
        {
            return encoded;
        }

        // 4. Secondary compaction pass (stricter limits)
        if (working.ContainsKey("tool_input"))
        {
            var tInObj2 = GetJsonObject(working["tool_input"]);
            if (tInObj2 != null)
            {
                working["tool_input"] = CompactToolInput(tInObj2, maxStrLen: 1000);
            }
        }

        TruncatePropertyIfString(working, "prompt", 1000);
        TruncatePropertyIfString(working, "last_assistant_message", 1000);

        encoded = JsonSerializer.SerializeToUtf8Bytes(working, WriterOptions);
        if (encoded.Length <= MaxPayloadBytes)
        {
            return encoded;
        }

        // 5. Fallback object with bounded lists and scalar strings
        var fallback = new JsonObject();
        string[] scalarStringKeys =
        [
            "hook_event_name",
            "session_id",
            "cwd",
            "model",
            "effort",
            "transcript_path",
            "tool_name",
            "conversation_title",
            "thread_name",
            "conversation_name",
            "tool_use_id"
        ];

        foreach (var key in scalarStringKeys)
        {
            string? strVal = GetString(working[key]);
            if (strVal != null)
            {
                fallback[key] = TruncateString(strVal, 200);
            }
        }

        string[] primitiveKeys = ["current_step", "progress_step", "_tool_response_omitted", "_tool_response_bytes"];
        foreach (var key in primitiveKeys)
        {
            if (working.ContainsKey(key) && working[key] != null)
            {
                fallback[key] = working[key]?.DeepClone();
            }
        }

        string[] listKeys = ["steps", "progress_steps", "plan"];
        foreach (var key in listKeys)
        {
            var arr = GetJsonArray(working[key]);
            if (arr != null)
            {
                fallback[key] = CompactList(arr, maxItems: 20, maxStr: 200);
            }
        }

        if (working.ContainsKey("tool_input"))
        {
            var tInObj3 = GetJsonObject(working["tool_input"]);
            if (tInObj3 != null)
            {
                fallback["tool_input"] = CompactToolInput(tInObj3, maxStrLen: 500);
            }
        }

        string? pStr = GetString(working["prompt"]);
        if (pStr != null)
        {
            fallback["prompt"] = TruncateString(pStr, 500);
        }

        string? mStr = GetString(working["last_assistant_message"]);
        if (mStr != null)
        {
            fallback["last_assistant_message"] = TruncateString(mStr, 500);
        }

        encoded = JsonSerializer.SerializeToUtf8Bytes(fallback, WriterOptions);
        if (encoded.Length <= MaxPayloadBytes)
        {
            return encoded;
        }

        // 6. Minimal guaranteed payload (hard safety net <= MaxPayloadBytes)
        var minimal = new JsonObject();
        string[] minimalKeys = ["hook_event_name", "session_id", "cwd", "model", "tool_name", "tool_use_id", "effort"];
        foreach (var key in minimalKeys)
        {
            string? strVal = GetString(working[key]);
            if (strVal != null)
            {
                minimal[key] = TruncateString(strVal, 100);
            }
        }

        if (working.ContainsKey("_tool_response_omitted") && working["_tool_response_omitted"] != null)
        {
            minimal["_tool_response_omitted"] = working["_tool_response_omitted"]?.DeepClone();
        }
        if (working.ContainsKey("_tool_response_bytes") && working["_tool_response_bytes"] != null)
        {
            minimal["_tool_response_bytes"] = working["_tool_response_bytes"]?.DeepClone();
        }

        return JsonSerializer.SerializeToUtf8Bytes(minimal, WriterOptions);
    }

    private static JsonObject CompactToolInput(JsonObject toolInput, int maxStrLen)
    {
        var compact = new JsonObject();
        foreach (var key in AllowedToolInputKeys)
        {
            if (!toolInput.ContainsKey(key))
            {
                continue;
            }

            var node = toolInput[key];
            var planArr = GetJsonArray(node);
            if (string.Equals(key, "plan", StringComparison.Ordinal) && planArr != null)
            {
                compact["plan"] = CompactList(planArr, maxItems: 20, maxStr: 200);
            }
            else
            {
                string? strVal = GetString(node);
                if (strVal != null)
                {
                    compact[key] = TruncateString(strVal, maxStrLen);
                }
                else if (node != null)
                {
                    compact[key] = node.DeepClone();
                }
            }
        }
        return compact;
    }

    private static JsonArray CompactList(JsonArray arr, int maxItems, int maxStr)
    {
        var compactArr = new JsonArray();
        int count = 0;
        foreach (var item in arr)
        {
            if (count >= maxItems)
            {
                break;
            }
            count++;

            string? strVal = GetString(item);
            if (strVal != null)
            {
                compactArr.Add(TruncateString(strVal, maxStr));
            }
            else
            {
                var itemObj = GetJsonObject(item);
                if (itemObj != null)
                {
                    var compactItemObj = new JsonObject();
                    string[] dictKeys = ["step", "title", "name", "status"];
                    foreach (var k in dictKeys)
                    {
                        if (itemObj.ContainsKey(k))
                        {
                            var propNode = itemObj[k];
                            string? pStr = GetString(propNode);
                            if (pStr != null)
                            {
                                compactItemObj[k] = TruncateString(pStr, maxStr);
                            }
                            else if (propNode != null)
                            {
                                compactItemObj[k] = propNode.DeepClone();
                            }
                        }
                    }
                    compactArr.Add(compactItemObj);
                }
                else if (item != null)
                {
                    compactArr.Add(item.DeepClone());
                }
            }
        }
        return compactArr;
    }

    private static void TruncatePropertyIfString(JsonObject obj, string propName, int maxChars)
    {
        string? strVal = GetString(obj[propName]);
        if (strVal != null)
        {
            obj[propName] = TruncateString(strVal, maxChars);
        }
    }

    public static string TruncateString(string val, int maxChars)
    {
        if (string.IsNullOrEmpty(val) || val.Length <= maxChars)
        {
            return val;
        }

        int len = maxChars;
        if (char.IsHighSurrogate(val[len - 1]))
        {
            len--;
        }
        return val.Substring(0, len);
    }

    private static string? GetString(JsonNode? node)
    {
        if (node is JsonValue val && val.GetValueKind() == JsonValueKind.String)
        {
            return val.GetValue<string>();
        }
        return null;
    }

    private static JsonObject? GetJsonObject(JsonNode? node)
    {
        return node as JsonObject;
    }

    private static JsonArray? GetJsonArray(JsonNode? node)
    {
        return node as JsonArray;
    }
}
