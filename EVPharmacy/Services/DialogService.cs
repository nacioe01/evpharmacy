using System.Windows;

namespace EVPharmacy.Services;

/// <summary>Small helper so view models can ask for input without referencing windows.</summary>
public class DialogService
{
    /// <summary>Shows a one-line text prompt. Returns null when cancelled.</summary>
    public string? PromptForText(
        string title,
        string label,
        string? initialValue = null,
        string? errorMessage = null)
    {
        var owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

        var window = new TextPromptWindow(title, label, initialValue, errorMessage)
        {
            Owner = owner
        };

        return window.ShowDialog() == true ? window.Value : null;
    }
}
