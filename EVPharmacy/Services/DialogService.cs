using Microsoft.Win32;
using System.Windows;

namespace EVPharmacy.Services;

public sealed record NewUserDetails(string Username, string Password, bool IsAdministrator);

/// <summary>Small helper so view models can ask for input without referencing windows.</summary>
public class DialogService
{
    private static Window? Owner() =>
        Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

    /// <summary>Shows a one-line text prompt. Returns null when cancelled.</summary>
    public string? PromptForText(
        string title,
        string label,
        string? initialValue = null,
        string? errorMessage = null,
        string acceptText = "Add",
        int maxLength = 60)
    {
        var window = new TextPromptWindow(title, label, initialValue, errorMessage, acceptText, maxLength)
        {
            Owner = Owner()
        };

        return window.ShowDialog() == true ? window.Value : null;
    }

    /// <summary>Asks for a new user's username, password and role. Returns null when cancelled.</summary>
    public NewUserDetails? ShowNewUserDialog()
    {
        var window = UserDialogWindow.ForNewUser(Owner());

        return window.ShowDialog() == true
            ? new NewUserDetails(window.Username, window.Password, window.IsAdministrator)
            : null;
    }

    /// <summary>Asks for a new password for an existing user. Returns null when cancelled.</summary>
    public string? ShowResetPasswordDialog(string username)
    {
        var window = UserDialogWindow.ForPasswordReset(Owner(), username);
        return window.ShowDialog() == true ? window.Password : null;
    }

    public string? PickOpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog(Owner()) == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string title, string defaultFileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = defaultFileName,
            Filter = filter,
            OverwritePrompt = true
        };

        return dialog.ShowDialog(Owner()) == true ? dialog.FileName : null;
    }
}
