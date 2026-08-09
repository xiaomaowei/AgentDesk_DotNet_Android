using System.Text;
using System.Text.Json.Nodes;
using AgentDesk.Hook;
using Xunit;

namespace AgentDesk.Hook.Tests;

public class HookCompactorTests
{
    [Fact]
    public void SmallNonPostToolUse_ReturnsRawBytesByteForByte()
    {
        string rawJson = "{\n  \"hook_event_name\": \"PreToolUse\",\n  \"tool_name\": \"bash\",\n  \"custom_field\": \"unaltered\"\n}";
        byte[] rawBytes = Encoding.UTF8.GetBytes(rawJson);
        var obj = JsonNode.Parse(rawBytes)!.AsObject();

        byte[] compacted = HookCompactor.CompactPayload(rawBytes, obj);

        Assert.Same(rawBytes, compacted);
    }

    [Fact]
    public void PostToolUse_RemovesToolResponseAndStaysUnder60KiB()
    {
        var obj = new JsonObject
        {
            ["hook_event_name"] = "PostToolUse",
            ["tool_name"] = "bash",
            ["tool_response"] = new JsonObject
            {
                ["stdout"] = new string('A', 50000),
                ["stderr"] = "some output"
            }
        };

        byte[] rawBytes = Encoding.UTF8.GetBytes(obj.ToJsonString());
        byte[] compacted = HookCompactor.CompactPayload(rawBytes, obj);

        Assert.True(compacted.Length <= HookCompactor.MaxPayloadBytes);

        var compactedObj = JsonNode.Parse(compacted)!.AsObject();
        Assert.False(compactedObj.ContainsKey("tool_response"));
        Assert.True(compactedObj["_tool_response_omitted"]?.GetValue<bool>());
        Assert.True(compactedObj["_tool_response_bytes"]?.GetValue<int>() > 50000);
    }

    [Fact]
    public void HugeUnicode_RemainsValidJsonAndUnder60KiB()
    {
        // Build a huge string containing multi-byte Unicode (emojis, CJK)
        var hugeSb = new StringBuilder();
        for (int i = 0; i < 20000; i++)
        {
            hugeSb.Append("🐉🚀繁體中文測試 Unicode Data Payload ");
        }

        var obj = new JsonObject
        {
            ["hook_event_name"] = "PreToolUse",
            ["model"] = "gpt-4",
            ["prompt"] = hugeSb.ToString(),
            ["last_assistant_message"] = hugeSb.ToString(),
            ["tool_input"] = new JsonObject
            {
                ["command"] = hugeSb.ToString(),
                ["description"] = hugeSb.ToString()
            }
        };

        byte[] rawBytes = Encoding.UTF8.GetBytes(obj.ToJsonString());
        Assert.True(rawBytes.Length > HookCompactor.MaxPayloadBytes);

        byte[] compacted = HookCompactor.CompactPayload(rawBytes, obj);

        Assert.True(compacted.Length <= HookCompactor.MaxPayloadBytes);

        // Verify valid JSON parse
        var compactedObj = JsonNode.Parse(compacted)?.AsObject();
        Assert.NotNull(compactedObj);
        Assert.Equal("PreToolUse", compactedObj["hook_event_name"]?.GetValue<string>());
        Assert.Equal("gpt-4", compactedObj["model"]?.GetValue<string>());
    }

    [Fact]
    public void Compaction_RetainsCommandModelPlanCoreFields()
    {
        var obj = new JsonObject
        {
            ["hook_event_name"] = "UserPrompt",
            ["session_id"] = "sess_12345",
            ["cwd"] = "/home/user/project",
            ["model"] = "claude-3-5-sonnet",
            ["effort"] = "high",
            ["tool_name"] = "bash",
            ["tool_use_id"] = "tu_999",
            ["prompt"] = new string('P', 20000),
            ["last_assistant_message"] = new string('M', 20000),
            ["tool_input"] = new JsonObject
            {
                ["command"] = "git status",
                ["path"] = "/home/user/file.txt",
                ["file_path"] = "file.txt",
                ["description"] = "Run status check",
                ["plan"] = new JsonArray
                {
                    new JsonObject { ["step"] = "1", ["title"] = "Check status" },
                    new JsonObject { ["step"] = "2", ["title"] = "Commit changes" }
                }
            }
        };

        byte[] rawBytes = Encoding.UTF8.GetBytes(obj.ToJsonString());
        byte[] compacted = HookCompactor.CompactPayload(rawBytes, obj);

        Assert.True(compacted.Length <= HookCompactor.MaxPayloadBytes);

        var compactedObj = JsonNode.Parse(compacted)!.AsObject();
        Assert.Equal("UserPrompt", compactedObj["hook_event_name"]?.GetValue<string>());
        Assert.Equal("sess_12345", compactedObj["session_id"]?.GetValue<string>());
        Assert.Equal("/home/user/project", compactedObj["cwd"]?.GetValue<string>());
        Assert.Equal("claude-3-5-sonnet", compactedObj["model"]?.GetValue<string>());
        Assert.Equal("high", compactedObj["effort"]?.GetValue<string>());
        Assert.Equal("bash", compactedObj["tool_name"]?.GetValue<string>());
        Assert.Equal("tu_999", compactedObj["tool_use_id"]?.GetValue<string>());

        var toolInputObj = compactedObj["tool_input"]?.AsObject();
        Assert.NotNull(toolInputObj);
        Assert.Equal("git status", toolInputObj["command"]?.GetValue<string>());
        Assert.Equal("/home/user/file.txt", toolInputObj["path"]?.GetValue<string>());
        Assert.Equal("file.txt", toolInputObj["file_path"]?.GetValue<string>());
        Assert.Equal("Run status check", toolInputObj["description"]?.GetValue<string>());

        var planArr = toolInputObj["plan"]?.AsArray();
        Assert.NotNull(planArr);
        Assert.Equal(2, planArr.Count);
    }
}
