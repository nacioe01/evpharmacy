
using EVPharmacy.Data.Services;
using EVPharmacy.Services;
using EVPharmacy.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace EVPharmacy;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UserSession _session;

    public MainWindow(
        MainViewModel viewModel,
        IServiceProvider serviceProvider,
        UserSession session)
    {
        InitializeComponent();

        _serviceProvider = serviceProvider;
        _session = session;

        DataContext = viewModel;
        viewModel.LogoutRequested += OnLogoutRequested;
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "Are you sure you want to log out?",
            "Log out",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        // Record the sign-out while the user is still known (fire and forget; never blocks logging out).
        _ = _serviceProvider.GetRequiredService<AuditService>()
            .LogEventAsync("Logout", $"{_session.CurrentUser?.Username} signed out",
                userId: _session.CurrentUser?.Id, username: _session.CurrentUser?.Username);

        _session.SignOut();
        Hide();

        var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();

        if (loginWindow.ShowDialog() == true)
        {
            Show();
            Activate();
        }
        else
        {
            Application.Current.Shutdown();
        }
    }
}
