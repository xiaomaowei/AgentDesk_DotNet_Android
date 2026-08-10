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
}
