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
