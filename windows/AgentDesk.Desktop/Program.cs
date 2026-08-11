using System.Windows.Forms;

namespace AgentDesk.Desktop;

internal static class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    private static void Main(string[] args)
    {
        bool createdNew;
        try
        {
            _singleInstanceMutex = new Mutex(true, "Global\\AgentDesk.Desktop.SingleInstance", out createdNew);
        }
        catch
        {
            _singleInstanceMutex = new Mutex(true, "AgentDesk.Desktop.SingleInstance", out createdNew);
        }

        if (!createdNew)
        {
            MessageBox.Show(
                "AgentDesk Desktop is already running.",
                "AgentDesk Desktop",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            MessageBox.Show($"An unhandled thread exception occurred:\n{e.Exception.Message}", "AgentDesk Desktop Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"An unhandled domain exception occurred:\n{ex.Message}", "AgentDesk Desktop Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                }
                _singleInstanceMutex.Dispose();
            }
        }
    }
}
