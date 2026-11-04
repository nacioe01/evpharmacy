using EVPharmacy.Core.Security;
using EVPharmacy.Data.Services;
using System.Windows;

namespace EVPharmacy;

/// <summary>
/// Collects a new user's details, or a new password for an existing user.
/// Passwords stay in the window's code-behind (PasswordBox.Password is not bindable)
/// and are handed straight to the caller.
/// </summary>
public partial class UserDialogWindow : Window
{
    private readonly bool _isNewUser;

    private UserDialogWindow(bool isNewUser, string? existingUsername)
    {
        InitializeComponent();

        _isNewUser = isNewUser;

        HintText.Text = $"At least {PasswordPolicy.MinimumLength} characters, including a letter and a number.";

        if (isNewUser)
        {
            Title = "Add user";
            HeadingText.Text = "Add a user";
            SubText.Text = "Staff sign in with this username and password.";
            AcceptButton.Content = "Create user";
        }
        else
        {
            Title = "Reset password";
            HeadingText.Text = "Reset password";
            SubText.Text = $"Choose a new password for {existingUsername}. They will use it the next time they sign in.";
            AcceptButton.Content = "Save password";
            UsernamePanel.Visibility = Visibility.Collapsed;
            AdminCheck.Visibility = Visibility.Collapsed;
            PasswordLabel.Text = "New password";
        }

        UsernameBox.TextChanged += (_, _) => ErrorBox.Visibility = Visibility.Collapsed;
        PasswordBox.PasswordChanged += (_, _) => ErrorBox.Visibility = Visibility.Collapsed;
        ConfirmBox.PasswordChanged += (_, _) => ErrorBox.Visibility = Visibility.Collapsed;

        Loaded += (_, _) => (isNewUser ? (UIElement)UsernameBox : PasswordBox).Focus();
    }

    public string Username => UsernameBox.Text.Trim();

    public string Password => PasswordBox.Password;

    public bool IsAdministrator => AdminCheck.IsChecked == true;

    public static UserDialogWindow ForNewUser(Window? owner) =>
        new(true, null) { Owner = owner };

    public static UserDialogWindow ForPasswordReset(Window? owner, string username) =>
        new(false, username) { Owner = owner };

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        var error =
            (_isNewUser ? UserService.ValidateUsername(UsernameBox.Text) : null)
            ?? PasswordPolicy.Validate(PasswordBox.Password)
            ?? (PasswordBox.Password != ConfirmBox.Password ? "The two passwords do not match." : null);

        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorBox.Visibility = Visibility.Visible;
            return;
        }

        DialogResult = true;
    }
}
