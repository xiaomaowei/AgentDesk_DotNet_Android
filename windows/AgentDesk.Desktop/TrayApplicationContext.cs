using System.Drawing;
using System.Windows.Forms;

namespace AgentDesk.Desktop;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon? _trayIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _restartMenuItem;
    private readonly ToolStripMenuItem _launchMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;

    private readonly ServerHostManager _serverHostManager;
    private readonly AdbManager _adbManager;
    private readonly string? _adbPath;

    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _monitorLock = new(1, 1);
    private string _currentAdbStatus = "ADB: Initializing";

    private readonly object _shutdownLock = new();
    private Task? _initTask;
    private Task? _monitorTask;
    private Task? _connectLaunchTask;
    private Task? _restartTask;
    private Task? _shutdownTask;
    private Task? _exitTask;
    private bool _isShutdownRequested;

    public bool IsShutdownRequested
    {
        get
        {
            lock (_shutdownLock)
            {
                return _isShutdownRequested;
            }
        }
    }

    public TrayApplicationContext(ServerHostManager? serverHostManager = null, AdbManager? adbManager = null)
    {
        _serverHostManager = serverHostManager ?? new ServerHostManager();
        _adbManager = adbManager ?? new AdbManager();
        _adbPath = _adbManager.DiscoverAdbPath();

        _statusMenuItem = new ToolStripMenuItem("Status: Initializing...") { Enabled = false };
        _restartMenuItem = new ToolStripMenuItem("Restart Server...", null, OnRestartServerClicked);
        _launchMenuItem = new ToolStripMenuItem("Connect & Launch Android", null, OnConnectAndLaunchClicked);
        _exitMenuItem = new ToolStripMenuItem("Exit", null, OnExitClicked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_restartMenuItem);
        menu.Items.Add(_launchMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitMenuItem);

        _trayIcon = LoadTrayIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "AgentDesk Desktop",
            ContextMenuStrip = menu,
            Visible = true
        };

        EventHandler? idleHandler = null;
        idleHandler = (sender, e) =>
        {
            Application.Idle -= idleHandler;
            lock (_shutdownLock)
            {
                if (_isShutdownRequested) return;
                _initTask = InitializeAndStartAsync();
            }
        };
        Application.Idle += idleHandler;
    }

    private async Task InitializeAndStartAsync()
    {
        try
        {
            await _serverHostManager.StartAsync(cancellationToken: _cts.Token).ConfigureAwait(true);
            if (!IsShutdownRequested)
            {
                UpdateStatus("Server: Running", _currentAdbStatus);
            }
        }
        catch (OperationCanceledException) when (IsShutdownRequested || _cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (!IsShutdownRequested)
            {
                UpdateStatus("Server: Failed to start", "ADB: Idle");
                MessageBox.Show($"Failed to start embedded AgentDesk.Server:\n{ex.Message}", "AgentDesk Server Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        lock (_shutdownLock)
        {
            if (!IsShutdownRequested)
            {
                _monitorTask = StartAdbMonitorLoopAsync(_cts.Token);
            }
        }
    }

    private async Task StartAdbMonitorLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!cancellationToken.IsCancellationRequested && !IsShutdownRequested)
        {
            try
            {
                if (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(true))
                {
                    if (IsShutdownRequested) break;
                    await PerformAdbCheckAsync(cancellationToken).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (IsShutdownRequested) break;
                _currentAdbStatus = $"ADB: Error ({ex.Message})";
                UpdateStatus(_serverHostManager.IsRunning ? "Server: Running" : "Server: Stopped", _currentAdbStatus);
            }
        }
    }

    private async Task PerformAdbCheckAsync(CancellationToken cancellationToken)
    {
        if (IsShutdownRequested) return;
        if (!await _monitorLock.WaitAsync(0, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            var adbStatus = await _adbManager.GetStatusAsync(_adbPath, cancellationToken).ConfigureAwait(true);
            _currentAdbStatus = $"ADB: {adbStatus}";

            if (!string.IsNullOrEmpty(_adbPath) && adbStatus.StartsWith("Connected", StringComparison.OrdinalIgnoreCase))
            {
                await _adbManager.MaintainReverseAsync(_adbPath, cancellationToken).ConfigureAwait(true);
            }

            if (!IsShutdownRequested)
            {
                var serverStatus = _serverHostManager.IsRunning ? "Server: Running" : "Server: Stopped";
                UpdateStatus(serverStatus, _currentAdbStatus);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!IsShutdownRequested)
            {
                _currentAdbStatus = $"ADB: Error ({ex.Message})";
                var serverStatus = _serverHostManager.IsRunning ? "Server: Running" : "Server: Stopped";
                UpdateStatus(serverStatus, _currentAdbStatus);
            }
        }
        finally
        {
            try
            {
                _monitorLock.Release();
            }
            catch
            {
            }
        }
    }

    private void UpdateStatus(string serverStatus, string adbStatus)
    {
        if (IsShutdownRequested) return;
        try
        {
            var text = $"{serverStatus} | {adbStatus}";
            _statusMenuItem.Text = text;
            _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
        }
        catch
        {
        }
    }

    private void OnRestartServerClicked(object? sender, EventArgs e)
    {
        if (IsShutdownRequested) return;

        var result = MessageBox.Show(
            "Restarting the server will clear all active sessions and pending approval states. Are you sure you want to restart?",
            "Restart AgentDesk Server",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes || IsShutdownRequested)
        {
            return;
        }

        lock (_shutdownLock)
        {
            if (IsShutdownRequested) return;
            _restartTask = ExecuteRestartAsync();
        }
    }

    private async Task ExecuteRestartAsync()
    {
        try
        {
            _restartMenuItem.Enabled = false;
            UpdateStatus("Server: Restarting...", _currentAdbStatus);
            await _serverHostManager.RestartAsync(cancellationToken: _cts.Token).ConfigureAwait(true);
            if (!IsShutdownRequested)
            {
                UpdateStatus("Server: Running", _currentAdbStatus);
            }
        }
        catch (OperationCanceledException) when (IsShutdownRequested || _cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!IsShutdownRequested)
            {
                UpdateStatus("Server: Failed to restart", _currentAdbStatus);
                MessageBox.Show($"Failed to restart server: {ex.Message}", "Restart Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (!IsShutdownRequested)
            {
                try
                {
                    _restartMenuItem.Enabled = true;
                }
                catch
                {
                }
            }
        }
    }

    private void OnConnectAndLaunchClicked(object? sender, EventArgs e)
    {
        if (IsShutdownRequested) return;

        if (string.IsNullOrEmpty(_adbPath))
        {
            MessageBox.Show("ADB executable was not found on this system. Please install Android Platform Tools.", "ADB Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        lock (_shutdownLock)
        {
            if (IsShutdownRequested) return;
            _connectLaunchTask = ExecuteConnectAndLaunchAsync();
        }
    }

    private async Task ExecuteConnectAndLaunchAsync()
    {
        try
        {
            _launchMenuItem.Enabled = false;
            UpdateStatus(_serverHostManager.IsRunning ? "Server: Running" : "Server: Stopped", "ADB: Launching...");
            await _adbManager.ConnectAndLaunchAsync(_adbPath!, _cts.Token).ConfigureAwait(true);
            if (!IsShutdownRequested)
            {
                await PerformAdbCheckAsync(_cts.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (IsShutdownRequested || _cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!IsShutdownRequested)
            {
                MessageBox.Show($"Failed to connect & launch Android app: {ex.Message}", "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (!IsShutdownRequested)
            {
                try
                {
                    _launchMenuItem.Enabled = true;
                }
                catch
                {
                }
            }
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        TriggerExit();
    }

    private Task EnsureShutdownAsync()
    {
        lock (_shutdownLock)
        {
            _isShutdownRequested = true;
            _shutdownTask ??= PerformShutdownAsync();
            return _shutdownTask;
        }
    }

    private async Task PerformShutdownAsync()
    {
        try
        {
            _restartMenuItem.Enabled = false;
            _launchMenuItem.Enabled = false;
            _exitMenuItem.Enabled = false;
        }
        catch
        {
        }

        try
        {
            _cts.Cancel();
        }
        catch
        {
        }

        try
        {
            _notifyIcon.Visible = false;
        }
        catch
        {
        }

        List<Task> tasksToWait = new();
        lock (_shutdownLock)
        {
            if (_initTask != null && !_initTask.IsCompleted) tasksToWait.Add(_initTask);
            if (_monitorTask != null && !_monitorTask.IsCompleted) tasksToWait.Add(_monitorTask);
            if (_connectLaunchTask != null && !_connectLaunchTask.IsCompleted) tasksToWait.Add(_connectLaunchTask);
            if (_restartTask != null && !_restartTask.IsCompleted) tasksToWait.Add(_restartTask);
        }

        if (tasksToWait.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasksToWait).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
            }
            catch
            {
            }
        }

        try
        {
            await _serverHostManager.StopAsync().ConfigureAwait(true);
            await _serverHostManager.DisposeAsync().ConfigureAwait(true);
        }
        catch
        {
        }

        DisposeTrayIcons();

        try
        {
            _monitorLock.Dispose();
        }
        catch
        {
        }

        try
        {
            _cts.Dispose();
        }
        catch
        {
        }
    }

    private int _trayIconsDisposed;

    private void DisposeTrayIcons()
    {
        if (Interlocked.Exchange(ref _trayIconsDisposed, 1) != 0) return;

        try
        {
            _notifyIcon.Dispose();
        }
        catch
        {
        }

        try
        {
            _trayIcon?.Dispose();
        }
        catch
        {
        }
    }

    private void TriggerExit()
    {
        lock (_shutdownLock)
        {
            _exitTask ??= CompleteExitAsync();
        }
    }

    private async Task CompleteExitAsync()
    {
        try
        {
            await EnsureShutdownAsync().ConfigureAwait(true);
        }
        catch
        {
        }
        finally
        {
            base.ExitThreadCore();
        }
    }

    protected override void ExitThreadCore()
    {
        Task task;
        lock (_shutdownLock)
        {
            if (_exitTask != null && _exitTask.IsCompleted)
            {
                base.ExitThreadCore();
                return;
            }

            _exitTask ??= CompleteExitAsync();
            task = _exitTask;
        }

        if (task.IsCompleted)
        {
            base.ExitThreadCore();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeTrayIcons();
        }
        base.Dispose(disposing);
    }

    public static Icon LoadTrayIcon(System.Reflection.Assembly? targetAssembly = null, string? baseDirectory = null)
    {
        try
        {
            var assembly = targetAssembly ?? typeof(TrayApplicationContext).Assembly;
            using var stream = assembly.GetManifestResourceStream("AgentDesk.Desktop.Assets.AgentDesk.ico");
            if (stream != null)
            {
                using var loadedIcon = new Icon(stream);
                return (Icon)loadedIcon.Clone();
            }

            var names = assembly.GetManifestResourceNames();
            var matchingName = names.FirstOrDefault(n => n.EndsWith("AgentDesk.ico", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matchingName))
            {
                using var stream2 = assembly.GetManifestResourceStream(matchingName);
                if (stream2 != null)
                {
                    using var loadedIcon2 = new Icon(stream2);
                    return (Icon)loadedIcon2.Clone();
                }
            }
        }
        catch
        {
            // Tolerate resource-load failure
        }

        try
        {
            var baseDir = baseDirectory ?? AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var filePath = Path.Combine(baseDir, "Assets", "AgentDesk.ico");
                if (File.Exists(filePath))
                {
                    using var fileIcon = new Icon(filePath);
                    return (Icon)fileIcon.Clone();
                }
            }
        }
        catch
        {
            // Tolerate file-load failure
        }

        try
        {
            var procPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
            {
                var assocIcon = Icon.ExtractAssociatedIcon(procPath);
                if (assocIcon != null)
                {
                    return assocIcon;
                }
            }
        }
        catch
        {
            // Tolerate process icon extraction failure
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
