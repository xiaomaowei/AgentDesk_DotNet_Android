using System.Text.Json;
using AgentDesk.Core.Models;
using AgentDesk.Core.Translators;
using Xunit;

namespace AgentDesk.Core.Tests;

public class CodexTranslatorParityTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string CreateTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors in temp files
            }
        }
    }

    [Fact]
    public void Translate_LatestCommentaryOnly_And_DeduplicatesAcrossEvents()
    {
        var translator = new CodexTranslator();

        // Transcript contains TWO commentary records from assistant (e.g. earlier turn + current turn)
        var transcriptContent = """
        {"type":"response_item","payload":{"type":"message","role":"assistant","phase":"commentary","id":"msg_comm_01","content":[{"type":"text","text":"Earlier turn commentary"}]}}
        {"type":"response_item","payload":{"type":"message","role":"assistant","phase":"commentary","id":"msg_comm_02","content":[{"type":"text","text":"Latest current turn commentary"}]}}
        {"type":"response_item","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":4200}}}}
        """;
        var transcriptPath = CreateTempFile(transcriptContent);

        // 1. UserPromptSubmit
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s_parity_1","cwd":"/app/project","prompt":"Refactor codebase"}"""))
        {
            translator.Translate(doc.RootElement);
        }

        // 2. PreToolUse with transcript_path
        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"PreToolUse","session_id":"s_parity_1","tool_name":"exec_cmd","tool_use_id":"call_1","transcript_path":{{JsonSerializer.Serialize(transcriptPath)}}}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(4200, update.State.ConversationTokens);

            var items = update.State.CurrentTurn?.Items;
            Assert.NotNull(items);

            // Exactly 2 items: ONLY the latest commentary (msg_comm_02) and the tool item
            Assert.Equal(2, items.Count);

            // Chronological order: latest commentary then tool
            Assert.Equal(TurnItemKind.Commentary, items[0].Kind);
            Assert.Equal(TurnItemPhase.Delivered, items[0].Phase);
            Assert.Equal("Commentary", items[0].Label);
            Assert.Equal("Latest current turn commentary", items[0].Content);
            Assert.EndsWith("msg_comm_02", items[0].Id);

            Assert.Equal(TurnItemKind.Tool, items[1].Kind);
            Assert.Equal(TurnItemPhase.Running, items[1].Phase);
        }

        // 3. PostToolUse with same transcript_path (same latest commentary id msg_comm_02)
        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"PostToolUse","session_id":"s_parity_1","tool_name":"exec_cmd","tool_use_id":"call_1","transcript_path":{{JsonSerializer.Serialize(transcriptPath)}}}"""))
        {
            var update = translator.Translate(doc.RootElement);
            var items = update.State.CurrentTurn?.Items;
            Assert.NotNull(items);

            // Commentary must NOT be duplicated
            Assert.Equal(2, items.Count);
            Assert.Equal(TurnItemKind.Commentary, items[0].Kind);
            Assert.EndsWith("msg_comm_02", items[0].Id);
            Assert.Equal(TurnItemKind.Tool, items[1].Kind);
            Assert.Equal(TurnItemPhase.Completed, items[1].Phase);
        }
    }

    [Fact]
    public void Translate_InvalidAndUnreadableTranscriptBehavior()
    {
        var translator = new CodexTranslator();

        // Prompt
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s_invalid_1","prompt":"Test"}"""))
        {
            translator.Translate(doc.RootElement);
        }

        // Missing transcript path
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","session_id":"s_invalid_1","tool_name":"test_tool","tool_use_id":"t1"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Null(update.State.ConversationTokens);
        }

        // Non-existent transcript file path
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","session_id":"s_invalid_1","tool_name":"test_tool","tool_use_id":"t2","transcript_path":"C:\\non_existent_path_xyz_12345.jsonl"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Null(update.State.ConversationTokens);
        }

        // Invalid JSON lines in transcript file
        var badFile = CreateTempFile("{{bad json line 1\nnot json line 2\n{\"type\":\"response_item\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":999}}}}\n");
        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"PreToolUse","session_id":"s_invalid_1","tool_name":"test_tool","tool_use_id":"t3","transcript_path":{{JsonSerializer.Serialize(badFile)}}}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(999, update.State.ConversationTokens);
        }
    }

    [Fact]
    public void Translate_TailCutMidLine_IgnoresPartialFirstLine()
    {
        var translator = new CodexTranslator();
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s_tail_1","prompt":"Test tail cut"}"""))
        {
            translator.Translate(doc.RootElement);
        }

        // Create a large file (> 256 KiB)
        var padding = new string('A', 260 * 1024);
        var fileContent = padding + "partial_cut_line_without_json\n{\"type\":\"response_item\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":7777}}}}\n";
        var largeFile = CreateTempFile(fileContent);

        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"PreToolUse","session_id":"s_tail_1","tool_name":"tool1","tool_use_id":"t1","transcript_path":{{JsonSerializer.Serialize(largeFile)}}}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(7777, update.State.ConversationTokens);
        }
    }

    [Fact]
    public void Translate_IntegerAndFloatingTokenCounts()
    {
        var translator = new CodexTranslator();

        // Integer token count
        var intFile = CreateTempFile("""{"type":"response_item","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":1234}}}}""");
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s_tok_1","prompt":"Int tokens"}"""))
        {
            translator.Translate(doc.RootElement);
        }
        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"PreToolUse","session_id":"s_tok_1","tool_name":"t1","transcript_path":{{JsonSerializer.Serialize(intFile)}}}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(1234, update.State.ConversationTokens);
        }

        // Floating token count (truncated toward zero)
        var floatFile = CreateTempFile("""{"type":"response_item","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":2500.75}}}}""");
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s_tok_2","prompt":"Float tokens"}"""))
        {
            translator.Translate(doc.RootElement);
        }
        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"PreToolUse","session_id":"s_tok_2","tool_name":"t1","transcript_path":{{JsonSerializer.Serialize(floatFile)}}}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(2500, update.State.ConversationTokens);
        }

        // Negative token count
        var negFile = CreateTempFile("""{"type":"response_item","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":-100}}}}""");
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s_tok_3","prompt":"Negative tokens"}"""))
        {
            translator.Translate(doc.RootElement);
        }
        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"PreToolUse","session_id":"s_tok_3","tool_name":"t1","transcript_path":{{JsonSerializer.Serialize(negFile)}}}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Null(update.State.ConversationTokens);
        }
    }

    [Fact]
    public void Translate_SessionEnd_ClearsCache_And_BoundedCacheEvictsOldest()
    {
        var translator = new CodexTranslator();
        var commFile = CreateTempFile("""{"type":"response_item","payload":{"type":"message","role":"assistant","phase":"commentary","id":"msg_end_1","content":"Session end test commentary"}}""");

        // 1. Session 1 prompt & tool
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s_cache_1","prompt":"Prompt"}"""))
            translator.Translate(doc.RootElement);

        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"PreToolUse","session_id":"s_cache_1","tool_name":"t1","transcript_path":{{JsonSerializer.Serialize(commFile)}}}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Single(update.State.CurrentTurn!.Items.Where(i => i.Kind == TurnItemKind.Commentary));
        }

        // SessionEnd for s_cache_1
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"SessionEnd","session_id":"s_cache_1"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.True(update.RemoveSession);
        }

        // 2. Bounded cache test: process 105 distinct sessions without error
        for (int i = 0; i < 105; i++)
        {
            string sId = $"s_lru_{i}";
            using var doc1 = JsonDocument.Parse($$"""{"hook_event_name":"UserPromptSubmit","session_id":{{JsonSerializer.Serialize(sId)}},"prompt":"P"}""");
            translator.Translate(doc1.RootElement);

            using var doc2 = JsonDocument.Parse($$"""{"hook_event_name":"PreToolUse","session_id":{{JsonSerializer.Serialize(sId)}},"tool_name":"t1"}""");
            var update = translator.Translate(doc2.RootElement);
            Assert.NotNull(update.State);
        }
    }

    [Fact]
    public void ToPayload_RetainsPromptCommentaryAndFinalItems_With20Tools()
    {
        var translator = new CodexTranslator();
        var commFile = CreateTempFile("""{"type":"response_item","payload":{"type":"message","role":"assistant","phase":"commentary","id":"msg_long_turn_1","content":"Planning 25 tool execution steps"}}""");

        // UserPromptSubmit
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s_long_turn","prompt":"Execute 25 steps"}"""))
        {
            translator.Translate(doc.RootElement);
        }

        // Send 25 PreToolUse events (more than 20 tools)
        for (int i = 1; i <= 25; i++)
        {
            var tId = $"call_{i}";
            var path = i == 1 ? commFile : null;
            var json = $$"""{"hook_event_name":"PreToolUse","session_id":"s_long_turn","tool_name":"exec_cmd","tool_use_id":{{JsonSerializer.Serialize(tId)}},"transcript_path":{{JsonSerializer.Serialize(path)}}}""";
            using var doc = JsonDocument.Parse(json);
            translator.Translate(doc.RootElement);
        }

        // Send Stop event
        AgentState finalState;
        using (var doc = JsonDocument.Parse("""{"hook_event_name":"Stop","session_id":"s_long_turn","last_assistant_message":"Executed all 25 tools successfully"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            finalState = update.State;
        }

        // Verify ToPayload()
        var payload = finalState.ToPayload();
        Assert.NotNull(payload.CurrentTurn);
        Assert.Equal("Execute 25 steps", payload.CurrentTurn.Prompt);

        var payloadItems = payload.CurrentTurn.Items;

        // Commentary item present
        var commItem = payloadItems.FirstOrDefault(i => i.Kind == TurnItemKind.Commentary);
        Assert.NotNull(commItem);
        Assert.Equal("Planning 25 tool execution steps", commItem.Content);

        // Exactly 20 tool items (capped from 25)
        var toolItems = payloadItems.Where(i => i.Kind == TurnItemKind.Tool).ToList();
        Assert.Equal(20, toolItems.Count);

        // Final item present
        var finalItem = payloadItems.FirstOrDefault(i => i.Kind == TurnItemKind.Final);
        Assert.NotNull(finalItem);
        Assert.Equal("Executed all 25 tools successfully", finalItem.Content);

        // Total payload items: 1 commentary + 20 tools + 1 final = 22 items
        Assert.Equal(22, payloadItems.Count);
    }
}
