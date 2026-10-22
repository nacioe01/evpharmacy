
using System.Windows;
using EVPharmacy.Data.Services;

namespace EVPharmacy;

public partial class SetupAdminWindow : Window
{
    private readonly UserService _userService;

    public SetupAdminWindow(UserService userService)
    {
        InitializeComponent();
        _userService = userService;
    }

    private async void CreateAdminButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordInput.Password;
        var confirmPassword = ConfirmPasswordInput.Password;

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(
                "Username and password are required.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (password != confirmPassword)
        {
            MessageBox.Show(
                "Passwords do not match.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var created = await _userService.CreateAdminAsync(
                username,
                password);

            if (!created)
            {
                MessageBox.Show(
                    "That username already exists. Please choose another.",
                    "Username Already Exists",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                "Administrator account created successfully.",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to create the administrator account.\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
