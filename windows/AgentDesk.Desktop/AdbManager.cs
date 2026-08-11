namespace AgentDesk.Desktop;

public class AdbManager
{
    private readonly IAdbCommandRunner _runner;

    public AdbManager(IAdbCommandRunner? runner = null)
    {
        _runner = runner ?? new AdbCommandRunner();
    }

    public string? DiscoverAdbPath(IEnumerable<string>? customCandidates = null)
    {
        if (customCandidates != null)
        {
            foreach (var candidate in customCandidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        var envPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathDirs = envPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in pathDirs)
        {
            var exePath = Path.Combine(dir.Trim(), "adb.exe");
            if (File.Exists(exePath))
            {
                return Path.GetFullPath(exePath);
            }
            var noExtPath = Path.Combine(dir.Trim(), "adb");
            if (File.Exists(noExtPath))
            {
                return Path.GetFullPath(noExtPath);
            }
        }

        var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (!string.IsNullOrEmpty(androidHome))
        {
            var p1 = Path.Combine(androidHome, "platform-tools", "adb.exe");
            if (File.Exists(p1)) return Path.GetFullPath(p1);
        }

        var androidSdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        if (!string.IsNullOrEmpty(androidSdkRoot))
        {
            var p2 = Path.Combine(androidSdkRoot, "platform-tools", "adb.exe");
            if (File.Exists(p2)) return Path.GetFullPath(p2);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            var p3 = Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe");
            if (File.Exists(p3)) return Path.GetFullPath(p3);
        }

        return null;
    }

    public List<string> ParseDevices(string adbDevicesOutput)
    {
        var serials = new List<string>();
        if (string.IsNullOrWhiteSpace(adbDevicesOutput)) return serials;

        var lines = adbDevicesOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var serial = parts[0];
                var state = parts[1];

                if (string.Equals(state, "device", StringComparison.OrdinalIgnoreCase))
                {
                    serials.Add(serial);
                }
            }
        }

        return serials;
    }

    public bool IsReverseConfigured(string reverseListOutput)
    {
        if (string.IsNullOrWhiteSpace(reverseListOutput)) return false;

        return reverseListOutput.Contains("tcp:8765 tcp:8765", StringComparison.OrdinalIgnoreCase) ||
               reverseListOutput.Contains("(tcp:8765, tcp:8765)", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatConciseError(string? rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError))
        {
            return "execution failed";
        }

        var lines = rawError.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length > 0)
            {
                return trimmed.Length > 240 ? trimmed[..240] : trimmed;
            }
        }

        return "execution failed";
    }

    public async Task<string> GetStatusAsync(string? adbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            return "ADB missing";
        }

        var result = await _runner.RunAsync(adbPath, new[] { "devices", "-l" }, cancellationToken).ConfigureAwait(false);
        if (result.TimedOut || result.ExitCode != 0)
        {
            return $"ADB error: {FormatConciseError(result.Error)}";
        }

        var serials = ParseDevices(result.Output);
        if (serials.Count == 0)
        {
            return "No authorized devices";
        }

        return $"Connected ({serials.Count} device{(serials.Count > 1 ? "s" : "")})";
    }

    public async Task MaintainReverseAsync(string adbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            throw new InvalidOperationException("ADB executable path is empty.");
        }

        var devicesResult = await _runner.RunAsync(adbPath, new[] { "devices", "-l" }, cancellationToken).ConfigureAwait(false);
        if (devicesResult.ExitCode != 0 || devicesResult.TimedOut)
        {
            var err = FormatConciseError(devicesResult.Error);
            throw new InvalidOperationException($"ADB devices failed: {err}");
        }

        var serials = ParseDevices(devicesResult.Output);
        if (serials.Count == 0)
        {
            throw new InvalidOperationException("No authorized devices");
        }

        foreach (var serial in serials)
        {
            var revListResult = await _runner.RunAsync(adbPath, new[] { "-s", serial, "reverse", "--list" }, cancellationToken).ConfigureAwait(false);
            if (revListResult.ExitCode == 0 && !revListResult.TimedOut && IsReverseConfigured(revListResult.Output))
            {
                continue;
            }

            var revResult = await _runner.RunAsync(adbPath, new[] { "-s", serial, "reverse", "tcp:8765", "tcp:8765" }, cancellationToken).ConfigureAwait(false);
            if (revResult.ExitCode != 0 || revResult.TimedOut)
            {
                var err = FormatConciseError(revResult.Error);
                throw new InvalidOperationException($"ADB reverse failed for {serial}: {err}");
            }
        }
    }

    public async Task ConnectAndLaunchAsync(string adbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            throw new InvalidOperationException("ADB executable path is empty.");
        }

        var startResult = await _runner.RunAsync(adbPath, new[] { "start-server" }, cancellationToken).ConfigureAwait(false);
        if (startResult.ExitCode != 0 || startResult.TimedOut)
        {
            var err = FormatConciseError(startResult.Error);
            throw new InvalidOperationException($"ADB start-server failed: {err}");
        }

        var devicesResult = await _runner.RunAsync(adbPath, new[] { "devices", "-l" }, cancellationToken).ConfigureAwait(false);
        if (devicesResult.ExitCode != 0 || devicesResult.TimedOut)
        {
            var err = FormatConciseError(devicesResult.Error);
            throw new InvalidOperationException($"ADB devices failed: {err}");
        }

        var serials = ParseDevices(devicesResult.Output);
        if (serials.Count == 0)
        {
            throw new InvalidOperationException("No authorized devices");
        }

        foreach (var serial in serials)
        {
            var revListResult = await _runner.RunAsync(adbPath, new[] { "-s", serial, "reverse", "--list" }, cancellationToken).ConfigureAwait(false);
            if (revListResult.ExitCode != 0 || revListResult.TimedOut || !IsReverseConfigured(revListResult.Output))
            {
                var revResult = await _runner.RunAsync(adbPath, new[] { "-s", serial, "reverse", "tcp:8765", "tcp:8765" }, cancellationToken).ConfigureAwait(false);
                if (revResult.ExitCode != 0 || revResult.TimedOut)
                {
                    var err = FormatConciseError(revResult.Error);
                    throw new InvalidOperationException($"ADB reverse failed for {serial}: {err}");
                }
            }

            var launchResult = await _runner.RunAsync(adbPath, new[] { "-s", serial, "shell", "am", "start", "-n", "com.agentdeck.mobile/.MainActivity" }, cancellationToken).ConfigureAwait(false);
            if (launchResult.ExitCode != 0 || launchResult.TimedOut)
            {
                var err = FormatConciseError(launchResult.Error);
                throw new InvalidOperationException($"ADB launch failed for {serial}: {err}");
            }
        }
    }
}
