using System.Text.Json;
using AgentDesk.Core.Models;

namespace AgentDesk.Core.Translators;

public record HookUpdate
{
    public required AgentState State { get; init; }
    public bool WaitsForAction { get; init; }
    public bool RemoveSession { get; init; }
}

public class CodexTranslator
{
    private readonly Dictionary<string, CurrentTurn> _currentTurns = new();
    private readonly Dictionary<string, List<string>> _modelHistory = new();
    private readonly Dictionary<string, string> _lastCommentaryId = new();
    private readonly LinkedList<string> _sessionLru = new();
    private readonly object _lock = new();

    public HookUpdate Translate(JsonElement eventElement)
    {
        string eventName = GetStringProperty(eventElement, "hook_event_name") ?? "Unknown";
        string sessionId = GetStringProperty(eventElement, "session_id") ?? $"session_{Guid.NewGuid():n}";
        string cwd = GetStringProperty(eventElement, "cwd") ?? "";
        string? transcriptPath = GetStringProperty(eventElement, "transcript_path");

        var parseResult = TranscriptParser.ParseTail(transcriptPath);

        string project = ResolveProjectName(cwd);
        bool waiting = eventName == "PermissionRequest";
        bool removeSession = eventName == "SessionEnd";

        string status = GetStatus(eventName);
        string message = GetMessage(eventName, eventElement);

        lock (_lock)
        {
            TouchSession(sessionId);

            AccumulateModels(sessionId, eventElement);
            UpdateCurrentTurn(sessionId, eventName, eventElement, parseResult);

            if (removeSession)
            {
                RemoveSessionData(sessionId);
            }

            var models = _modelHistory.TryGetValue(sessionId, out var history) ? new List<string>(history) : new List<string>();
            _currentTurns.TryGetValue(sessionId, out var currentTurn);

            var state = new AgentState
            {
                Agent = "codex",
                SessionId = sessionId,
                Project = project,
                Status = status,
                Message = message.Length > 180 ? message[..180] : message,
                StartedAt = DateTimeOffset.UtcNow,
                ConversationTokens = parseResult.ConversationTokens,
                RequiresAction = waiting,
                Actions = waiting ? new List<string> { DeviceAction.Approve, DeviceAction.Reject } : new List<string>(),
                TargetId = null,
                Models = models,
                CurrentTurn = currentTurn
            };

            if (waiting)
            {
                state.TargetId = $"approval_{state.Id}";
            }

            return new HookUpdate
            {
                State = state,
                WaitsForAction = waiting,
                RemoveSession = removeSession
            };
        }
    }

    private void TouchSession(string sessionId)
    {
        var node = _sessionLru.Find(sessionId);
        if (node != null)
        {
            _sessionLru.Remove(node);
        }
        _sessionLru.AddLast(sessionId);

        while (_sessionLru.Count > 100)
        {
            var oldestNode = _sessionLru.First;
            if (oldestNode == null) break;
            var oldest = oldestNode.Value;
            _sessionLru.RemoveFirst();
            RemoveSessionData(oldest);
        }
    }

    private void RemoveSessionData(string sessionId)
    {
        _currentTurns.Remove(sessionId);
        _modelHistory.Remove(sessionId);
        _lastCommentaryId.Remove(sessionId);
        var node = _sessionLru.Find(sessionId);
        if (node != null)
        {
            _sessionLru.Remove(node);
        }
    }

