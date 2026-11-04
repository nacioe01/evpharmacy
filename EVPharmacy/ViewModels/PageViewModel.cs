
using EVPharmacy.Mvvm;

namespace EVPharmacy.ViewModels;

/// <summary>
/// Base class for pages hosted in the main window content area.
/// </summary>
public abstract class PageViewModel : ObservableObject
{
    protected PageViewModel(string title, string subtitle)
    {
        Title = title;
        Subtitle = subtitle;
    }

    public string Title { get; }

    public string Subtitle { get; }
}
