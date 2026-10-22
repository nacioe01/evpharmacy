
using EVPharmacy.Data.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static EVPharmacy.Data.Services.UserService;

namespace EVPharmacy;

public partial class LoginWindow : Window
{
    private readonly UserService _userService;

    public string AuthenticatedUsername { get; private set; } = string.Empty;

    public LoginWindow(UserService userService)
    {
        InitializeComponent();
        _userService = userService;

        UsernameTextBox.TextChanged += Input_TextChanged;
        PasswordInput.PasswordChanged += Input_TextChanged;

        UpdateLoginButton();
    }

    private void Input_TextChanged(object sender, RoutedEventArgs e)
    {
        UpdateLoginButton();
        StatusTextBlock.Text = string.Empty;
    }

    private void UpdateLoginButton()
    {
        LoginButton.IsEnabled =
            !string.IsNullOrWhiteSpace(UsernameTextBox.Text) &&
            !string.IsNullOrWhiteSpace(PasswordInput.Password);
    }

    private async void LoginButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        StatusTextBlock.Text = string.Empty;

        try
        {
            var result = await _userService.AuthenticateAsync(
                UsernameTextBox.Text,
                PasswordInput.Password);

            switch (result)
            {
                case AuthenticationResult.Success:
                    AuthenticatedUsername = UsernameTextBox.Text.Trim();
                    DialogResult = true;
                    break;

                case AuthenticationResult.AccountDeactivated:
                    StatusTextBlock.Text =
                        "Your account has been deactivated.";
                    break;

                default:
                    StatusTextBlock.Text =
                        "Invalid username or password.";
                    break;
            }
        }
        catch (Exception)
        {
            StatusTextBlock.Text =
                "Unable to log in. Please try again.";
        }
        finally
        {
            if (IsLoaded)
            {
                UpdateLoginButton();
            }

            UpdateLoginButton();
        }
    }
}
