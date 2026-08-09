using AgentDesk.Core.Models;
using AgentDesk.Core.Protocol;

namespace AgentDesk.Core.State;

public class StateStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, AgentState> _states = new();
    private readonly List<string> _sessionOrder = new();
    private readonly Dictionary<string, string> _projectKeys = new();
    private readonly List<string> _projectOrder = new();
    private string? _activeKey;

    public Task<AgentState> UpsertAsync(AgentState state)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(state.ProjectKey))
            {
                return Task.FromResult(state);
            }

            _states.TryGetValue(state.Key, out var previous);
            if (previous != null && state.Status != AgentStatus.Idle)
            {
                state.StartedAt = previous.StartedAt;
            }

            if (previous != null && !string.IsNullOrEmpty(previous.ConversationName))
            {
                if (string.IsNullOrEmpty(state.ConversationName))
                {
                    state.ConversationName = previous.ConversationName;
                }
            }

            // Combine recent events
            var prevEvents = previous?.RecentEvents ?? new List<RecentEvent>();
            var combined = new List<RecentEvent>(prevEvents);
            combined.AddRange(state.RecentEvents);

            var dedupedEvents = new List<RecentEvent>();
            foreach (var ev in combined)
            {
                if (dedupedEvents.Count == 0 ||
                    dedupedEvents[^1].Kind != ev.Kind ||
                    dedupedEvents[^1].Label != ev.Label ||
                    dedupedEvents[^1].Content != ev.Content)
                {
                    dedupedEvents.Add(ev);
                }
            }
            if (dedupedEvents.Count > 6)
            {
                dedupedEvents = dedupedEvents.TakeLast(6).ToList();
            }
            state.RecentEvents = dedupedEvents;

            // Merge models
            var mergedModels = previous != null ? new List<string>(previous.Models) : new List<string>();
            foreach (var m in state.Models)
            {
                var baseName = Translators.ModelHelper.GetModelBaseName(m);
                var newHasEffort = Translators.ModelHelper.HasEffortSuffix(m);
                bool matched = false;

                for (int i = 0; i < mergedModels.Count; i++)
                {
                    if (Translators.ModelHelper.GetModelBaseName(mergedModels[i]).Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        var existingHasEffort = Translators.ModelHelper.HasEffortSuffix(mergedModels[i]);
                        if (newHasEffort || !existingHasEffort)
                        {
                            mergedModels[i] = m;
                        }
                        matched = true;
                        break;
                    }
                }

                if (!matched && mergedModels.Count < 8)
                {
                    mergedModels.Add(m);
                }
            }
            if (mergedModels.Count > 8)
            {
                mergedModels = mergedModels.Take(8).ToList();
            }
            state.Models = mergedModels;

            // Update state
            _states[state.Key] = state;
            _sessionOrder.Remove(state.Key);
            _sessionOrder.Add(state.Key);

            if (!_projectOrder.Contains(state.ProjectKey))
            {
                _projectOrder.Add(state.ProjectKey);
            }
            else
            {
                _projectOrder.Remove(state.ProjectKey);
                _projectOrder.Add(state.ProjectKey);
            }
            _projectKeys[state.ProjectKey] = state.Key;

            _activeKey = state.Key;
            return Task.FromResult(state);
        }
    }

    public Task RemoveAsync(AgentState state)
    {
        lock (_lock)
        {
            _states.TryGetValue(state.Key, out var storedState);
            var projectKey = storedState?.ProjectKey ?? state.ProjectKey;

            _states.Remove(state.Key);
            _sessionOrder.Remove(state.Key);

            if (_projectKeys.TryGetValue(projectKey, out var latestKey) && latestKey == state.Key)
            {
                var replacement = _sessionOrder.LastOrDefault(k => _states.TryGetValue(k, out var s) && s.ProjectKey == projectKey);
                if (replacement == null)
                {
                    _projectKeys.Remove(projectKey);
                    _projectOrder.Remove(projectKey);
                }
                else
                {
                    _projectKeys[projectKey] = replacement;
                }
            }

            if (_activeKey == state.Key)
            {
                _activeKey = _sessionOrder.LastOrDefault();
            }

            return Task.CompletedTask;
        }
    }

    public Task<AgentState?> CurrentAsync()
    {
        lock (_lock)
        {
            if (_activeKey != null && _states.TryGetValue(_activeKey, out var state))
            {
                return Task.FromResult<AgentState?>(state);
            }
            return Task.FromResult<AgentState?>(null);
        }
    }

    public Task<List<AgentState>> ProjectStatesAsync()
    {
        lock (_lock)
        {
            var list = new List<AgentState>();
            foreach (var pk in _projectOrder)
            {
                if (_projectKeys.TryGetValue(pk, out var sessionKey) && _states.TryGetValue(sessionKey, out var state))
                {
                    list.Add(state);
                }
            }
            return Task.FromResult(list);
        }
    }

    public Task<AgentState?> NextProjectAsync()
    {
        lock (_lock)
        {
            if (_projectOrder.Count == 0) return Task.FromResult<AgentState?>(null);
            var current = _activeKey != null && _states.TryGetValue(_activeKey, out var cs) ? cs : null;
            int nextIdx = 0;
            if (current != null)
            {
                int currIdx = _projectOrder.IndexOf(current.ProjectKey);
                if (currIdx >= 0)
                {
                    nextIdx = (currIdx + 1) % _projectOrder.Count;
                }
            }
            var nextProjectKey = _projectOrder[nextIdx];
            _activeKey = _projectKeys[nextProjectKey];
            return Task.FromResult<AgentState?>(_states[_activeKey]);
        }
    }

    public Task<AgentState?> PreviousProjectAsync()
    {
        lock (_lock)
        {
            if (_projectOrder.Count == 0) return Task.FromResult<AgentState?>(null);
            var current = _activeKey != null && _states.TryGetValue(_activeKey, out var cs) ? cs : null;
            int prevIdx = _projectOrder.Count - 1;
            if (current != null)
            {
                int currIdx = _projectOrder.IndexOf(current.ProjectKey);
                if (currIdx >= 0)
                {
                    prevIdx = (currIdx - 1 + _projectOrder.Count) % _projectOrder.Count;
                }
            }
            var prevProjectKey = _projectOrder[prevIdx];
            _activeKey = _projectKeys[prevProjectKey];
            return Task.FromResult<AgentState?>(_states[_activeKey]);
        }
    }

    public Task<AgentState?> SelectProjectAsync(string targetId)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(targetId)) return Task.FromResult<AgentState?>(null);
            AgentState? targetState = null;
            foreach (var state in _states.Values)
            {
                if (state.Id == targetId)
                {
                    targetState = state;
                    break;
                }
            }
            if (targetState == null) return Task.FromResult<AgentState?>(null);
            if (!_projectKeys.TryGetValue(targetState.ProjectKey, out var activeKey))
            {
                return Task.FromResult<AgentState?>(null);
            }
            _activeKey = activeKey;
            return Task.FromResult<AgentState?>(_states[_activeKey]);
        }
    }

    public Task<AgentState?> NextSessionAsync()
    {
        lock (_lock)
        {
            var current = _activeKey != null && _states.TryGetValue(_activeKey, out var cs) ? cs : null;
            if (current == null) return Task.FromResult<AgentState?>(null);
            var projectSessions = _sessionOrder.Where(k => _states.TryGetValue(k, out var s) && s.ProjectKey == current.ProjectKey).ToList();
            if (projectSessions.Count == 0) return Task.FromResult<AgentState?>(current);

            int idx = projectSessions.IndexOf(current.Key);
            int nextIdx = (idx + 1) % projectSessions.Count;
            _activeKey = projectSessions[nextIdx];
            return Task.FromResult<AgentState?>(_states[_activeKey]);
        }
    }

    public Task<AgentState?> ClearCurrentAsync()
    {
        lock (_lock)
        {
            var state = _activeKey != null && _states.TryGetValue(_activeKey, out var cs) ? cs : null;
            if (state == null || state.RequiresAction) return Task.FromResult(state);
            state.Status = AgentStatus.Idle;
            state.Message = "Notification cleared";
            return Task.FromResult<AgentState?>(state);
        }
    }

    public async Task<DashboardSnapshot> GetDashboardSnapshotAsync()
    {
        var current = await CurrentAsync();
        var projects = await ProjectStatesAsync();

        return new DashboardSnapshot
        {
            Version = "1.0",
            Current = current?.ToEnvelope(),
            Projects = projects.Select(p => p.ToEnvelope()).ToList()
        };
    }
}