    private static string ResolveProjectName(string cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return "Unknown project";
        try
        {
            var name = Path.GetFileName(cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(name) ? "Unknown project" : name;
        }
        catch
        {
            return "Unknown project";
        }
    }

    private static string GetStatus(string eventName) => eventName switch
    {
        "UserPromptSubmit" or "PreToolUse" or "PostToolUse" => AgentStatus.Working,
        "PermissionRequest" => AgentStatus.Waiting,
        "Stop" => AgentStatus.Completed,
        _ => AgentStatus.Working
    };

    private static string GetMessage(string eventName, JsonElement el) => eventName switch
    {
        "UserPromptSubmit" => Truncate(GetStringProperty(el, "prompt") ?? "Prompt submitted", 180),
        "PreToolUse" => FormatToolLabel(el, "Executing"),
        "PostToolUse" => FormatToolLabel(el, "Completed"),
        "PermissionRequest" => FormatToolLabel(el, "Waiting for approval"),
        "Stop" => "Completed turn",
        "SessionEnd" => "Session ended",
        _ => $"Event {eventName}"
    };

    private static string FormatToolLabel(JsonElement el, string prefix)
    {
        var toolName = GetStringProperty(el, "tool_name") ?? "tool";
        var toolInput = el.TryGetProperty("tool_input", out var inputProp) && inputProp.ValueKind == JsonValueKind.Object ? inputProp : (JsonElement?)null;

        string? detail = null;
        if (toolInput.HasValue)
        {
            detail = GetStringProperty(toolInput.Value, "command")
                  ?? GetStringProperty(toolInput.Value, "path")
                  ?? GetStringProperty(toolInput.Value, "file_path")
                  ?? GetStringProperty(toolInput.Value, "description");
        }

        if (!string.IsNullOrEmpty(detail))
        {
            var label = $"{prefix} {toolName}: {detail}";
            return Truncate(label, 180);
        }
        return $"{prefix} {toolName}";
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length > maxLen ? text[..maxLen] : text;
    }

    private static string? GetStringProperty(JsonElement el, string propertyName)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private void AccumulateModels(string sessionId, JsonElement el)
    {
        var labels = new List<string>();

        var modelSlug = GetStringProperty(el, "model");
        if (!string.IsNullOrWhiteSpace(modelSlug))
        {
            var effort = GetStringProperty(el, "effort");
            var label = ModelHelper.NormalizeModelSlug(modelSlug, effort);
            if (!string.IsNullOrEmpty(label))
            {
                labels.Add(label);
            }
        }

        var eventName = GetStringProperty(el, "hook_event_name");
        if (eventName == "PreToolUse" || eventName == "PostToolUse")
        {
            if (el.TryGetProperty("tool_input", out var toolInput) && toolInput.ValueKind == JsonValueKind.Object)
            {
                var command = GetStringProperty(toolInput, "command");
                var agySlug = ModelHelper.ExtractAgyModel(command);
                if (!string.IsNullOrEmpty(agySlug))
                {
                    var label = ModelHelper.NormalizeModelSlug(agySlug);
                    if (!string.IsNullOrEmpty(label))
                    {
                        labels.Add(label);
                    }
                }
            }
        }

        if (labels.Count == 0) return;

        if (!_modelHistory.TryGetValue(sessionId, out var history))
        {
            history = new List<string>();
            _modelHistory[sessionId] = history;
        }

        foreach (var label in labels)
        {
            var baseName = ModelHelper.GetModelBaseName(label);
            var newHasEffort = ModelHelper.HasEffortSuffix(label);
            bool matched = false;

            for (int i = 0; i < history.Count; i++)
            {
                if (ModelHelper.GetModelBaseName(history[i]).Equals(baseName, StringComparison.OrdinalIgnoreCase))
                {
                    var existingHasEffort = ModelHelper.HasEffortSuffix(history[i]);
                    if (newHasEffort || !existingHasEffort)
                    {
                        history[i] = label;
                    }
                    matched = true;
                    break;
                }
            }

            if (!matched && history.Count < 8)
            {
                history.Add(label);
            }
        }
    }

    private void UpdateCurrentTurn(string sessionId, string eventName, JsonElement el, TranscriptParseResult parseResult)
    {
        var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        if (eventName == "UserPromptSubmit")
        {
            var prompt = GetStringProperty(el, "prompt") ?? string.Empty;
            var turnId = $"turn_{Guid.NewGuid():n}"[..21];
            var turn = new CurrentTurn
            {
                Id = turnId,
                StartedAt = now,
                Prompt = Truncate(prompt, 8000),
                Items = new List<TurnItem>()
            };
            _currentTurns[sessionId] = turn;
            return;
        }

        if (!_currentTurns.TryGetValue(sessionId, out var currentTurn))
        {
            return;
        }

        var toolName = GetStringProperty(el, "tool_name") ?? "tool";
        var toolUseId = ExtractToolUseId(el);
        var sig = $"{toolName.ToLowerInvariant()}::{GetToolInputDetail(el)}";

        if (eventName == "PermissionRequest")
        {
            ProcessLatestCommentary(currentTurn, sessionId, parseResult.LatestCommentary, now, insertIndex: null);

            var itemId = !string.IsNullOrEmpty(toolUseId) ? $"approval_{currentTurn.Id}_{toolUseId}" : $"approval_{currentTurn.Id}_{Guid.NewGuid():n}"[..21];
            var existing = currentTurn.Items.FirstOrDefault(i => i.Id == itemId && i.Kind == TurnItemKind.Approval);

            if (existing == null)
            {
                currentTurn.Items.Add(new TurnItem
                {
                    Id = itemId,
                    Timestamp = now,
                    Kind = TurnItemKind.Approval,
                    Phase = TurnItemPhase.Waiting,
                    Label = Truncate(FormatToolLabel(el, "Waiting approval for"), 180),
                    Sig = sig
                });
            }
            else
            {
                existing.Phase = TurnItemPhase.Waiting;
                existing.Label = Truncate(FormatToolLabel(el, "Waiting approval for"), 180);
                existing.Timestamp = now;
            }
        }
        else if (eventName == "PreToolUse")
        {
            var matchingApproval = currentTurn.Items.FirstOrDefault(i => i.Kind == TurnItemKind.Approval && i.Phase == TurnItemPhase.Waiting && (i.Sig == sig || string.IsNullOrEmpty(i.Sig)));
            if (matchingApproval != null)
            {
                matchingApproval.Phase = TurnItemPhase.Delivered;
            }

            ProcessLatestCommentary(currentTurn, sessionId, parseResult.LatestCommentary, now, insertIndex: null);

            var itemId = !string.IsNullOrEmpty(toolUseId) ? $"tool_{currentTurn.Id}_{toolUseId}" : $"tool_{currentTurn.Id}_{Guid.NewGuid():n}"[..21];
            currentTurn.Items.Add(new TurnItem
            {
                Id = itemId,
                Timestamp = now,
                Kind = TurnItemKind.Tool,
                Phase = TurnItemPhase.Running,
                Label = Truncate(FormatToolLabel(el, "Executing"), 180),
                Sig = sig
            });
        }
        else if (eventName == "PostToolUse")
        {
            TurnItem? existing = null;
            if (!string.IsNullOrEmpty(toolUseId))
            {
                var targetId = $"tool_{currentTurn.Id}_{toolUseId}";
                existing = currentTurn.Items.FirstOrDefault(i => i.Id == targetId && i.Kind == TurnItemKind.Tool && i.Phase != TurnItemPhase.Completed);
            }

            existing ??= currentTurn.Items.FirstOrDefault(i => i.Kind == TurnItemKind.Tool && i.Phase == TurnItemPhase.Running && i.Sig == sig);
            existing ??= currentTurn.Items.FirstOrDefault(i => i.Kind == TurnItemKind.Tool && i.Phase == TurnItemPhase.Running && i.Sig.StartsWith($"{toolName.ToLowerInvariant()}::"));
            existing ??= currentTurn.Items.FirstOrDefault(i => i.Kind == TurnItemKind.Tool && i.Phase == TurnItemPhase.Running);
            existing ??= currentTurn.Items.FirstOrDefault(i => i.Kind == TurnItemKind.Approval && i.Phase == TurnItemPhase.Waiting);

            if (existing != null)
            {
                int idx = currentTurn.Items.IndexOf(existing);
                ProcessLatestCommentary(currentTurn, sessionId, parseResult.LatestCommentary, now, insertIndex: idx);

                existing.Phase = TurnItemPhase.Completed;
                existing.Label = Truncate(FormatToolLabel(el, "Completed"), 180);
                existing.Timestamp = now;
            }
            else
            {
                ProcessLatestCommentary(currentTurn, sessionId, parseResult.LatestCommentary, now, insertIndex: null);

                var itemId = !string.IsNullOrEmpty(toolUseId) ? $"tool_{currentTurn.Id}_{toolUseId}" : $"tool_{currentTurn.Id}_{Guid.NewGuid():n}"[..21];
                currentTurn.Items.Add(new TurnItem
                {
                    Id = itemId,
                    Timestamp = now,
                    Kind = TurnItemKind.Tool,
                    Phase = TurnItemPhase.Completed,
                    Label = Truncate(FormatToolLabel(el, "Completed"), 180),
                    Sig = sig
                });
            }
        }
        else if (eventName == "Stop")
        {
            var lastReply = GetStringProperty(el, "last_assistant_message");
            var finalContent = !string.IsNullOrWhiteSpace(lastReply) ? Truncate(lastReply.Trim(), 4000) : null;

            var existingFinal = currentTurn.Items.FirstOrDefault(i => i.Kind == TurnItemKind.Final);
            if (existingFinal != null)
            {
                existingFinal.Phase = TurnItemPhase.Delivered;
                existingFinal.Content = finalContent;
                existingFinal.Timestamp = now;
            }
            else
            {
                currentTurn.Items.Add(new TurnItem
                {
                    Id = $"final_{currentTurn.Id}",
                    Timestamp = now,
                    Kind = TurnItemKind.Final,
                    Phase = TurnItemPhase.Delivered,
                    Label = "最終答案",
                    Content = finalContent
                });
            }
        }

        // Enforce max 20 tool/approval items
        var toolItems = currentTurn.Items.Where(i => i.Kind == TurnItemKind.Tool || i.Kind == TurnItemKind.Approval).ToList();
        if (toolItems.Count > 20)
        {
            var toRemove = toolItems[0];
            currentTurn.Items.Remove(toRemove);
        }
    }

    private void ProcessLatestCommentary(CurrentTurn currentTurn, string sessionId, CommentaryItem? latestCommentary, string now, int? insertIndex)
    {
        if (latestCommentary == null) return;

        if (_lastCommentaryId.TryGetValue(sessionId, out var lastId) && lastId == latestCommentary.Id)
        {
            return;
        }

        _lastCommentaryId[sessionId] = latestCommentary.Id;

        var commentaryTurnItem = new TurnItem
        {
            Id = $"commentary_{currentTurn.Id}_{latestCommentary.Id}",
            Timestamp = now,
            Kind = TurnItemKind.Commentary,
            Phase = TurnItemPhase.Delivered,
            Label = "Commentary",
            Content = latestCommentary.Content,
            Sig = string.Empty
        };

        if (insertIndex.HasValue && insertIndex.Value >= 0 && insertIndex.Value <= currentTurn.Items.Count)
        {
            currentTurn.Items.Insert(insertIndex.Value, commentaryTurnItem);
        }
        else
        {
            currentTurn.Items.Add(commentaryTurnItem);
        }
    }

    private static string? ExtractToolUseId(JsonElement el)
    {
        foreach (var key in new[] { "tool_use_id", "tool_call_id", "call_id", "tool_id" })
        {
            var val = GetStringProperty(el, key);
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
        }

        if (el.TryGetProperty("tool_input", out var inputProp) && inputProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "tool_use_id", "tool_call_id", "call_id", "id" })
            {
                var val = GetStringProperty(inputProp, key);
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }
        }

        return null;
    }

    private static string GetToolInputDetail(JsonElement el)
    {
        if (el.TryGetProperty("tool_input", out var inputProp))
        {
            if (inputProp.ValueKind == JsonValueKind.Object)
            {
                var val = GetStringProperty(inputProp, "command")
                       ?? GetStringProperty(inputProp, "path")
                       ?? GetStringProperty(inputProp, "file_path")
                       ?? GetStringProperty(inputProp, "description");
                if (val != null) return val.Trim();
            }
            else if (inputProp.ValueKind == JsonValueKind.String)
            {
                return inputProp.GetString()?.Trim() ?? string.Empty;
            }
        }
        return string.Empty;
    }
}
