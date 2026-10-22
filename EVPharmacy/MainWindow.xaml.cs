
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace EVPharmacy;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    public string CurrentUsername
    {
        get => UsernameTextBlock.Text;
        set
        {
            UsernameTextBlock.Text = value;
            UserInitialTextBlock.Text =
                string.IsNullOrWhiteSpace(value)
                    ? "U"
                    : value[..1].ToUpperInvariant();
        }
    }

    private void InventoryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            "The Inventory module will be implemented next.",
            "Inventory",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }


    private void LogoutButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to log out?",
            "Confirm Logout",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Hide();

        var loginWindow =
            _serviceProvider.GetRequiredService<LoginWindow>();

        var loginResult = loginWindow.ShowDialog();

        if (loginResult == true)
        {
            CurrentUsername = loginWindow.AuthenticatedUsername;
            Show();
        }
        else
        {
            Application.Current.Shutdown();
        }
    }

}
