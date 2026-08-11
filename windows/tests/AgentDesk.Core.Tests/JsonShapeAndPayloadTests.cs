using System.Text.Json;
using AgentDesk.Core.Models;
using AgentDesk.Core.Protocol;
using Xunit;

namespace AgentDesk.Core.Tests;

public class JsonShapeAndPayloadTests
{
    [Fact]
    public void AgentState_ToPayload_SerializesRequiredAndOmitsNullOptionalFields()
    {
        var state = new AgentState
        {
            Agent = "codex",
            Project = "TestProject",
            Status = AgentStatus.Working,
            Message = "Test message",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            RequiresAction = false,
            Actions = new List<string>(),
            TargetId = null
        };

        var payload = state.ToPayload();
        var json = JsonSerializer.Serialize(payload, ProtocolSerializerOptions.Default);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("codex", root.GetProperty("agent").GetString());
        Assert.Equal("TestProject", root.GetProperty("project").GetString());
        Assert.Equal("working", root.GetProperty("status").GetString());
        Assert.Equal("Test message", root.GetProperty("message").GetString());
        Assert.True(root.GetProperty("elapsed").GetInt64() >= 300);
        Assert.False(root.GetProperty("requires_action").GetBoolean());
        Assert.Equal(0, root.GetProperty("actions").GetArrayLength());

        Assert.True(root.TryGetProperty("target_id", out var targetIdProp));
        Assert.Equal(JsonValueKind.Null, targetIdProp.ValueKind);

        Assert.False(root.TryGetProperty("conversation_name", out _));
        Assert.False(root.TryGetProperty("steps", out _));
        Assert.False(root.TryGetProperty("recent_events", out _));
        Assert.False(root.TryGetProperty("models", out _));
        Assert.False(root.TryGetProperty("current_turn", out _));
    }

    [Fact]
    public void AgentState_ToPayload_ElapsedIsNonNegativeForFutureStartedAt()
    {
        var state = new AgentState
        {
            Agent = "codex",
            Project = "TestProject",
            Status = AgentStatus.Working,
            Message = "Test message",
            StartedAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        var payload = state.ToPayload();
        Assert.Equal(0, payload.Elapsed);
    }

    [Fact]
    public void AgentState_ToPayload_IncludesOptionalFieldsWhenPresent()
    {
        var state = new AgentState
        {
            Agent = "codex",
            Project = "TestProject",
            Status = AgentStatus.Working,
            Message = "Test message",
            ConversationName = "Conv123",
            ConversationTokens = 1500,
            Steps = new List<string> { "Step 1", "Step 2" },
            CurrentStep = 1,
            RecentEvents = new List<RecentEvent> { new RecentEvent { Kind = "command", Label = "git status" } },
            Models = new List<string> { "Sol High" },
            CurrentTurn = new CurrentTurn
            {
                Id = "turn_123",
                StartedAt = "2026-08-09T12:00:00Z",
                Prompt = "Test prompt"
            }
        };

        var payload = state.ToPayload();
        var json = JsonSerializer.Serialize(payload, ProtocolSerializerOptions.Default);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Conv123", root.GetProperty("conversation_name").GetString());
        Assert.Equal(1500, root.GetProperty("conversation_tokens").GetInt64());
        Assert.Equal(2, root.GetProperty("steps").GetArrayLength());
        Assert.Equal(1, root.GetProperty("current_step").GetInt32());
        Assert.Equal(1, root.GetProperty("recent_events").GetArrayLength());
        Assert.Equal(1, root.GetProperty("models").GetArrayLength());
        Assert.Equal("turn_123", root.GetProperty("current_turn").GetProperty("id").GetString());
    }

    [Fact]
    public void DashboardSnapshot_SerializesCurrentAndProjectsAsStateEnvelopes()
    {
        var state = new AgentState
        {
            Agent = "codex",
            Project = "TestProject",
            Status = AgentStatus.Working,
            Message = "Test message",
            StartedAt = DateTimeOffset.UtcNow
        };

        var snapshot = new DashboardSnapshot
        {
            Version = "1.0",
            Current = state.ToEnvelope(),
            Projects = new List<ProtocolEnvelope<AgentStatePayload>> { state.ToEnvelope() }
        };

        var json = JsonSerializer.Serialize(snapshot, ProtocolSerializerOptions.Default);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("1.0", root.GetProperty("version").GetString());

        var current = root.GetProperty("current");
        Assert.Equal("1.0", current.GetProperty("version").GetString());
        Assert.Equal("state", current.GetProperty("type").GetString());
        Assert.Equal(state.Id, current.GetProperty("id").GetString());
        Assert.NotNull(current.GetProperty("timestamp").GetString());

        var payload = current.GetProperty("payload");
        Assert.Equal("codex", payload.GetProperty("agent").GetString());
        Assert.Equal("TestProject", payload.GetProperty("project").GetString());

        var projects = root.GetProperty("projects");
        Assert.Equal(1, projects.GetArrayLength());
        Assert.Equal("1.0", projects[0].GetProperty("version").GetString());
        Assert.Equal("state", projects[0].GetProperty("type").GetString());
        Assert.Equal("TestProject", projects[0].GetProperty("payload").GetProperty("project").GetString());
    }
}
