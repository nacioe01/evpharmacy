
using EVPharmacy.Mvvm;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed record DashboardMetric(
    string Label,
    string Value,
    string Caption,
    string Glyph);

public sealed class DashboardViewModel : PageViewModel
{
    public DashboardViewModel(string username, Action<string> navigate)
        : base("Dashboard", "Overview of today's pharmacy operations")
    {
        Greeting = $"{GetGreeting(DateTime.Now)}, {username}";
        TodayText = DateTime.Now.ToString("dddd, MMMM d, yyyy");

        // Live values arrive with the Inventory and POS modules.
        const string pending = "Available once inventory is set up";

        Metrics =
        [
            new DashboardMetric("Today's sales", "—", "Available once POS is set up", ""),
            new DashboardMetric("Medicines", "—", pending, ""),
            new DashboardMetric("Low stock", "—", pending, ""),
            new DashboardMetric("Expiring in 90 days", "—", pending, "")
        ];

        NewSaleCommand = new RelayCommand(() => navigate(PageKeys.PointOfSale));
        ReceiveStockCommand = new RelayCommand(() => navigate(PageKeys.Inventory));
        AddMedicineCommand = new RelayCommand(() => navigate(PageKeys.Medicines));
    }

    public string Greeting { get; }

    public string TodayText { get; }

    public IReadOnlyList<DashboardMetric> Metrics { get; }

    public ICommand NewSaleCommand { get; }

    public ICommand ReceiveStockCommand { get; }

    public ICommand AddMedicineCommand { get; }

    private static string GetGreeting(DateTime now) => now.Hour switch
    {
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening"
    };
}
