using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AgentDesk.Core.Usage;
using Microsoft.Win32.SafeHandles;

namespace AgentDesk.Server.Usage;

public interface IAntigravityRunner
{
    Task<string?> RunAsync(CancellationToken cancellationToken = default);
}

public class ConPtyAntigravityRunner : IAntigravityRunner
{
    private readonly string _cli;
    private readonly TimeSpan _timeout;

    public ConPtyAntigravityRunner(string? cli = null, TimeSpan? timeout = null)
    {
        _cli = cli ?? Environment.GetEnvironmentVariable("AGENTDECK_ANTIGRAVITY_CLI") ?? "agy";
        _timeout = timeout ?? TimeSpan.FromSeconds(45);
    }

    public async Task<string?> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        return await Task.Run(() => RunInternal(cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    public static string ResolveExecutable(string cli)
    {
        if (File.Exists(cli)) return Path.GetFullPath(cli);

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var localAgy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "agy", "bin");
        if (!paths.Contains(localAgy, StringComparer.OrdinalIgnoreCase))
        {
            paths.Insert(0, localAgy);
        }

        var extensions = new[] { "", ".exe", ".cmd", ".bat" };
        foreach (var dir in paths)
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(dir, cli + ext);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }
        return cli;
    }

    public static (string applicationName, string commandLine) BuildCommandLine(string cli)
    {
        string resolvedExe = ResolveExecutable(cli);
        string ext = Path.GetExtension(resolvedExe).ToLowerInvariant();

        if (ext == ".cmd" || ext == ".bat")
        {
            string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string cmdPath = Path.Combine(string.IsNullOrEmpty(systemDir) ? @"C:\Windows\System32" : systemDir, "cmd.exe");
            return (cmdPath, $"\"{cmdPath}\" /c \"{resolvedExe}\"");
        }
        else
        {
            return (resolvedExe, $"\"{resolvedExe}\"");
        }
    }

