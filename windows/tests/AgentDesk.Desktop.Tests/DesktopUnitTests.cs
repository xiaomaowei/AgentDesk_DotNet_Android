using System.Net;
using System.Text.Json;
using AgentDesk.Desktop;
using Xunit;

namespace AgentDesk.Desktop.Tests;

public class MockAdbCommandRunner : IAdbCommandRunner
{
    public List<(string Path, List<string> Args)> ExecutionLog { get; } = new();
    public Func<string, List<string>, AdbCommandResult>? CommandHandler { get; set; }

    public Task<AdbCommandResult> RunAsync(string adbPath, IEnumerable<string> args, CancellationToken cancellationToken = default)
    {
        var argList = args.ToList();
        ExecutionLog.Add((adbPath, argList));

        if (CommandHandler != null)
        {
            return Task.FromResult(CommandHandler(adbPath, argList));
        }

        return Task.FromResult(new AdbCommandResult(0, string.Empty, string.Empty, TimedOut: false));
    }
}

public class DesktopUnitTests
{
    [Fact]
    public void ParseDevices_AcceptsDevice_IgnoresOfflineUnauthorizedAndHeader()
    {
        var manager = new AdbManager(new MockAdbCommandRunner());
        var sampleOutput = """
        List of devices attached
        * daemon not running; starting now at tcp:5037
        * daemon started successfully
        R3CT10XYZ123	device product:s5e3g model:SM_S918B device:s5e3g transport_id:1
        OFFLINE01	offline
        UNAUTH02	unauthorized
        EMU12345	device product:sdk_gphone64 model:gphone device:gphone transport_id:2
        """;

        var serials = manager.ParseDevices(sampleOutput);

        Assert.Equal(2, serials.Count);
        Assert.Contains("R3CT10XYZ123", serials);
        Assert.Contains("EMU12345", serials);
        Assert.DoesNotContain("OFFLINE01", serials);
        Assert.DoesNotContain("UNAUTH02", serials);
    }

    [Fact]
    public void IsReverseConfigured_ParsesReverseOutputCorrectly()
    {
        var manager = new AdbManager(new MockAdbCommandRunner());

        Assert.True(manager.IsReverseConfigured("(tcp:8765, tcp:8765)"));
        Assert.True(manager.IsReverseConfigured("host-18 tcp:8765 tcp:8765"));
        Assert.False(manager.IsReverseConfigured("host-18 tcp:9000 tcp:9000"));
        Assert.False(manager.IsReverseConfigured(""));
    }

    [Fact]
    public void DiscoverAdbPath_ReturnsCustomCandidateIfExists()
    {
        var manager = new AdbManager(new MockAdbCommandRunner());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);
        var fakeAdb = Path.Combine(tempDir, "adb.exe");
        File.WriteAllText(fakeAdb, "fake adb binary");

