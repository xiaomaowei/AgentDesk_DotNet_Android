using System.Text.Json;
using AgentDesk.Core.Protocol;

namespace AgentDesk.Server;

public class SseSubscription
{
    private readonly System.Threading.Channels.Channel<(string? Data, bool IsKeepAlive)> _channel =
        System.Threading.Channels.Channel.CreateBounded<(string?, bool)>(new System.Threading.Channels.BoundedChannelOptions(10)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest
        });

    public async ValueTask BroadcastAsync(string data)
    {
        await _channel.Writer.WriteAsync((data, false));
    }

    public async ValueTask KeepAliveAsync()
    {
        await _channel.Writer.WriteAsync((null, true));
    }

    public async ValueTask<(string? Data, bool IsKeepAlive)> ReadNextAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await _channel.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, true);
        }
    }
}

public class SseBroadcaster
{
    private readonly object _lock = new();
    private readonly HashSet<SseSubscription> _subscriptions = new();

    public SseSubscription Subscribe()
    {
        var sub = new SseSubscription();
        lock (_lock)
        {
            _subscriptions.Add(sub);
        }
        return sub;
    }

    public void Unsubscribe(SseSubscription sub)
    {
        lock (_lock)
        {
            _subscriptions.Remove(sub);
        }
    }

    public async Task BroadcastAsync(DashboardSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, ProtocolSerializerOptions.Default);
        List<SseSubscription> currentSubs;
        lock (_lock)
        {
            currentSubs = _subscriptions.ToList();
        }

        foreach (var sub in currentSubs)
        {
            await sub.BroadcastAsync(json);
        }
    }
}
