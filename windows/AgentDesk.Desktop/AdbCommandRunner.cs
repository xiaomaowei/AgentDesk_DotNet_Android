using System.Diagnostics;
using System.Text;

namespace AgentDesk.Desktop;

public class BoundedStringBuilder
{
    private const string TruncationMarker = "\n[...Output Truncated...]";
    private readonly int _maxCapacity;
    private readonly StringBuilder _sb = new();
    private readonly object _lock = new();
    private bool _truncated;

    public BoundedStringBuilder(int maxCapacity = 64 * 1024)
    {
        if (maxCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Max capacity must be greater than zero.");
        }
        _maxCapacity = maxCapacity;
    }

    public void AppendLine(string? text)
    {
        if (text == null) return;
        lock (_lock)
        {
            if (_truncated) return;

            string line = text + Environment.NewLine;
            if (_sb.Length + line.Length <= _maxCapacity)
            {
                _sb.Append(line);
                return;
            }

            _truncated = true;
            string marker = TruncationMarker;
            if (marker.Length > _maxCapacity)
            {
                marker = marker[.._maxCapacity];
            }

            int allowedContentLength = _maxCapacity - marker.Length;
            if (_sb.Length > allowedContentLength)
            {
                _sb.Length = allowedContentLength;
            }
            else
            {
                int remaining = allowedContentLength - _sb.Length;
                if (remaining > 0 && remaining < line.Length)
                {
                    _sb.Append(line.AsSpan(0, remaining));
                }
            }

            _sb.Append(marker);
        }
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return _sb.ToString();
        }
    }
}

public class AdbCommandRunner : IAdbCommandRunner
{
    private readonly TimeSpan _defaultTimeout;

    public AdbCommandRunner(TimeSpan? defaultTimeout = null)
    {
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<AdbCommandResult> RunAsync(string adbPath, IEnumerable<string> args, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = adbPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new BoundedStringBuilder();
        var errorBuilder = new BoundedStringBuilder();

        process.OutputDataReceived += (_, e) => outputBuilder.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => errorBuilder.AppendLine(e.Data);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_defaultTimeout);

            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }

                    try
                    {
                        using var exitCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                        await process.WaitForExitAsync(exitCts.Token).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return new AdbCommandResult(-1, outputBuilder.ToString(), errorBuilder.ToString(), TimedOut: true);
            }

            return new AdbCommandResult(process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString(), TimedOut: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AdbCommandResult(-1, string.Empty, ex.Message, TimedOut: false);
        }
    }
}
