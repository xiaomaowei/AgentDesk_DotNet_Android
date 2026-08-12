using System.Text.Json;
using AgentDesk.Core.Models;
using AgentDesk.Core.Translators;
using Xunit;

namespace AgentDesk.Core.Tests;

public class CodexTranslatorLifecycleTests
{
    [Fact]
    public void Translate_UserPromptSubmit_StartsCurrentTurn()
    {
        var translator = new CodexTranslator();
        var json = """
        {
          "hook_event_name": "UserPromptSubmit",
          "session_id": "sess_01",
          "cwd": "C:\\prog\\MyProject",
          "prompt": "Build feature X"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var update = translator.Translate(doc.RootElement);

        Assert.Equal("codex", update.State.Agent);
        Assert.Equal("MyProject", update.State.Project);
        Assert.Equal(AgentStatus.Working, update.State.Status);
        Assert.False(update.WaitsForAction);
        Assert.False(update.RemoveSession);

        Assert.NotNull(update.State.CurrentTurn);
        Assert.Equal("Build feature X", update.State.CurrentTurn.Prompt);
    }

    [Theory]
    [InlineData("conversation_name")]
    [InlineData("conversation_title")]
    [InlineData("thread_name")]
    public void Translate_ConversationName_AcceptsSupportedAliases(string propertyName)
    {
        var translator = CreateTranslatorWithoutSessionIndex();
        using var doc = JsonDocument.Parse($$"""{"hook_event_name":"UserPromptSubmit","session_id":"s1","{{propertyName}}":"  Conversation title  "}""");

        var update = translator.Translate(doc.RootElement);

        Assert.Equal("Conversation title", update.State.ConversationName);
    }

    [Fact]
    public void Translate_ConversationName_FallsBackPastBlankCandidates()
    {
        var translator = CreateTranslatorWithoutSessionIndex();
        using var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s1","conversation_name":" ","conversation_title":"\t","thread_name":"  Fallback title  "}""");

        var update = translator.Translate(doc.RootElement);

