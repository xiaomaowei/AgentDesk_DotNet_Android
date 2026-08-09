using AgentDesk.Core.Models;

namespace AgentDesk.Core.State;

public class ApprovalBroker
{
    private readonly object _lock = new();
    private readonly Dictionary<string, TaskCompletionSource<string>> _pending = new();

    public Task<string> Register(string targetId)
    {
        lock (_lock)
        {
            if (_pending.TryGetValue(targetId, out var existing) && !existing.Task.IsCompleted)
            {
                existing.TrySetCanceled();
            }

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[targetId] = tcs;
            return tcs.Task;
        }
    }

    public bool Resolve(string? targetId, string action)
    {
        if (string.IsNullOrEmpty(targetId)) return false;

        lock (_lock)
        {
            if (_pending.TryGetValue(targetId, out var tcs))
            {
                _pending.Remove(targetId);
                if (!tcs.Task.IsCompleted)
                {
                    return tcs.TrySetResult(action);
                }
            }
            return false;
        }
    }

    public void Discard(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return;

        lock (_lock)
        {
            if (_pending.TryGetValue(targetId, out var tcs))
            {
                _pending.Remove(targetId);
                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetCanceled();
                }
            }
        }
    }
}
