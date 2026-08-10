using AgentDesk.Core.Models;
using AgentDesk.Core.State;
using AgentDesk.Server.Usage;
using Xunit;

namespace AgentDesk.Server.Tests;

public class UsageServiceTests
{
    private class TestCodexUsageReader : ICodexUsageReader
    {
        public Func<Task<CodexUsagePayload?>> FetchFunc { get; set; } = () => Task.FromResult<CodexUsagePayload?>(null);
        public int FetchCount { get; private set; }

        public Task<CodexUsagePayload?> FetchAsync(CancellationToken cancellationToken = default)
        {
            FetchCount++;
            return FetchFunc();
        }
    }

    private class TestAntigravityUsageReader : IAntigravityUsageReader
    {
        public Func<Task<AntigravityUsagePayload?>> FetchFunc { get; set; } = () => Task.FromResult<AntigravityUsagePayload?>(null);
        public int FetchCount { get; private set; }

        public Task<AntigravityUsagePayload?> FetchAsync(CancellationToken cancellationToken = default)
        {
            FetchCount++;
            return FetchFunc();
        }
    }

    [Fact]
    public async Task Refresh_CachesSuccessfulResult_For5Minutes()
    {
        var codexReader = new TestCodexUsageReader
        {
            FetchFunc = () => Task.FromResult<CodexUsagePayload?>(new CodexUsagePayload
            {
                WeeklyRemainingPercent = 80,
                ResetText = "Resets 8/16",
                ResetDate = "8/16",
                ResetAvailable = 1
            })
        };
        var agReader = new TestAntigravityUsageReader();
        var store = new StateStore();
        var broadcaster = new SseBroadcaster();

        var service = new UsageService(codexReader, agReader, store, broadcaster);
        var t0 = DateTimeOffset.UtcNow;

        await service.RefreshAsync(t0);
        Assert.Equal(1, codexReader.FetchCount);
        Assert.Equal(80, service.CurrentCodexUsage?.WeeklyRemainingPercent);

        // Fetch within 5 minutes -> cache hit
        await service.RefreshAsync(t0.AddMinutes(4));
        Assert.Equal(1, codexReader.FetchCount);

        // Fetch after 5 minutes -> re-fetches
        await service.RefreshAsync(t0.AddMinutes(5.1));
        Assert.Equal(2, codexReader.FetchCount);
    }

    [Fact]
    public async Task Refresh_FailedFetch_RetriesAfter30Seconds_AndRetainsLastSnapshot()
    {
        var codexPayload = new CodexUsagePayload
        {
            WeeklyRemainingPercent = 80,
            ResetText = "Resets 8/16",
            ResetDate = "8/16",
            ResetAvailable = 1
        };

        int calls = 0;
        var codexReader = new TestCodexUsageReader
        {
            FetchFunc = () =>
            {
                calls++;
                return Task.FromResult<CodexUsagePayload?>(calls == 1 ? codexPayload : null);
            }
        };
        var agReader = new TestAntigravityUsageReader();
        var store = new StateStore();
        var broadcaster = new SseBroadcaster();

        var service = new UsageService(codexReader, agReader, store, broadcaster);
        var t0 = DateTimeOffset.UtcNow;

        // First call succeeds
        await service.RefreshAsync(t0);
        Assert.Equal(1, codexReader.FetchCount);
        Assert.Equal(80, service.CurrentCodexUsage?.WeeklyRemainingPercent);

        // Expire 5 min cache to force refetch
        var t1 = t0.AddMinutes(6);
        await service.RefreshAsync(t1); // calls == 2 -> returns null
        Assert.Equal(2, codexReader.FetchCount);
        Assert.Equal(80, service.CurrentCodexUsage?.WeeklyRemainingPercent); // Last good snapshot retained!

        // Next call within 30s backoff window -> does not refetch
        await service.RefreshAsync(t1.AddSeconds(20));
        Assert.Equal(2, codexReader.FetchCount);

        // After 30s backoff -> refetches
        await service.RefreshAsync(t1.AddSeconds(31));
        Assert.Equal(3, codexReader.FetchCount);
        Assert.Equal(80, service.CurrentCodexUsage?.WeeklyRemainingPercent); // Retained
    }

    [Fact]
    public async Task RefreshCodex_PropagatesPromptlyWithoutWaitingForAntigravity()
    {
        var codexPayload = new CodexUsagePayload
        {
            WeeklyRemainingPercent = 85,
            ResetText = "Resets 8/17",
            ResetDate = "8/17",
            ResetAvailable = 2
        };

        var codexReader = new TestCodexUsageReader
        {
            FetchFunc = () => Task.FromResult<CodexUsagePayload?>(codexPayload)
        };

        var agTcs = new TaskCompletionSource<AntigravityUsagePayload?>();
        var agReader = new TestAntigravityUsageReader
        {
            FetchFunc = () => agTcs.Task
        };

        var store = new StateStore();
        var broadcaster = new SseBroadcaster();
        var service = new UsageService(codexReader, agReader, store, broadcaster);

        var t0 = DateTimeOffset.UtcNow;
        var refreshTask = service.RefreshAsync(t0);

        // Wait a short delay to allow Codex task to complete while Antigravity is still pending
        await Task.Delay(100);

        // Assert Codex usage has already propagated to Service and StateStore without waiting for Antigravity
        Assert.Equal(85, service.CurrentCodexUsage?.WeeklyRemainingPercent);

        // Now complete Antigravity task
        agTcs.SetResult(new AntigravityUsagePayload
        {
            WeeklyRemainingPercent = 95,
            WeeklyRefreshText = "Refreshes in 6d"
        });

        await refreshTask;

        Assert.Equal(85, service.CurrentCodexUsage?.WeeklyRemainingPercent);
        Assert.Equal(95, service.CurrentAntigravityUsage?.WeeklyRemainingPercent);
    }
}