        try
        {
            var found = manager.DiscoverAdbPath(new[] { fakeAdb });
            Assert.NotNull(found);
            Assert.Equal(Path.GetFullPath(fakeAdb), found);
        }
        finally
        {
            if (File.Exists(fakeAdb)) File.Delete(fakeAdb);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task ConnectAndLaunchAsync_ExecutesExpectedPlanWithArgsSeparation()
    {
        var mockRunner = new MockAdbCommandRunner();
        mockRunner.CommandHandler = (path, args) =>
        {
            var argString = string.Join(" ", args);
            if (argString == "devices -l")
            {
                return new AdbCommandResult(0, "DEV001 device\n", "", false);
            }
            if (argString == "-s DEV001 reverse --list")
            {
                return new AdbCommandResult(0, "", "", false);
            }
            return new AdbCommandResult(0, "", "", false);
        };

        var manager = new AdbManager(mockRunner);
        await manager.ConnectAndLaunchAsync("adb.exe");

        Assert.Equal(5, mockRunner.ExecutionLog.Count);

        Assert.Equal(new[] { "start-server" }, mockRunner.ExecutionLog[0].Args);
        Assert.Equal(new[] { "devices", "-l" }, mockRunner.ExecutionLog[1].Args);
        Assert.Equal(new[] { "-s", "DEV001", "reverse", "--list" }, mockRunner.ExecutionLog[2].Args);
        Assert.Equal(new[] { "-s", "DEV001", "reverse", "tcp:8765", "tcp:8765" }, mockRunner.ExecutionLog[3].Args);
        Assert.Equal(new[] { "-s", "DEV001", "shell", "am", "start", "-n", "com.agentdeck.mobile/.MainActivity" }, mockRunner.ExecutionLog[4].Args);
    }

    [Fact]
    public async Task GetStatusAsync_FormatsStatusCorrectly_MissingAndNoDevices()
    {
        var mockRunner = new MockAdbCommandRunner();
        mockRunner.CommandHandler = (path, args) =>
        {
            var argString = string.Join(" ", args);
            if (argString == "devices -l")
            {
                return new AdbCommandResult(0, "List of devices attached\n", "", false);
            }
            return new AdbCommandResult(0, "", "", false);
        };

        var manager = new AdbManager(mockRunner);

        var missingStatus = await manager.GetStatusAsync(null);
        Assert.Equal("ADB missing", missingStatus);

        var noDevicesStatus = await manager.GetStatusAsync("adb.exe");
        Assert.Equal("No authorized devices", noDevicesStatus);
    }

    [Fact]
    public async Task EmbeddedServer_DynamicLoopbackPort_StartsAndStopsCleanly()
    {
        await using var manager = new ServerHostManager();

        await manager.StartAsync("http://127.0.0.1:0");
        Assert.True(manager.IsRunning);
        Assert.NotEmpty(manager.Addresses);

        var address = manager.Addresses.First();
        using var client = new HttpClient();
        var healthRes = await client.GetAsync($"{address}/health");

        Assert.Equal(HttpStatusCode.OK, healthRes.StatusCode);
        using var doc = JsonDocument.Parse(await healthRes.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());

        await manager.StopAsync();
        Assert.False(manager.IsRunning);
    }

    [Fact]
    public void BoundedStringBuilder_TruncatesOutputWhenExceedingMaxCapacity()
    {
        var builder = new BoundedStringBuilder(100);
        for (int i = 0; i < 20; i++)
        {
            builder.AppendLine("1234567890");
        }

        var result = builder.ToString();
        Assert.Contains("[...Output Truncated...]", result);
        Assert.True(result.Length <= 100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void BoundedStringBuilder_InvalidCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedStringBuilder(invalidCapacity));
    }

    [Fact]
    public void BoundedStringBuilder_ExactBoundary_DoesNotExceedCap()
    {
        var builder = new BoundedStringBuilder(10);
        builder.AppendLine("123456789"); // line length = 10 or 11 with newline, exceeds 10 or triggers truncation
        var result1 = builder.ToString();
        Assert.True(result1.Length <= 10);

        builder.AppendLine("abcdefghij");
        var result2 = builder.ToString();
        Assert.True(result2.Length <= 10);
    }

    [Fact]
    public void FormatConciseError_ExtractsFirstNonEmptyLine_ExcludesSecondLineSecret()
    {
        Assert.Equal("execution failed", AdbManager.FormatConciseError(null));
        Assert.Equal("execution failed", AdbManager.FormatConciseError("   \r\n  \n  "));

        var multilineWithSecret = "\r\n  error: device 'DEV001' not found\r\nSECRET_KEY=super_secret_token_12345\r\nTraceback...";
        var concise = AdbManager.FormatConciseError(multilineWithSecret);

        Assert.Equal("error: device 'DEV001' not found", concise);
        Assert.DoesNotContain("SECRET_KEY", concise);

        var longLine = new string('A', 300);
        var truncated = AdbManager.FormatConciseError(longLine);
        Assert.Equal(240, truncated.Length);
    }

    [Fact]
    public async Task ConnectAndLaunchAsync_LongMultilineError_ExcludesSecondLineSecretAndBounded()
    {
        var mockRunner = new MockAdbCommandRunner();
        mockRunner.CommandHandler = (path, args) =>
        {
            var argString = string.Join(" ", args);
            if (argString == "devices -l")
            {
                return new AdbCommandResult(0, "DEV001 device\n", "", false);
            }
            if (argString.Contains("am start"))
            {
                var multilineErr = "error: activity launch crashed\nSECRET_DATABASE_PASSWORD=qwerty12345\n" + new string('X', 500);
                return new AdbCommandResult(1, "", multilineErr, false);
            }
            return new AdbCommandResult(0, "", "", false);
        };

        var manager = new AdbManager(mockRunner);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ConnectAndLaunchAsync("adb.exe"));

        Assert.Contains("error: activity launch crashed", ex.Message);
        Assert.DoesNotContain("SECRET_DATABASE_PASSWORD", ex.Message);
        Assert.True(ex.Message.Length <= 260);
    }

