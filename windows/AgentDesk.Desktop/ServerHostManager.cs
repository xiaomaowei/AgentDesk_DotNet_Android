using AgentDesk.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace AgentDesk.Desktop;

public class ServerHostManager : IAsyncDisposable
{
    private WebApplication? _app;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isDisposed;

    public bool IsRunning => _app != null;

    public IReadOnlyCollection<string> Addresses
    {
        get
        {
            if (_app == null) return Array.Empty<string>();
            var server = _app.Services.GetService<IServer>();
            var feature = server?.Features.Get<IServerAddressesFeature>();
            return feature?.Addresses.ToList() ?? _app.Urls.ToList();
        }
    }

    public async Task StartAsync(string? urlOverride = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_app != null)
            {
                return;
            }

            var app = AgentDeskServer.Build(Array.Empty<string>(), urlOverride);
            try
            {
                await app.StartAsync(cancellationToken).ConfigureAwait(false);
                _app = app;
            }
            catch
            {
                try
                {
                    using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await app.StopAsync(stopCts.Token).ConfigureAwait(false);
                }
                catch
                {
                }
                try
                {
                    await app.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                }
                _app = null;
                throw;
            }
        }
        finally
        {
            if (!_isDisposed)
            {
                _lock.Release();
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed) return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!_isDisposed)
            {
                _lock.Release();
            }
        }
    }

    private async Task StopInternalAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        _app = null;

        if (app != null)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                await app.StopAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch
            {
            }
            finally
            {
                try
                {
                    await app.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }
    }

    public async Task RestartAsync(string? urlOverride = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopInternalAsync(cancellationToken).ConfigureAwait(false);

            var app = AgentDeskServer.Build(Array.Empty<string>(), urlOverride);
            try
            {
                await app.StartAsync(cancellationToken).ConfigureAwait(false);
                _app = app;
            }
            catch
            {
                try
                {
                    using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await app.StopAsync(stopCts.Token).ConfigureAwait(false);
                }
                catch
                {
                }
                try
                {
                    await app.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                }
                _app = null;
                throw;
            }
        }
        finally
        {
            if (!_isDisposed)
            {
                _lock.Release();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isDisposed) return;
            _isDisposed = true;

            await StopInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
