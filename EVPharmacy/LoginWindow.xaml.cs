
using EVPharmacy.Data.Services;
using EVPharmacy.Services;
using System.Windows;
using System.Windows.Input;

namespace EVPharmacy;

// Login stays in code-behind: PasswordBox.Password is intentionally not bindable,
// and this keeps the password out of view-model properties.
public partial class LoginWindow : Window
{
    private readonly UserService _userService;
    private readonly UserSession _session;
    private bool _isBusy;

    public LoginWindow(UserService userService, UserSession session)
    {
        InitializeComponent();

        _userService = userService;
        _session = session;

        UsernameTextBox.TextChanged += (_, _) => OnInputChanged();
        PasswordInput.PasswordChanged += (_, _) => OnInputChanged();
        PreviewKeyDown += (_, _) => UpdateCapsLockWarning();
        PasswordInput.GotKeyboardFocus += (_, _) => UpdateCapsLockWarning();
        PasswordInput.LostKeyboardFocus += (_, _) => CapsLockWarning.Visibility = Visibility.Collapsed;

        UpdateLoginButton();
    }

    private void OnInputChanged()
    {
        HideError();
        UpdateLoginButton();
    }

    private void UpdateLoginButton()
    {
        LoginButton.IsEnabled =
            !_isBusy &&
            !string.IsNullOrWhiteSpace(UsernameTextBox.Text) &&
            !string.IsNullOrWhiteSpace(PasswordInput.Password);
    }

    private void UpdateCapsLockWarning()
    {
        CapsLockWarning.Visibility =
            PasswordInput.IsKeyboardFocused && Keyboard.IsKeyToggled(Key.CapsLock)
                ? Visibility.Visible
                : Visibility.Collapsed;
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

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        UpdateLoginButton();
        HideError();
        LoginButton.Content = "Signing in…";

        try
        {
            var outcome = await _userService.AuthenticateAsync(
                UsernameTextBox.Text,
                PasswordInput.Password);

            switch (outcome.Result)
            {
                case AuthenticationResult.Success when outcome.User is not null:
                    _session.SignIn(outcome.User);
                    DialogResult = true;
                    return;

                case AuthenticationResult.AccountDeactivated:
                    ShowError("This account has been deactivated. Please contact your administrator.");
                    break;

                default:
                    // Clear first: PasswordChanged hides any visible error.
                    PasswordInput.Clear();
                    PasswordInput.Focus();
                    ShowError("Incorrect username or password.");
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Login failed");
            ShowError("Unable to sign in right now. Please try again.");
        }
        finally
        {
            _isBusy = false;
            if (IsLoaded)
            {
                LoginButton.Content = "Sign in";
                UpdateLoginButton();
            }
        }
    }
}
