using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AgentDesk.Desktop;

public class DashboardForm : Form
{
    private WebView2? _webView;
    private readonly Uri _navigateUri;

    public DashboardForm(Screen targetScreen, Uri navigateUri)
    {
        ArgumentNullException.ThrowIfNull(targetScreen);
        _navigateUri = navigateUri ?? throw new ArgumentNullException(nameof(navigateUri));

        Text = "AgentDesk Dashboard";
        Icon = TrayApplicationContext.LoadTrayIcon();
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = targetScreen.Bounds;
        TopMost = false;
        ShowInTaskbar = true;

        InitializeWebView();
    }

    private void InitializeWebView()
    {
        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };
        Controls.Add(_webView);

        Load += async (_, _) =>
        {
            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AgentDesk",
                    "WebView2");

                Directory.CreateDirectory(userDataFolder);
                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder).ConfigureAwait(true);

                if (_webView != null && !IsDisposed)
                {
                    await _webView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
                    _webView.Source = _navigateUri;
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    MessageBox.Show(
                        $"Failed to initialize WebView2 dashboard:\n{ex.Message}",
                        "Secondary Display Dashboard Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    Close();
                }
            }
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_webView != null)
            {
                try
                {
                    _webView.Dispose();
                }
                catch
                {
                }
                _webView = null;
            }
        }
        base.Dispose(disposing);
    }
}
