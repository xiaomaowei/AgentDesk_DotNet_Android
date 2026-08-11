namespace AgentDesk.Core.Models;

public static class AgentStatus
{
    public const string Idle = "idle";
    public const string Working = "working";
    public const string Waiting = "waiting";
    public const string Completed = "completed";
    public const string Error = "error";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Idle, Working, Waiting, Completed, Error
    };
}
