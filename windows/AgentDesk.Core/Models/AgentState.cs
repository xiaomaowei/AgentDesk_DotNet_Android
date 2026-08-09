using AgentDesk.Core.Protocol;

namespace AgentDesk.Core.Models;

public record AgentState
{
    public string Agent { get; set; } = "codex";
    public string SessionId { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string? ConversationName { get; set; }
    public string Status { get; set; } = AgentStatus.Idle;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public long? ConversationTokens { get; set; }
    public List<string>? Steps { get; set; }
    public int? CurrentStep { get; set; }
    public List<RecentEvent> RecentEvents { get; set; } = new();
    public bool RequiresAction { get; set; }
    public List<string> Actions { get; set; } = new();
    public string? TargetId { get; set; }
    public List<string> Models { get; set; } = new();
    public CurrentTurn? CurrentTurn { get; set; }

    public string Id { get; set; } = $"evt_{Guid.NewGuid():n}";
    public string Key => $"{Agent}:{SessionId}";
    public string ProjectKey => ProjectId ?? Project;

    public AgentStatePayload ToPayload(DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.UtcNow;
        var elapsedSeconds = (long)(current - StartedAt).TotalSeconds;
        var elapsed = elapsedSeconds < 0 ? 0 : elapsedSeconds;

        return new AgentStatePayload
        {
            Agent = Agent,
            Project = Project,
            Status = Status,
            Message = Message.Length > 256 ? Message[..256] : Message,
            Elapsed = elapsed,
            RequiresAction = RequiresAction,
            Actions = Actions,
            TargetId = TargetId,
            ConversationName = ConversationName,
            ConversationTokens = ConversationTokens,
            Steps = Steps,
            CurrentStep = CurrentStep,
            RecentEvents = RecentEvents.Count > 0 ? RecentEvents.Take(6).Select(e => new RecentEvent
            {
                Kind = e.Kind,
                Label = e.Label.Length > 180 ? e.Label[..180] : e.Label,
                Content = e.Content != null && e.Content.Length > 1600 ? e.Content[..1600] : e.Content
            }).ToList() : null,
            Models = Models.Count > 0 ? Models.Take(8).ToList() : null,
            CurrentTurn = CurrentTurn != null ? new CurrentTurn
            {
                Id = CurrentTurn.Id,
                StartedAt = CurrentTurn.StartedAt,
                Prompt = CurrentTurn.Prompt.Length > 8000 ? CurrentTurn.Prompt[..8000] : CurrentTurn.Prompt,
                Items = CurrentTurn.Items.Take(20).Select(i => new TurnItem
                {
                    Id = i.Id,
                    Timestamp = i.Timestamp,
                    Kind = i.Kind,
                    Phase = i.Phase,
                    Label = i.Label.Length > 180 ? i.Label[..180] : i.Label,
                    Content = i.Content != null && i.Content.Length > 4000 ? i.Content[..4000] : i.Content,
                    Sig = i.Sig
                }).ToList()
            } : null
        };
    }

    public ProtocolEnvelope<AgentStatePayload> ToEnvelope(DateTimeOffset? now = null)
    {
        return new ProtocolEnvelope<AgentStatePayload>
        {
            Version = "1.0",
            Type = "state",
            Id = Id,
            Timestamp = StartedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Payload = ToPayload(now)
        };
    }
}
