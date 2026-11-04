
namespace EVPharmacy.ViewModels;

/// <summary>
/// Shown for modules that are planned but not yet implemented.
/// </summary>
public sealed class PlaceholderViewModel : PageViewModel
{
    public PlaceholderViewModel(
        string title,
        string subtitle,
        string glyph,
        string description,
        string plannedPhase)
        : base(title, subtitle)
    {
        Glyph = glyph;
        Description = description;
        PlannedPhase = plannedPhase;
    }

    public string Glyph { get; }

    public string Description { get; }

    public string PlannedPhase { get; }
}
