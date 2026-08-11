using AgentDesk.Core.Models;
using AgentDesk.Core.Usage;

namespace AgentDesk.Server.Usage;

public interface IAntigravityUsageReader
{
    Task<AntigravityUsagePayload?> FetchAsync(CancellationToken cancellationToken = default);
}

public class AntigravityUsageReader : IAntigravityUsageReader
{
    private readonly IAntigravityRunner _runner;

    public AntigravityUsageReader(IAntigravityRunner runner)
    {
        _runner = runner;
    }

    public async Task<AntigravityUsagePayload?> FetchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await _runner.RunAsync(cancellationToken);
            if (string.IsNullOrEmpty(output)) return null;
            return AntigravityUsageParser.Parse(output);
        }
        catch
        {
            return null;
        }
    }
}
