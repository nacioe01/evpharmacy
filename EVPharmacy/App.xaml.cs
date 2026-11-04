
using EVPharmacy.Data.Database;
using EVPharmacy.Data.Services;
using EVPharmacy.Services;
using EVPharmacy.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EVPharmacy;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        RegisterGlobalExceptionHandlers();

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) => ConfigureServices(services))
                .Build();

            await _host.StartAsync();

            // Back up (if needed) and apply pending migrations before any data access.
            await _host.Services
                .GetRequiredService<DatabaseInitializer>()
                .InitializeAsync();

            var userService = _host.Services.GetRequiredService<UserService>();

            if (!await userService.AnyUsersExistAsync())
            {
                var setupWindow = _host.Services.GetRequiredService<SetupAdminWindow>();

                if (setupWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            var loginWindow = _host.Services.GetRequiredService<LoginWindow>();

            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception ex)
        {
            var logPath = ErrorLogger.Log(ex, "Startup failed");

            MessageBox.Show(
                "EV Pharmacy could not start." +
                (logPath is null ? string.Empty : $"\n\nDetails were saved to:\n{logPath}"),
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // A factory gives each operation its own short-lived DbContext,
        // avoiding stale data and change-tracker growth in a long-running app.
        services.AddDbContextFactory<AppDbContext>(AppDatabase.Configure);

        services.AddSingleton<DatabaseBackupService>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<UserService>();
        services.AddSingleton<LookupService>();
        services.AddSingleton<MedicineService>();
        services.AddSingleton<UserSession>();
        services.AddSingleton<DialogService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<MedicinesViewModel>();

        services.AddTransient<SetupAdminWindow>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                ErrorLogger.Log(ex, "Unhandled exception (AppDomain)");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorLogger.Log(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        var logPath = ErrorLogger.Log(e.Exception, "Unhandled UI exception");

        MessageBox.Show(
            "Something went wrong, but your work has not been lost. Please try the action again." +
            (logPath is null ? string.Empty : $"\n\nDetails were saved to:\n{logPath}"),
            "Unexpected error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        // Keep the application running.
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
