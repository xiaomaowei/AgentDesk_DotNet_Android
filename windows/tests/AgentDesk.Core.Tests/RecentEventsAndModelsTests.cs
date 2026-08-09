using AgentDesk.Core.Models;
using AgentDesk.Core.State;
using AgentDesk.Core.Translators;
using Xunit;

namespace AgentDesk.Core.Tests;

public class RecentEventsAndModelsTests
{
    [Fact]
    public async Task StateStore_RecentEvents_DeduplicatesAndCapsAt6()
    {
        var store = new StateStore();
        var state = new AgentState { Agent = "codex", SessionId = "s1", Project = "ProjA" };
        await store.UpsertAsync(state);

        for (int i = 1; i <= 8; i++)
        {
            var update = new AgentState
            {
                Agent = "codex",
                SessionId = "s1",
                Project = "ProjA",
                RecentEvents = new List<RecentEvent>
                {
                    new RecentEvent { Kind = "command", Label = $"cmd_{i}" }
                }
            };
            await store.UpsertAsync(update);
        }

        var current = await store.CurrentAsync();
        Assert.NotNull(current);
        Assert.Equal(6, current.RecentEvents.Count);
        Assert.Equal("cmd_3", current.RecentEvents[0].Label);
        Assert.Equal("cmd_8", current.RecentEvents[5].Label);
    }

    [Fact]
    public async Task StateStore_ModelDeduplicationAndEffortUpgrade()
    {
        var store = new StateStore();

        var state1 = new AgentState
        {
            Agent = "codex",
            SessionId = "s1",
            Project = "ProjA",
            Models = new List<string> { "Sol", "Gemini 3.6 Flash" }
        };
        await store.UpsertAsync(state1);

        var state2 = new AgentState
        {
            Agent = "codex",
            SessionId = "s1",
            Project = "ProjA",
            Models = new List<string> { "Sol High", "Claude Sonnet 4.6" }
        };
        await store.UpsertAsync(state2);

        var current = await store.CurrentAsync();
        Assert.NotNull(current);
        Assert.Equal(3, current.Models.Count);
        Assert.Equal("Sol High", current.Models[0]);
        Assert.Equal("Gemini 3.6 Flash", current.Models[1]);
        Assert.Equal("Claude Sonnet 4.6", current.Models[2]);

        var state3 = new AgentState
        {
            Agent = "codex",
            SessionId = "s1",
            Project = "ProjA",
            Models = new List<string> { "Sol" }
        };
        await store.UpsertAsync(state3);

        current = await store.CurrentAsync();
        Assert.NotNull(current);
        Assert.Equal("Sol High", current.Models[0]);
    }

    [Theory]
    [InlineData("gpt-5.6-sol", "high", "Sol High")]
    [InlineData("gpt-5.6-luna", "medium", "Luna Medium")]
    [InlineData("gemini-3.6-flash", null, "Gemini 3.6 Flash")]
    [InlineData("claude-sonnet-4-6", null, "Claude Sonnet 4.6")]
    public void ModelHelper_NormalizeModelSlug(string slug, string? effort, string expected)
    {
        var result = ModelHelper.NormalizeModelSlug(slug, effort);
        Assert.Equal(expected, result);
    }
}