        Assert.Equal("Fallback title", update.State.ConversationName);
    }

    [Fact]
    public void Translate_ConversationName_TrimsAndLimitsTo96Characters()
    {
        var title = new string('a', 100);
        var translator = CreateTranslatorWithoutSessionIndex();
        using var doc = JsonDocument.Parse($$"""{"hook_event_name":"UserPromptSubmit","session_id":"s1","conversation_name":"  {{title}}  "}""");

        var update = translator.Translate(doc.RootElement);

        Assert.Equal(new string('a', 96), update.State.ConversationName);
    }

    [Fact]
    public void Translate_ConversationName_PrefersDirectFieldsOverOtherSources()
    {
        var indexPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(indexPath, """{"id":"s1","thread_name":"Indexed title"}""");
            var translator = new CodexTranslator(indexPath);
            using var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s1","conversation_name":"Legacy","thread_name":"Thread","conversation_title":"Direct title","prompt":"Prompt title"}""");

            var update = translator.Translate(doc.RootElement);

            Assert.Equal("Direct title", update.State.ConversationName);
        }
        finally
        {
            File.Delete(indexPath);
        }
    }

    [Fact]
    public void Translate_ConversationName_UsesNewestMatchingSessionIndexRecord()
    {
        var indexPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(indexPath, """
            {"id":"s1","thread_name":"Older title"}
            {"session_id":"s1","title":"Newest title"}
            """);
            var translator = new CodexTranslator(indexPath);
            using var doc = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","session_id":"s1","tool_name":"exec"}""");

            var update = translator.Translate(doc.RootElement);

            Assert.Equal("Newest title", update.State.ConversationName);
        }
        finally
        {
            File.Delete(indexPath);
        }
    }

    [Theory]
    [InlineData("thread_name", "Thread alias")]
    [InlineData("title", "Title alias")]
    public void Translate_ConversationName_AcceptsSessionIndexTitleAliases(string propertyName, string expectedName)
    {
        var indexPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(indexPath, $$"""{"id":"s1","{{propertyName}}":"{{expectedName}}"}""");
            var translator = new CodexTranslator(indexPath);
            using var doc = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","session_id":"s1","tool_name":"exec"}""");

            var update = translator.Translate(doc.RootElement);

            Assert.Equal(expectedName, update.State.ConversationName);
        }
        finally
        {
            File.Delete(indexPath);
        }
    }

    [Fact]
    public void Translate_ConversationName_SkipsInvalidAndUnmatchedSessionIndexRecords()
    {
        var indexPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(indexPath, """
            not json
            {"id":"other","thread_name":"Wrong session"}
            {"session_id":"s1","title":"Matched title"}
            """);
            var translator = new CodexTranslator(indexPath);
            using var doc = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","session_id":"s1","tool_name":"exec"}""");

            var update = translator.Translate(doc.RootElement);

            Assert.Equal("Matched title", update.State.ConversationName);
        }
        finally
        {
            File.Delete(indexPath);
        }
    }

    [Fact]
    public void Translate_ConversationName_PrefersSessionIndexOverPromptFallback()
    {
        var indexPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(indexPath, """{"id":"s1","thread_name":"Indexed title"}""");
            var translator = new CodexTranslator(indexPath);
            using var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s1","prompt":"Prompt title"}""");

            var update = translator.Translate(doc.RootElement);

            Assert.Equal("Indexed title", update.State.ConversationName);
        }
        finally
        {
            File.Delete(indexPath);
        }
    }

    [Fact]
    public void Translate_ConversationName_UsesPromptFallbackAfterAttachmentMetadata()
    {
        var translator = CreateTranslatorWithoutSessionIndex();
        using var doc = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit","session_id":"s1","prompt":"# Files mentioned by the user:\n\n## photo.jpg: C:/Temp/photo.jpg\n\n## My request for Codex:\n\nActual request <image name=[Image #1] path=\"C:/Temp/photo.jpg\">"}""");

        var update = translator.Translate(doc.RootElement);

        Assert.Equal("Actual request", update.State.ConversationName);
    }

    [Fact]
    public void Translate_ConversationName_DoesNotUsePromptFallbackForOtherEvents()
    {
        var translator = CreateTranslatorWithoutSessionIndex();
        using var doc = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","session_id":"s1","prompt":"Prompt title","tool_name":"exec"}""");

        var update = translator.Translate(doc.RootElement);

        Assert.Null(update.State.ConversationName);
    }

    private static CodexTranslator CreateTranslatorWithoutSessionIndex()
    {
        return new CodexTranslator(Path.Combine(Path.GetTempPath(), $"missing-session-index-{Guid.NewGuid():n}.jsonl"));
    }

    [Fact]
    public void Translate_ToolAndApprovalLifecycle_TracksItemsAndSharedIds()
    {
        var translator = new CodexTranslator();

        // 1. UserPromptSubmit
        using (var doc = JsonDocument.Parse("""{"hook_event_name": "UserPromptSubmit", "session_id": "s1", "cwd": "/app/AgentDesk", "prompt": "Fix bug"}"""))
        {
            translator.Translate(doc.RootElement);
        }

        // 2. PermissionRequest
        using (var doc = JsonDocument.Parse("""{"hook_event_name": "PermissionRequest", "session_id": "s1", "tool_name": "exec_command", "tool_use_id": "call_100"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.True(update.WaitsForAction);
            Assert.Equal(AgentStatus.Waiting, update.State.Status);
            Assert.NotNull(update.State.TargetId);
        }

        // 3. PreToolUse
        using (var doc = JsonDocument.Parse("""{"hook_event_name": "PreToolUse", "session_id": "s1", "tool_name": "exec_command", "tool_use_id": "call_100"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(AgentStatus.Working, update.State.Status);
            var item = update.State.CurrentTurn?.Items.FirstOrDefault(i => i.Kind == TurnItemKind.Tool && i.Id.EndsWith("call_100"));
            Assert.NotNull(item);
            Assert.Equal(TurnItemPhase.Running, item.Phase);
        }

        // 4. PostToolUse
        using (var doc = JsonDocument.Parse("""{"hook_event_name": "PostToolUse", "session_id": "s1", "tool_name": "exec_command", "tool_use_id": "call_100"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            var item = update.State.CurrentTurn?.Items.FirstOrDefault(i => i.Kind == TurnItemKind.Tool && i.Id.EndsWith("call_100"));
            Assert.NotNull(item);
            Assert.Equal(TurnItemPhase.Completed, item.Phase);
        }

        // 5. Stop
        using (var doc = JsonDocument.Parse("""{"hook_event_name": "Stop", "session_id": "s1", "last_assistant_message": "All done!"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(AgentStatus.Completed, update.State.Status);
            var finalItem = update.State.CurrentTurn?.Items.FirstOrDefault(i => i.Kind == TurnItemKind.Final);
            Assert.NotNull(finalItem);
            Assert.Equal("All done!", finalItem.Content);
        }
    }

    [Fact]
    public void Translate_UnknownEvent_DoesNotCrash()
    {
        var translator = new CodexTranslator();
        using var doc = JsonDocument.Parse("""{"hook_event_name": "SomeRandomUnknownEvent", "session_id": "s1"}""");

        var update = translator.Translate(doc.RootElement);
        Assert.NotNull(update.State);
        Assert.Equal("s1", update.State.SessionId);
    }

    [Fact]
    public void Translate_ModelEffort_ExtractsReasoningEffort()
    {
        var translator = new CodexTranslator();
        using var doc = JsonDocument.Parse("""
        {
            "hook_event_name": "UserPromptSubmit",
            "session_id": "s_re_1",
            "model": "gpt-5.6-sol",
            "reasoning_effort": "high",
            "prompt": "Hello"
        }
        """);

        var update = translator.Translate(doc.RootElement);
        Assert.Contains("Sol High", update.State.Models);
    }

    [Fact]
    public void Translate_ModelEffort_PrefersEffortOverReasoningEffort()
    {
        var translator = new CodexTranslator();
        using var doc = JsonDocument.Parse("""
        {
            "hook_event_name": "UserPromptSubmit",
            "session_id": "s_re_2",
            "model": "gpt-5.6-sol",
            "effort": "low",
            "reasoning_effort": "high",
            "prompt": "Hello"
        }
        """);

        var update = translator.Translate(doc.RootElement);
        Assert.Contains("Sol Low", update.State.Models);
        Assert.DoesNotContain("Sol High", update.State.Models);
    }

    [Fact]
    public void Translate_ModelEffort_LunaAndTerraWithReasoningEffort()
    {
        var translator = new CodexTranslator();
        using var docLuna = JsonDocument.Parse("""
        {
            "hook_event_name": "UserPromptSubmit",
            "session_id": "s_re_luna",
            "model": "gpt-5.6-luna",
            "reasoning_effort": "medium",
            "prompt": "Hello"
        }
        """);
        var updateLuna = translator.Translate(docLuna.RootElement);
        Assert.Contains("Luna Medium", updateLuna.State.Models);

        using var docTerra = JsonDocument.Parse("""
        {
            "hook_event_name": "UserPromptSubmit",
            "session_id": "s_re_terra",
            "model": "gpt-5.6-terra",
            "reasoning_effort": "low",
            "prompt": "Hello"
        }
        """);
        var updateTerra = translator.Translate(docTerra.RootElement);
        Assert.Contains("Terra Low", updateTerra.State.Models);
    }

    [Fact]
    public void Translate_UpdatePlan_InitialFourStepPlan_ProgressUpdate_AllCompleted_Retention_And_NewTurnReset()
    {
        var translator = new CodexTranslator();
        const string sessionId = "sess_plan_01";

        // 1. Initial UserPromptSubmit
        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"UserPromptSubmit","session_id":"{{sessionId}}","prompt":"Do task"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Null(update.State.Steps);
            Assert.Null(update.State.CurrentStep);
        }

        // 2. Initial four-step plan (Step 1 completed, Step 2 in_progress, Step 3 pending, Step 4 pending)
        using (var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "PostToolUse",
            "session_id": "{{sessionId}}",
            "tool_name": "update_plan",
            "tool_input": {
                "plan": [
                    { "step": "  1. Initial research  ", "status": "completed" },
                    { "step": "2. Implement changes", "status": "in_progress" },
                    { "step": "3. Add unit tests", "status": "pending" },
                    { "step": "4. Verify build", "status": "pending" }
                ]
            }
        }
        """))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.NotNull(update.State.Steps);
            Assert.Equal(4, update.State.Steps.Count);
            Assert.Equal("1. Initial research", update.State.Steps[0]);
            Assert.Equal("2. Implement changes", update.State.Steps[1]);
            Assert.Equal("3. Add unit tests", update.State.Steps[2]);
            Assert.Equal("4. Verify build", update.State.Steps[3]);
            Assert.Equal(2, update.State.CurrentStep);
        }

        // 3. Progress update (Step 2 completed, Step 3 in_progress)
        using (var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "PostToolUse",
            "session_id": "{{sessionId}}",
            "tool_name": "update_plan",
            "tool_input": {
                "plan": [
                    { "step": "1. Initial research", "status": "completed" },
                    { "step": "2. Implement changes", "status": "completed" },
                    { "step": "3. Add unit tests", "status": "in_progress" },
                    { "step": "4. Verify build", "status": "pending" }
                ]
            }
        }
        """))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(4, update.State.Steps?.Count);
            Assert.Equal(3, update.State.CurrentStep);
        }

        // 4. All-completed state (All 4 steps completed)
        using (var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "PostToolUse",
            "session_id": "{{sessionId}}",
            "tool_name": "update_plan",
            "tool_input": {
                "plan": [
                    { "step": "1. Initial research", "status": "completed" },
                    { "step": "2. Implement changes", "status": "completed" },
                    { "step": "3. Add unit tests", "status": "completed" },
                    { "step": "4. Verify build", "status": "completed" }
                ]
            }
        }
        """))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(4, update.State.Steps?.Count);
            Assert.Equal(4, update.State.CurrentStep);
        }

        // 5. Retention on later non-plan PreToolUse event
        using (var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "PreToolUse",
            "session_id": "{{sessionId}}",
            "tool_name": "exec_command",
            "tool_input": { "command": "git status" }
        }
        """))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.NotNull(update.State.Steps);
            Assert.Equal(4, update.State.Steps.Count);
            Assert.Equal(4, update.State.CurrentStep);
        }

        // 6. Retention on Stop event
        using (var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "Stop",
            "session_id": "{{sessionId}}",
            "last_assistant_message": "All done"
        }
        """))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.NotNull(update.State.Steps);
            Assert.Equal(4, update.State.Steps.Count);
            Assert.Equal(4, update.State.CurrentStep);
        }

        // 7. Reset on new turn (UserPromptSubmit)
        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"UserPromptSubmit","session_id":"{{sessionId}}","prompt":"New prompt"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Null(update.State.Steps);
            Assert.Null(update.State.CurrentStep);
        }
    }

    [Fact]
    public void Translate_UpdatePlan_AcceptsTitleAndNameAliases_AndStringifiedToolInput()
    {
        var translator = new CodexTranslator();
        const string sessionId = "sess_plan_aliases";

        using var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "PreToolUse",
            "session_id": "{{sessionId}}",
            "tool_name": "update_plan",
            "tool_input": "{\"plan\": [{\"title\": \"Step A\", \"status\": \"pending\"}, {\"name\": \"Step B\", \"status\": \"pending\"}]}"
        }
        """);

        var update = translator.Translate(doc.RootElement);
        Assert.NotNull(update.State.Steps);
        Assert.Equal(2, update.State.Steps.Count);
        Assert.Equal("Step A", update.State.Steps[0]);
        Assert.Equal("Step B", update.State.Steps[1]);
        Assert.Equal(1, update.State.CurrentStep);
    }

    [Fact]
    public void Translate_UpdatePlan_MalformedInput_DoesNotCrashOrCorruptState()
    {
        var translator = new CodexTranslator();
        const string sessionId = "sess_malformed";

        // 1. Establish valid plan
        using (var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "PostToolUse",
            "session_id": "{{sessionId}}",
            "tool_input": {
                "plan": [
                    { "step": "Valid step", "status": "in_progress" }
                ]
            }
        }
        """))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Equal(1, update.State.Steps?.Count);
            Assert.Equal(1, update.State.CurrentStep);
        }

        // 2. Send malformed plan (empty array / objects with blank names)
        using (var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "PostToolUse",
            "session_id": "{{sessionId}}",
            "tool_input": {
                "plan": [
                    { "step": "   ", "status": "pending" }
                ]
            }
        }
        """))
        {
            var update = translator.Translate(doc.RootElement);
            // Should retain previous valid plan
            Assert.Equal(1, update.State.Steps?.Count);
            Assert.Equal("Valid step", update.State.Steps?[0]);
        }
    }

    [Fact]
    public void Translate_SessionEnd_ClearsPlanSnapshot()
    {
        var translator = new CodexTranslator();
        const string sessionId = "sess_cleanup";

        // 1. Establish valid plan
        using (var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "PostToolUse",
            "session_id": "{{sessionId}}",
            "tool_input": {
                "plan": [ { "step": "Step 1", "status": "completed" } ]
            }
        }
        """))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.NotNull(update.State.Steps);
        }

        // 2. SessionEnd event
        using (var doc = JsonDocument.Parse($$"""{"hook_event_name":"SessionEnd","session_id":"{{sessionId}}"}"""))
        {
            var update = translator.Translate(doc.RootElement);
            Assert.Null(update.State.Steps);
            Assert.Null(update.State.CurrentStep);
            Assert.True(update.RemoveSession);
        }
    }

    [Fact]
    public void Translate_UpdatePlan_TrimsAndLimitsLengthAndCount()
    {
        var translator = new CodexTranslator();
        const string sessionId = "sess_limits";

        var longName = new string('x', 300);
        var planItemsJson = string.Join(",", Enumerable.Range(1, 25).Select(i => $$"""{"step": "Step {{i}} {{longName}}", "status": "pending"}"""));

        using var doc = JsonDocument.Parse($$"""
        {
            "hook_event_name": "PostToolUse",
            "session_id": "{{sessionId}}",
            "tool_input": {
                "plan": [ {{planItemsJson}} ]
            }
        }
        """);

        var update = translator.Translate(doc.RootElement);
        Assert.NotNull(update.State.Steps);
        Assert.Equal(20, update.State.Steps.Count);
        Assert.Equal(200, update.State.Steps[0].Length);
        Assert.Equal(1, update.State.CurrentStep);
    }
}
