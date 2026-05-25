using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DrawThisEasy;

public partial class App : Application
{
    private const string CrashLogName = "DrawThisEasy-crash.log";

    public App()
    {
        // Catch anything that escapes — never just silently exit.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowCrash(e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) ShowCrash(ex);
    }

    private void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowCrash(e.Exception);
        e.SetObserved();
    }

    private static void ShowCrash(Exception ex)
    {
        var msg = $"{DateTime.Now:O}\n{ex}\n\n";
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DrawThisEasy", CrashLogName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, msg);
            MessageBox.Show(
                $"{ex.GetType().Name}: {ex.Message}\n\nFull details written to:\n{path}",
                "DrawThisEasy hit an error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            MessageBox.Show(ex.ToString(), "DrawThisEasy hit an error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
