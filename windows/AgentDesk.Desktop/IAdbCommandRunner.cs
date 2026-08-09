namespace AgentDesk.Desktop;

public record AdbCommandResult(int ExitCode, string Output, string Error, bool TimedOut);

public interface IAdbCommandRunner
{
    Task<AdbCommandResult> RunAsync(string adbPath, IEnumerable<string> args, CancellationToken cancellationToken = default);
}
