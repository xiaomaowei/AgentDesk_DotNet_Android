namespace AgentDesk.Core.Models;

public static class DeviceAction
{
    public const string Next = "next";
    public const string NextProject = "next_project";
    public const string PreviousProject = "previous_project";
    public const string SelectProject = "select_project";
    public const string Usage = "usage";
    public const string UsageNext = "usage_next";
    public const string Clear = "clear";
    public const string Approve = "approve";
    public const string Reject = "reject";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Next, NextProject, PreviousProject, SelectProject, Usage, UsageNext, Clear, Approve, Reject
    };
}
