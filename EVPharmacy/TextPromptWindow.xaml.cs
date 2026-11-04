using System.Windows;

namespace EVPharmacy;

public partial class TextPromptWindow : Window
{
    public TextPromptWindow(
        string title, string label, string? initialValue, string? errorMessage,
        string acceptText = "Add", int maxLength = 60)
    {
        InitializeComponent();

        Title = title;
        LabelText.Text = label;
        AcceptButton.Content = acceptText;
        InputBox.MaxLength = maxLength;
        InputBox.Text = initialValue ?? string.Empty;

        if (!string.IsNullOrEmpty(errorMessage))
        {
            ErrorText.Text = errorMessage;
            ErrorBox.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string Value => InputBox.Text.Trim();

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
}
