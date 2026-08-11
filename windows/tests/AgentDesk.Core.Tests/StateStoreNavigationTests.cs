using AgentDesk.Core.Models;
using AgentDesk.Core.State;
using Xunit;

namespace AgentDesk.Core.Tests;

public class StateStoreNavigationTests
{
    [Fact]
    public async Task StateStore_ProjectNavigation_CyclesProjectsCorrectly()
    {
        var store = new StateStore();

        var state1 = new AgentState { Agent = "codex", SessionId = "s1", Project = "ProjA" };
        var state2 = new AgentState { Agent = "codex", SessionId = "s2", Project = "ProjB" };
        var state3 = new AgentState { Agent = "codex", SessionId = "s3", Project = "ProjC" };

        await store.UpsertAsync(state1);
        await store.UpsertAsync(state2);
        await store.UpsertAsync(state3);

        var current = await store.CurrentAsync();
        Assert.NotNull(current);
        Assert.Equal("ProjC", current.Project);

        var next1 = await store.NextProjectAsync();
        Assert.NotNull(next1);
        Assert.Equal("ProjA", next1.Project);

        var next2 = await store.NextProjectAsync();
        Assert.NotNull(next2);
        Assert.Equal("ProjB", next2.Project);

        var prev1 = await store.PreviousProjectAsync();
        Assert.NotNull(prev1);
        Assert.Equal("ProjA", prev1.Project);
    }

    [Fact]
    public async Task StateStore_SelectProject_SelectsTargetProjectByIdOnly()
    {
        var store = new StateStore();

        var state1 = new AgentState { Agent = "codex", SessionId = "s1", Project = "ProjA" };
        var state2 = new AgentState { Agent = "codex", SessionId = "s2", Project = "ProjB" };

        await store.UpsertAsync(state1);
        await store.UpsertAsync(state2);

        // Selecting using state.Id succeeds
        var selected = await store.SelectProjectAsync(state1.Id);
        Assert.NotNull(selected);
        Assert.Equal("ProjA", selected.Project);

        var current = await store.CurrentAsync();
        Assert.Equal("ProjA", current?.Project);

        // Selecting using project display name string returns null
        var byProjectName = await store.SelectProjectAsync("ProjA");
        Assert.Null(byProjectName);
    }

    [Fact]
    public async Task StateStore_RemoveAsync_WithSameKeyAndUnknownProject_ClearsStateAndProjects()
    {
        var store = new StateStore();

        var state1 = new AgentState { Agent = "codex", SessionId = "s1", Project = "ProjA" };
        await store.UpsertAsync(state1);

        var stateToRemove = new AgentState { Agent = "codex", SessionId = "s1", Project = "Unknown project" };
        await store.RemoveAsync(stateToRemove);

        var current = await store.CurrentAsync();
        Assert.Null(current);

        var projectStates = await store.ProjectStatesAsync();
        Assert.Empty(projectStates);

        var nextProject = await store.NextProjectAsync();
        Assert.Null(nextProject);
    }

    [Fact]
    public async Task StateStore_ClearCurrent_ClearsNotificationWhenNoActionRequired()
    {
        var store = new StateStore();

        var state = new AgentState
        {
            Agent = "codex",
            SessionId = "s1",
            Project = "ProjA",
            Status = AgentStatus.Completed,
            Message = "Task finished",
            RequiresAction = false
        };

        await store.UpsertAsync(state);
        var cleared = await store.ClearCurrentAsync();

        Assert.NotNull(cleared);
        Assert.Equal(AgentStatus.Idle, cleared.Status);
        Assert.Equal("Notification cleared", cleared.Message);
    }

    [Fact]
    public async Task StateStore_ClearCurrent_DoesNotClearWhenRequiresActionIsTrue()
    {
        var store = new StateStore();

        var state = new AgentState
        {
            Agent = "codex",
            SessionId = "s1",
            Project = "ProjA",
            Status = AgentStatus.Waiting,
            Message = "Waiting for approval",
            RequiresAction = true
        };

        await store.UpsertAsync(state);
        var cleared = await store.ClearCurrentAsync();

        Assert.NotNull(cleared);
        Assert.Equal(AgentStatus.Waiting, cleared.Status);
        Assert.Equal("Waiting for approval", cleared.Message);
    }
}
