using AgentDesk.Core.Models;
using AgentDesk.Core.State;
using Microsoft.Extensions.Hosting;

namespace AgentDesk.Server.Usage;

public class UsageService
{
    private readonly ICodexUsageReader _codexReader;
    private readonly IAntigravityUsageReader _antigravityReader;
    private readonly StateStore _stateStore;
    private readonly SseBroadcaster _broadcaster;

    private readonly object _lock = new();
    private CodexUsagePayload? _lastCodexSuccess;
    private DateTimeOffset? _lastCodexFetchedAt;
    private DateTimeOffset _codexNextRetryAt = DateTimeOffset.MinValue;

    private AntigravityUsagePayload? _lastAntigravitySuccess;
    private DateTimeOffset? _lastAntigravityFetchedAt;
    private DateTimeOffset _antigravityNextRetryAt = DateTimeOffset.MinValue;

    public CodexUsagePayload? CurrentCodexUsage
    {
        get { lock (_lock) return _lastCodexSuccess; }
    }

    public AntigravityUsagePayload? CurrentAntigravityUsage
    {
        get { lock (_lock) return _lastAntigravitySuccess; }
    }

    public UsageService(
        ICodexUsageReader codexReader,
        IAntigravityUsageReader antigravityReader,
        StateStore stateStore,
        SseBroadcaster broadcaster)
    {
        _codexReader = codexReader;
        _antigravityReader = antigravityReader;
        _stateStore = stateStore;
        _broadcaster = broadcaster;
    }

    public async Task RefreshAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var codexTask = RefreshCodexAsync(now, cancellationToken);
        var antigravityTask = RefreshAntigravityAsync(now, cancellationToken);

        await Task.WhenAll(codexTask, antigravityTask).ConfigureAwait(false);
    }

    public async Task<bool> RefreshCodexAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_lastCodexFetchedAt.HasValue && now - _lastCodexFetchedAt.Value < TimeSpan.FromMinutes(5))
            {
                return false;
            }
            if (now < _codexNextRetryAt)
            {
                return false;
            }
        }

        CodexUsagePayload? result = null;
        try
        {
            result = await _codexReader.FetchAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            result = null;
        }

        bool changed = false;
        lock (_lock)
        {
            if (result != null)
            {
                changed = !Equals(_lastCodexSuccess, result);
                _lastCodexSuccess = result;
                _lastCodexFetchedAt = now;
                _codexNextRetryAt = DateTimeOffset.MinValue;
            }
            else
            {
                _codexNextRetryAt = now.AddSeconds(30);
            }
        }

        if (changed)
        {
            await ApplyUsageAndBroadcastAsync().ConfigureAwait(false);
        }

        return changed;
    }

    public async Task<bool> RefreshAntigravityAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_lastAntigravityFetchedAt.HasValue && now - _lastAntigravityFetchedAt.Value < TimeSpan.FromMinutes(5))
            {
                return false;
            }
            if (now < _antigravityNextRetryAt)
            {
                return false;
            }
        }

        AntigravityUsagePayload? result = null;
        try
        {
            result = await _antigravityReader.FetchAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            result = null;
        }

        bool changed = false;
        lock (_lock)
        {
            if (result != null)
            {
                changed = !Equals(_lastAntigravitySuccess, result);
                _lastAntigravitySuccess = result;
                _lastAntigravityFetchedAt = now;
                _antigravityNextRetryAt = DateTimeOffset.MinValue;
            }
            else
            {
                _antigravityNextRetryAt = now.AddSeconds(30);
            }
        }

        if (changed)
        {
            await ApplyUsageAndBroadcastAsync().ConfigureAwait(false);
        }

        return changed;
    }

    public async Task ApplyUsageAndBroadcastAsync()
    {
        CodexUsagePayload? codex;
        AntigravityUsagePayload? antigravity;
        lock (_lock)
        {
            codex = _lastCodexSuccess;
            antigravity = _lastAntigravitySuccess;
        }

        _stateStore.SetUsageSnapshots(codex, antigravity);
        var snapshot = await _stateStore.GetDashboardSnapshotAsync().ConfigureAwait(false);
        await _broadcaster.BroadcastAsync(snapshot).ConfigureAwait(false);
    }
}

public class UsageBackgroundService : BackgroundService
{
    private readonly UsageService _usageService;

    public UsageBackgroundService(UsageService usageService)
    {
        _usageService = usageService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _usageService.RefreshAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Background exception must not crash loop
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