    [Fact]
    public async Task ConnectAndLaunchAsync_NoAuthorizedDevices_ThrowsInvalidOperationException()
    {
        var mockRunner = new MockAdbCommandRunner();
        mockRunner.CommandHandler = (path, args) =>
        {
            var argString = string.Join(" ", args);
            if (argString == "devices -l")
            {
                return new AdbCommandResult(0, "List of devices attached\n", "", false);
            }
            return new AdbCommandResult(0, "", "", false);
        };

        var manager = new AdbManager(mockRunner);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ConnectAndLaunchAsync("adb.exe"));
        Assert.Contains("No authorized devices", ex.Message);
    }

    [Fact]
    public async Task ConnectAndLaunchAsync_ReverseNonZero_ThrowsInvalidOperationException()
    {
        var mockRunner = new MockAdbCommandRunner();
        mockRunner.CommandHandler = (path, args) =>
        {
            var argString = string.Join(" ", args);
            if (argString == "devices -l")
            {
                return new AdbCommandResult(0, "DEV001 device\n", "", false);
            }
            if (argString.Contains("reverse tcp:8765 tcp:8765"))
            {
                return new AdbCommandResult(1, "", "cannot bind port", false);
            }
            return new AdbCommandResult(0, "", "", false);
        };

        var manager = new AdbManager(mockRunner);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ConnectAndLaunchAsync("adb.exe"));
        Assert.Contains("ADB reverse failed", ex.Message);
    }

    [Fact]
    public async Task ConnectAndLaunchAsync_LaunchNonZero_ThrowsInvalidOperationException()
    {
        var mockRunner = new MockAdbCommandRunner();
        mockRunner.CommandHandler = (path, args) =>
        {
            var argString = string.Join(" ", args);
            if (argString == "devices -l")
            {
                return new AdbCommandResult(0, "DEV001 device\n", "", false);
            }
            if (argString.Contains("am start"))
            {
                return new AdbCommandResult(1, "", "Activity not found", false);
            }
            return new AdbCommandResult(0, "", "", false);
        };

        var manager = new AdbManager(mockRunner);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ConnectAndLaunchAsync("adb.exe"));
        Assert.Contains("ADB launch failed", ex.Message);
    }

    [Fact]
    public async Task ServerHostManager_StartAsync_BusyPort_IsRunningIsFalse()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int busyPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        await using var manager = new ServerHostManager();
        await Assert.ThrowsAsync<System.IO.IOException>(() => manager.StartAsync($"http://127.0.0.1:{busyPort}"));

        Assert.False(manager.IsRunning);
    }

    [Fact]
    public async Task TrayApplicationContext_ExitThreadCore_CompletesShutdownAsyncCleanly()
    {
        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var syncContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(syncContext);

            var mockServerHostManager = new ServerHostManager();
            var mockAdbRunner = new MockAdbCommandRunner();
            var mockAdbManager = new AdbManager(mockAdbRunner);

            var context = new TrayApplicationContext(mockServerHostManager, mockAdbManager);

            var exitMethod = typeof(TrayApplicationContext).GetMethod("ExitThreadCore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            exitMethod?.Invoke(context, null);

            tcs.SetResult();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        thread.Join(1000);
    }

    [Fact]
    public async Task GetStatusAsync_DaemonStartupInStderrWithValidDevices_ReturnsConnected()
    {
        var mockRunner = new MockAdbCommandRunner();
        mockRunner.CommandHandler = (path, args) =>
        {
            var argString = string.Join(" ", args);
            if (argString == "devices -l")
            {
                var stdout = "List of devices attached\nRFCW607NEMH device product:r8q model:SM_G990N device:r8q transport_id:1\n";
                var stderr = "* daemon not running; starting now at tcp:5037\n* daemon started successfully\n";
                return new AdbCommandResult(0, stdout, stderr, TimedOut: false);
            }
            return new AdbCommandResult(0, "", "", TimedOut: false);
        };

        var manager = new AdbManager(mockRunner);
        var status = await manager.GetStatusAsync("adb.exe");

        Assert.Equal("Connected (1 device)", status);
    }

    [Fact]
    public async Task GetStatusAsync_NonZeroExitWithDaemonStartupAndError_ReturnsConciseRealError()
    {
        var mockRunner = new MockAdbCommandRunner();
        mockRunner.CommandHandler = (path, args) =>
        {
            var argString = string.Join(" ", args);
            if (argString == "devices -l")
            {
                var stderr = "* daemon not running; starting now at tcp:5037\n* daemon started successfully\nerror: cannot connect to daemon\n";
                return new AdbCommandResult(1, "", stderr, TimedOut: false);
            }
            return new AdbCommandResult(0, "", "", TimedOut: false);
        };

        var manager = new AdbManager(mockRunner);
        var status = await manager.GetStatusAsync("adb.exe");

        Assert.Equal("ADB error: error: cannot connect to daemon", status);
    }

    [Fact]
    public async Task GetStatusAsync_TimedOut_ReturnsConciseTimeoutError()
    {
        var mockRunner = new MockAdbCommandRunner();
        mockRunner.CommandHandler = (path, args) =>
        {
            return new AdbCommandResult(-1, "", "", TimedOut: true);
        };

        var manager = new AdbManager(mockRunner);
        var status = await manager.GetStatusAsync("adb.exe");

        Assert.Equal("ADB error: command timed out", status);
    }

    [Fact]
    public void LoadTrayIcon_LoadsEmbeddedIcon_ReturnsNonNullIcon()
    {
        using var icon = TrayApplicationContext.LoadTrayIcon();

        Assert.NotNull(icon);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
    }

    [Fact]
    public void LoadTrayIcon_FallbackWhenResourceMissing_ReturnsNonNullIcon()
    {
        using var icon = TrayApplicationContext.LoadTrayIcon(typeof(object).Assembly, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n")));

        Assert.NotNull(icon);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
    }
}
