using System.Text.Json;
using AgentDesk.Core.Models;
using AgentDesk.Core.Protocol;
using AgentDesk.Core.State;
using Xunit;

namespace AgentDesk.Core.Tests;

public class StateStoreUsageTests
{
    [Fact]
    public async Task SetUsageSnapshots_UpdatesExistingAndNewStates()
    {
        var store = new StateStore();

        var state1 = new AgentState
        {
            Agent = "codex",
            SessionId = "s1",
            Project = "ProjA",
            Message = "Test"
        };
        await store.UpsertAsync(state1);

        var codexUsage = new CodexUsagePayload
        {
            WeeklyRemainingPercent = 75,
            ResetText = "Resets 8/16",
            ResetDate = "8/16",
            ResetAvailable = 1
        };

        var antigravityUsage = new AntigravityUsagePayload
        {
            WeeklyRemainingPercent = 90,
            WeeklyRefreshText = "Refreshes in 5d",
            FiveHourRemainingPercent = 80,
            FiveHourRefreshText = "Refreshes in 10m",
            GeminiFiveHourRemainingPercent = 80,
            GeminiFiveHourRefreshText = "Refreshes in 10m",
            ClaudeFiveHourRemainingPercent = null,
            ClaudeFiveHourRefreshText = ""
        };

        store.SetUsageSnapshots(codexUsage, antigravityUsage);

        var current = await store.CurrentAsync();
        Assert.NotNull(current);
        Assert.Equal(75, current.CodexUsage?.WeeklyRemainingPercent);
        Assert.Equal(90, current.AntigravityUsage?.WeeklyRemainingPercent);

        // Verify newly upserted state receives cached usage
        var state2 = new AgentState
        {
            Agent = "codex",
            SessionId = "s2",
            Project = "ProjB",
            Message = "Test2"
        };
        await store.UpsertAsync(state2);

        Assert.Equal(75, state2.CodexUsage?.WeeklyRemainingPercent);
        Assert.Equal(90, state2.AntigravityUsage?.WeeklyRemainingPercent);
    }

    [Fact]
    public void AgentStatePayload_SerializesExactUsageJsonShape()
    {
        var state = new AgentState
        {
            Agent = "codex",
            SessionId = "s1",
            Project = "ProjA",
            Message = "Test",
            CodexUsage = new CodexUsagePayload
            {
                WeeklyRemainingPercent = 74,
                ResetText = "Resets 8/16",
                ResetDate = "8/16",
                ResetAvailable = 1
            },
            AntigravityUsage = new AntigravityUsagePayload
            {
                WeeklyRemainingPercent = 96,
                WeeklyRefreshText = "Refreshes in 141h 33m",
                FiveHourRemainingPercent = 78,
                FiveHourRefreshText = "Refreshes in 4m",
                GeminiFiveHourRemainingPercent = 78,
                GeminiFiveHourRefreshText = "Refreshes in 4m",
                ClaudeFiveHourRemainingPercent = null,
                ClaudeFiveHourRefreshText = ""
            }
        };

        var envelope = state.ToEnvelope();
        string json = JsonSerializer.Serialize(envelope, ProtocolSerializerOptions.Default);

        using var doc = JsonDocument.Parse(json);
        var payload = doc.RootElement.GetProperty("payload");

        Assert.True(payload.TryGetProperty("codex_usage", out var codexEl));
        Assert.Equal(74, codexEl.GetProperty("weekly_remaining_percent").GetInt32());
        Assert.Equal("Resets 8/16", codexEl.GetProperty("reset_text").GetString());
        Assert.Equal("8/16", codexEl.GetProperty("reset_date").GetString());
        Assert.Equal(1, codexEl.GetProperty("reset_available").GetInt32());

        Assert.True(payload.TryGetProperty("antigravity_usage", out var agEl));
        Assert.Equal(96, agEl.GetProperty("weekly_remaining_percent").GetInt32());
        Assert.Equal("Refreshes in 141h 33m", agEl.GetProperty("weekly_refresh_text").GetString());
        Assert.Equal(78, agEl.GetProperty("five_hour_remaining_percent").GetInt32());
        Assert.Equal("Refreshes in 4m", agEl.GetProperty("five_hour_refresh_text").GetString());
        Assert.Equal(78, agEl.GetProperty("gemini_five_hour_remaining_percent").GetInt32());
        Assert.Equal("Refreshes in 4m", agEl.GetProperty("gemini_five_hour_refresh_text").GetString());
        Assert.Equal(JsonValueKind.Null, agEl.GetProperty("claude_five_hour_remaining_percent").ValueKind);
        Assert.Equal("", agEl.GetProperty("claude_five_hour_refresh_text").GetString());
    }
}
