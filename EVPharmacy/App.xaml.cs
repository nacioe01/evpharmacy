
using EVPharmacy.Data.Database;
using EVPharmacy.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

namespace EVPharmacy;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EVPharmacy");

        Directory.CreateDirectory(appDataPath);

        var dbPath = Path.Combine(appDataPath, "pharmacy.db");
        var connectionString = $"Data Source={dbPath}";

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite(connectionString));

                services.AddScoped<UserService>();

                services.AddTransient<SetupAdminWindow>();
                services.AddTransient<LoginWindow>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        bool hasUsers;

        using (var scope = _host.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            hasUsers = await dbContext.Users.AnyAsync();
        }

        if (!hasUsers)
        {
            using var scope = _host.Services.CreateScope();

            var setupWindow =
                scope.ServiceProvider.GetRequiredService<SetupAdminWindow>();

            var setupResult = setupWindow.ShowDialog();

            if (setupResult != true)
            {
                Shutdown();
                return;
            }
        }

        var loginWindow =
            _host.Services.GetRequiredService<LoginWindow>();

        var loginResult = loginWindow.ShowDialog();

        if (loginResult != true)
        {
            Shutdown();
            return;
        }

        var mainWindow =
            _host.Services.GetRequiredService<MainWindow>();

        mainWindow.CurrentUsername =
            loginWindow.AuthenticatedUsername;

        Application.Current.MainWindow = mainWindow;
        mainWindow.Show();

        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
