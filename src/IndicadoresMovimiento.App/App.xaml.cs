using System.Windows;
using System.Windows.Threading;
using IndicadoresMovimiento.App.Services;

namespace IndicadoresMovimiento.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instance = new SingleInstance("IndicadoresMovimiento-8d2f4b61");
        if (!_instance.IsFirstInstance)
        {
            // Ya estaba abierta: que muestre sus ajustes y esta copia se cierra.
            _instance.SignalFirstInstance();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception) ErrorLog.Write(exception, "Error no controlado");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorLog.Write(args.Exception, "Error en una tarea en segundo plano");
            args.SetObserved();
        };

        var fromAutoStart = e.Args.Contains(AutoStart.StartupArgument, StringComparer.OrdinalIgnoreCase);
        _controller = new AppController(this);
        _instance.ListenForSignals(() => Dispatcher.BeginInvoke(() => _controller?.ShowSettings()));
        _controller.Start(fromAutoStart);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorLog.Write(e.Exception, "Error en la interfaz");
        e.Handled = true;
        MessageBox.Show(
            "Ha ocurrido un error inesperado, pero la aplicación sigue funcionando.\n\n" +
            $"{e.Exception.Message}\n\nHay más detalles en:\n{AppPaths.ErrorLogFile}",
            "Indicadores de movimiento", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