    private string? RunInternal(CancellationToken cancellationToken)
    {
        SafeFileHandle? inputRead = null;
        SafeFileHandle? inputWrite = null;
        SafeFileHandle? outputRead = null;
        SafeFileHandle? outputWrite = null;
        IntPtr hPC = IntPtr.Zero;
        IntPtr attrList = IntPtr.Zero;
        IntPtr pEnv = IntPtr.Zero;
        IntPtr pCmdLine = IntPtr.Zero;
        IntPtr hJob = IntPtr.Zero;
        PROCESS_INFORMATION processInfo = default;

        try
        {
            var sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(), bInheritHandle = true };
            if (!CreatePipe(out inputRead, out inputWrite, ref sa, 0) ||
                !CreatePipe(out outputRead, out outputWrite, ref sa, 0))
            {
                return null;
            }

            // Ensure host side pipe handles are NOT inherited by child process
            SetHandleInformation(inputWrite, HANDLE_FLAG_INHERIT, 0);
            SetHandleInformation(outputRead, HANDLE_FLAG_INHERIT, 0);

            var size = new COORD(160, 80);
            int res = CreatePseudoConsole(size, inputRead, outputWrite, 0, out hPC);
            if (res != 0 || hPC == IntPtr.Zero)
            {
                return null;
            }

            // Pseudo-console holds inputRead and outputWrite. Close host copies immediately.
            inputRead.Close();
            inputRead = null;
            outputWrite.Close();
            outputWrite = null;

            IntPtr attrSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);
            attrList = Marshal.AllocHGlobal(attrSize);
            if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref attrSize))
            {
                return null;
            }

            if (!UpdateProcThreadAttribute(attrList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, hPC, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
            {
                return null;
            }

            var startupInfo = new STARTUPINFOEX();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            startupInfo.lpAttributeList = attrList;

            var envDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string k && entry.Value is string v)
                {
                    envDict[k] = v;
                }
            }
            envDict["AGY_CLI_HIDE_ACCOUNT_INFO"] = "1";
            pEnv = CreateUnicodeEnvironmentBlock(envDict);

            var (appName, cmdLine) = BuildCommandLine(_cli);
            pCmdLine = Marshal.StringToHGlobalUni(cmdLine);

            string cwd = Environment.GetEnvironmentVariable("AGENTDECK_ANTIGRAVITY_CWD") ?? Directory.GetCurrentDirectory();

            uint creationFlags = EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT;

            if (!CreateProcess(
                appName,
                pCmdLine,
                ref sa,
                ref sa,
                true,
                creationFlags,
                pEnv,
                cwd,
                ref startupInfo,
                out processInfo))
            {
                return null;
            }

            // Assign child process to a Win32 Job Object configured to kill the process tree on close
            hJob = CreateJobObject(IntPtr.Zero, null);
            if (hJob != IntPtr.Zero)
            {
                var extendedLimit = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };
                int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
                IntPtr pExtendedLimit = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(extendedLimit, pExtendedLimit, false);
                    SetInformationJobObject(hJob, JobObjectExtendedLimitInformation, pExtendedLimit, (uint)length);
                }
                finally
                {
                    Marshal.FreeHGlobal(pExtendedLimit);
                }
                AssignProcessToJobObject(hJob, processInfo.hProcess);
            }

            using var inputStream = new FileStream(inputWrite, FileAccess.Write, 4096, false);
            using var outputStream = new FileStream(outputRead, FileAccess.Read, 4096, false);

            void SendInput(string text)
            {
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(text);
                    inputStream.Write(bytes, 0, bytes.Length);
                    inputStream.Flush();
                }
                catch { }
            }

            var sb = new StringBuilder();
            var buffer = new byte[4096];

            bool trustSent = false;
            bool usageSent = false;
            var sw = Stopwatch.StartNew();
            var readyAfter = TimeSpan.FromSeconds(3);

            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var readTask = Task.Run(async () =>
            {
                try
                {
                    int bytesRead;
                    while ((bytesRead = await outputStream.ReadAsync(buffer, 0, buffer.Length, readCts.Token).ConfigureAwait(false)) > 0)
                    {
                        string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        lock (sb)
                        {
                            sb.Append(text);
                        }
                    }
                }
                catch
                {
                    // Stream closed or cancelled
                }
            }, readCts.Token);

            int lastLength = 0;
            int unchangedCount = 0;

            while (sw.Elapsed < _timeout && !cancellationToken.IsCancellationRequested)
            {
                string text;
                lock (sb)
                {
                    text = sb.ToString();
                }

                string plain = StripAnsi(text);

                if (!trustSent && plain.Contains("Do you trust the contents of this project?", StringComparison.OrdinalIgnoreCase))
                {
                    SendInput("\r");
                    trustSent = true;
                    readyAfter = sw.Elapsed + TimeSpan.FromSeconds(3);
                }

                if (!usageSent && sw.Elapsed >= readyAfter && (plain.Contains("? for shortcuts", StringComparison.OrdinalIgnoreCase) || sw.Elapsed >= TimeSpan.FromSeconds(6)))
                {
                    string tail = plain.Length > 2000 ? plain[^2000..] : plain;
                    if (!tail.Contains("Models & Quota", StringComparison.OrdinalIgnoreCase))
                    {
                        SendInput("/usage\r");
                        usageSent = true;
                    }
                }

                if (usageSent)
                {
                    var snapshot = AntigravityUsageParser.Parse(text);
                    if (snapshot != null)
                    {
                        if (text.Length == lastLength)
                        {
                            unchangedCount++;
                        }
                        else
                        {
                            unchangedCount = 0;
                            lastLength = text.Length;
                        }

                        // Settle condition: wait for output to stabilize (no changes for 400ms) after a valid parse.
                        // This allows secondary model groups (e.g. Claude/GPT) to render if present,
                        // while exiting promptly for single-group outputs.
                        if (unchangedCount >= 2)
                        {
                            break;
                        }
                    }
                }

                Thread.Sleep(200);
            }

            // Graceful shutdown of read loop
            readCts.Cancel();
            try
            {
                outputStream.Close();
            }
            catch { }

            try
            {
                readTask.Wait(500);
            }
            catch { }

            lock (sb)
            {
                return sb.ToString();
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (processInfo.hProcess != IntPtr.Zero)
            {
                TerminateProcess(processInfo.hProcess, 0);
                CloseHandle(processInfo.hProcess);
            }
            if (processInfo.hThread != IntPtr.Zero)
            {
                CloseHandle(processInfo.hThread);
            }
            if (hJob != IntPtr.Zero)
            {
                CloseHandle(hJob); // Terminates entire process tree safely
            }
            if (hPC != IntPtr.Zero)
            {
                ClosePseudoConsole(hPC);
            }
            if (attrList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }
            if (pEnv != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pEnv);
            }
            if (pCmdLine != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pCmdLine);
            }
            if (inputRead != null && !inputRead.IsClosed) inputRead.Close();
            if (outputWrite != null && !outputWrite.IsClosed) outputWrite.Close();
            if (inputWrite != null && !inputWrite.IsClosed) inputWrite.Close();
            if (outputRead != null && !outputRead.IsClosed) outputRead.Close();
        }
    }

    public static IntPtr CreateUnicodeEnvironmentBlock(IDictionary<string, string> environment)
    {
        var entries = new List<string>();
        foreach (var kvp in environment)
        {
            entries.Add($"{kvp.Key}={kvp.Value}");
        }
        entries.Sort(StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.Append(entry).Append('\0');
        }
        sb.Append('\0');

        byte[] bytes = Encoding.Unicode.GetBytes(sb.ToString());
        IntPtr pEnv = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, pEnv, bytes.Length);
        return pEnv;
    }

    private static string StripAnsi(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"\x1b(?:\[[0-?]*[ -/]*[@-~]|\][^\x07]*(?:\x07|\x1b\\))", "").Replace("\r", "");
    }

    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const long PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
    private const uint HANDLE_FLAG_INHERIT = 0x00000001;

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectExtendedLimitInformation = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
        public COORD(short x, short y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(SafeFileHandle hObject, uint dwMask, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint flags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        IntPtr lpCommandLine,
        ref SECURITY_ATTRIBUTES lpProcessAttributes,
        ref SECURITY_ATTRIBUTES lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);
}

