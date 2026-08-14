using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace AgentDesk.Server.Usage;

public interface IAntigravityRunner
{
    Task<string?> RunAsync(CancellationToken cancellationToken = default);
}

public class PowerShellAntigravityRunner : IAntigravityRunner
{
    public static readonly TimeSpan DefaultOuterTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan DefaultInnerTimeout = TimeSpan.FromSeconds(45);

    private readonly string? _cli;
    private readonly string? _pwshPath;
    private readonly TimeSpan _timeout;

    public TimeSpan Timeout => _timeout;

    public PowerShellAntigravityRunner() : this(null, null, null)
    {
    }

    public PowerShellAntigravityRunner(string? cli, string? pwshPath = null, TimeSpan? timeout = null)
    {
        _cli = cli;
        _pwshPath = pwshPath;
        _timeout = timeout ?? DefaultOuterTimeout;
    }

    public async Task<string?> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var token = linkedCts.Token;

        ProcessStartInfo startInfo;
        try
        {
            startInfo = BuildStartInfo(_cli, _pwshPath);
        }
        catch
        {
            return null;
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        IntPtr hJob = IntPtr.Zero;

        try
        {
            if (!process.Start())
            {
                return null;
            }

            hJob = SetupJobObject(process.Handle);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);

            await process.WaitForExitAsync(token).ConfigureAwait(false);

            string stdout = await stdoutTask.ConfigureAwait(false);
            _ = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return null;
            }

            return stdout;
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            return null;
        }
        catch (Exception)
        {
            KillProcessTree(process);
            return null;
        }
        finally
        {
            if (hJob != IntPtr.Zero)
            {
                CloseHandle(hJob);
            }
        }
    }

    public static ProcessStartInfo BuildStartInfo(string? cli = null, string? pwshPath = null, string? cwd = null)
    {
        string resolvedAgy = ResolveAgyExecutable(cli);
        string resolvedPwsh = ResolvePwshExecutable(pwshPath);
        string workingDir = cwd ?? Environment.GetEnvironmentVariable("AGENTDECK_ANTIGRAVITY_CWD") ?? Directory.GetCurrentDirectory();

        var startInfo = new ProcessStartInfo
        {
            FileName = resolvedPwsh,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDir,
        };

        int innerTimeoutSeconds = (int)DefaultInnerTimeout.TotalSeconds;

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-WindowStyle");
        startInfo.ArgumentList.Add("Hidden");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add($"& $env:AGENTDESK_AGY_EXE -p \"/usage\" --print-timeout {innerTimeoutSeconds.ToString(CultureInfo.InvariantCulture)}s; exit $LASTEXITCODE");

        startInfo.EnvironmentVariables["AGY_CLI_HIDE_ACCOUNT_INFO"] = "1";
        startInfo.EnvironmentVariables["AGENTDESK_AGY_EXE"] = resolvedAgy;

        return startInfo;
    }

    public static string ResolvePwshExecutable(string? pwshPath = null)
    {
        if (!string.IsNullOrEmpty(pwshPath) && File.Exists(pwshPath))
        {
            return Path.GetFullPath(pwshPath);
        }

        var envPwsh = Environment.GetEnvironmentVariable("AGENTDESK_PWSH_PATH")
                   ?? Environment.GetEnvironmentVariable("PWSH_PATH");
        if (!string.IsNullOrEmpty(envPwsh) && File.Exists(envPwsh))
        {
            return Path.GetFullPath(envPwsh);
        }

        var extraDirs = new List<string>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                extraDirs.Add(Path.Combine(programFiles, "PowerShell", "7"));
                extraDirs.Add(Path.Combine(programFiles, "PowerShell", "7-preview"));
            }
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFilesX86))
            {
                extraDirs.Add(Path.Combine(programFilesX86, "PowerShell", "7"));
            }
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                extraDirs.Add(Path.Combine(localAppData, "Microsoft", "WindowsApps"));
            }
        }

        return ResolveExecutable("pwsh", extraDirs);
    }

    public static string ResolveAgyExecutable(string? cli = null)
    {
        string target = cli ?? Environment.GetEnvironmentVariable("AGENTDECK_ANTIGRAVITY_CLI") ?? "agy";
        if (File.Exists(target))
        {
            return Path.GetFullPath(target);
        }

        var extraDirs = new List<string>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                extraDirs.Add(Path.Combine(localAppData, "agy", "bin"));
                extraDirs.Add(Path.Combine(localAppData, "Programs", "agy", "bin"));
            }
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                extraDirs.Add(Path.Combine(userProfile, ".antigravity", "bin"));
                extraDirs.Add(Path.Combine(userProfile, ".agy", "bin"));
            }
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                extraDirs.Add(Path.Combine(appData, "npm"));
            }
        }

        return ResolveExecutable(target, extraDirs);
    }

    public static string ResolveExecutable(string fileName, IEnumerable<string>? extraSearchDirs = null)
    {
        if (File.Exists(fileName)) return Path.GetFullPath(fileName);

        var paths = new List<string>();
        if (extraSearchDirs != null)
        {
            foreach (var dir in extraSearchDirs)
            {
                if (!string.IsNullOrWhiteSpace(dir) && !paths.Contains(dir, StringComparer.OrdinalIgnoreCase))
                {
                    paths.Add(dir);
                }
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!paths.Contains(dir, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(dir);
            }
        }

        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "", ".exe", ".cmd", ".bat", ".ps1" }
            : new[] { "" };

        foreach (var dir in paths)
        {
            foreach (var ext in extensions)
            {
                try
                {
                    var fullPath = Path.Combine(dir, fileName + ext);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                    // ignore invalid path characters
                }
            }
        }
        return fileName;
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Process may have already terminated
        }
    }

    private static IntPtr SetupJobObject(IntPtr processHandle)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IntPtr.Zero;
        }

        IntPtr hJob = IntPtr.Zero;
        try
        {
            hJob = CreateJobObject(IntPtr.Zero, null);
            if (hJob == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var extendedLimit = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };
            int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            IntPtr pExtendedLimit = Marshal.AllocHGlobal(length);
            bool setInfoSuccess;
            try
            {
                Marshal.StructureToPtr(extendedLimit, pExtendedLimit, false);
                setInfoSuccess = SetInformationJobObject(hJob, JobObjectExtendedLimitInformation, pExtendedLimit, (uint)length);
            }
            finally
            {
                Marshal.FreeHGlobal(pExtendedLimit);
            }

            if (!setInfoSuccess)
            {
                CloseHandle(hJob);
                return IntPtr.Zero;
            }

            if (!AssignProcessToJobObject(hJob, processHandle))
            {
                CloseHandle(hJob);
                return IntPtr.Zero;
            }

            return hJob;
        }
        catch
        {
            if (hJob != IntPtr.Zero)
            {
                try
                {
                    CloseHandle(hJob);
                }
                catch
                {
                    // ignore
                }
            }
            return IntPtr.Zero;
        }
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectExtendedLimitInformation = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
