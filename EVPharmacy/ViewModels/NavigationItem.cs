
namespace EVPharmacy.ViewModels;

public static class PageKeys
{
    public const string Dashboard = "dashboard";
    public const string PointOfSale = "pos";
    public const string Medicines = "medicines";
    public const string Inventory = "inventory";
    public const string Suppliers = "suppliers";
    public const string Reports = "reports";
    public const string Users = "users";
    public const string Settings = "settings";
}

/// <summary>
/// A sidebar entry. <see cref="Section"/>, when set, is shown as a small
/// heading above the item to group related modules.
/// </summary>
public sealed class NavigationItem
{
    public NavigationItem(
        string key,
        string title,
        string glyph,
        Func<PageViewModel> createPage,
        string? section = null)
    {
        Key = key;
        Title = title;
        Glyph = glyph;
        CreatePage = createPage;
        Section = section;
    }

    public string Key { get; }

    public string Title { get; }

    /// <summary>Segoe MDL2 Assets glyph.</summary>
    public string Glyph { get; }

    public string? Section { get; }

    public bool HasSection => !string.IsNullOrEmpty(Section);

    public Func<PageViewModel> CreatePage { get; }
}
