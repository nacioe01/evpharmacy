
using EVPharmacy.Core.Security;
using EVPharmacy.Data.Services;
using EVPharmacy.Services;
using System.Windows;

namespace EVPharmacy;

public partial class SetupAdminWindow : Window
{
    private readonly UserService _userService;
    private bool _isBusy;

    public SetupAdminWindow(UserService userService)
    {
        InitializeComponent();
        _userService = userService;

        PasswordHintTextBlock.Text =
            $"At least {PasswordPolicy.MinimumLength} characters, including a letter and a number.";

        UsernameTextBox.TextChanged += (_, _) => HideError();
        PasswordInput.PasswordChanged += (_, _) => HideError();
        ConfirmPasswordInput.PasswordChanged += (_, _) => HideError();
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorBanner.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorBanner.Visibility = Visibility.Collapsed;
    }

    private string? Validate(string username, string password, string confirmPassword)
    {
        return UserService.ValidateUsername(username)
               ?? PasswordPolicy.Validate(password)
               ?? (password != confirmPassword ? "Passwords do not match." : null);
    }

    private async void CreateAdminButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var username = UsernameTextBox.Text.Trim();
        var password = PasswordInput.Password;
        var confirmPassword = ConfirmPasswordInput.Password;

        var error = Validate(username, password, confirmPassword);
        if (error is not null)
        {
            ShowError(error);
            return;
        }

        _isBusy = true;
        CreateButton.IsEnabled = false;

        try
        {
            var created = await _userService.CreateAdminAsync(username, password);

            if (!created)
            {
                ShowError("That username is already taken. Please choose another.");
                return;
            }

            MessageBox.Show(
                this,
                "Administrator account created. You can now sign in.",
                "Setup complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            // Validation messages from the service are safe to display.
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Create administrator failed");
            ShowError("The administrator account could not be created. Please try again.");
        }
        finally
        {
            _isBusy = false;
            if (IsLoaded)
            {
                CreateButton.IsEnabled = true;
            }
        }
    }
}
