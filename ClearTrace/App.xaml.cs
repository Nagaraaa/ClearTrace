using System.Windows;

namespace ClearTrace;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, @"Local\ClearTrace.SingleInstance", out var createdNew);
        _ownsInstanceMutex = createdNew;
        if (!createdNew)
        {
            MessageBox.Show("ClearTrace est déjà ouvert.", "ClearTrace", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            ApplicationLog.WriteException("Unhandled UI exception", args.Exception);
            MessageBox.Show("ClearTrace a rencontré une erreur inattendue. Les détails ont été enregistrés dans le journal de diagnostic.", "ClearTrace", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => ApplicationLog.WriteException("Unhandled application exception", args.ExceptionObject as Exception ?? new InvalidOperationException(args.ExceptionObject.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) => { ApplicationLog.WriteException("Unobserved task exception", args.Exception); args.SetObserved(); };
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsInstanceMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
