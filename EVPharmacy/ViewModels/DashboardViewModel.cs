using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed record DashboardMetric(
    string Label,
    string Value,
    string Caption,
    string Glyph);

/// <summary>One line in the dashboard alerts card.</summary>
public sealed record AlertRow(string Label, string Kind, string Title, string Detail, int Severity);

public sealed class DashboardViewModel : PageViewModel
{
    private const int MaxAlerts = 10;

    private readonly InventoryService _inventory;

    private bool _isLoaded;
    private string _alertSummary = "Low-stock and expiration warnings.";

    public DashboardViewModel(string username, Action<string> navigate, InventoryService inventory)
        : base("Dashboard", "Overview of today's pharmacy operations")
    {
        _inventory = inventory;

        Greeting = $"{GetGreeting(DateTime.Now)}, {username}";
        TodayText = DateTime.Now.ToString("dddd, MMMM d, yyyy");

        Metrics =
        [
            new DashboardMetric("Today's sales", "—", "Available once POS is set up", ""),
            new DashboardMetric("Medicines", "…", "Active products", ""),
            new DashboardMetric("Low stock", "…", "At or below reorder level", ""),
            new DashboardMetric("Expiring in 90 days", "…", "Medicines with stock expiring", "")
        ];

        NewSaleCommand = new RelayCommand(() => navigate(PageKeys.PointOfSale));
        ReceiveStockCommand = new RelayCommand(() => navigate(PageKeys.Inventory));
        AddMedicineCommand = new RelayCommand(() => navigate(PageKeys.Medicines));
        ViewInventoryCommand = new RelayCommand(() => navigate(PageKeys.Inventory));

        _ = LoadAsync();
    }

    public string Greeting { get; }

    public string TodayText { get; }

    public ObservableCollection<DashboardMetric> Metrics { get; }

    public ObservableCollection<AlertRow> Alerts { get; } = [];

    public ICommand NewSaleCommand { get; }

    public ICommand ReceiveStockCommand { get; }

    public ICommand AddMedicineCommand { get; }

    public ICommand ViewInventoryCommand { get; }

    public bool HasAlerts => Alerts.Count > 0;

    /// <summary>True once loaded with nothing to report.</summary>
    public bool IsAllClear => _isLoaded && Alerts.Count == 0;

    public string AlertSummary
    {
        get => _alertSummary;
        private set => SetProperty(ref _alertSummary, value);
    }

    private async Task LoadAsync()
    {
        try
        {
            var stock = await _inventory.GetStockSummaryAsync();
            var batches = await _inventory.GetBatchesAsync();

            var today = InventoryService.Today;
            var alerts = new List<AlertRow>();

            foreach (var b in batches)
            {
                var name = b.BrandName is null ? b.GenericName : $"{b.GenericName} ({b.BrandName})";
                var days = b.ExpiryDate.DayNumber - today.DayNumber;
                var qty = $"{b.QuantityOnHand:N0} left";

                if (b.Status == BatchStatus.Recalled)
                {
                    alerts.Add(new AlertRow("Recalled", StatusKind.Bad, name,
                        $"Batch {b.BatchNumber} · {qty} · {b.StatusNote}", 1));
                }
                else if (days < 0)
                {
                    alerts.Add(new AlertRow("Expired", StatusKind.Bad, name,
                        $"Batch {b.BatchNumber} · {qty} · expired {-days} day{(days == -1 ? "" : "s")} ago", 0));
                }
                else if (b.Status == BatchStatus.Quarantined)
                {
                    alerts.Add(new AlertRow("On hold", StatusKind.Warn, name,
                        $"Batch {b.BatchNumber} · {qty} · {b.StatusNote}", 3));
                }
                else if (days <= InventoryService.ExpiringSoonDays)
                {
                    alerts.Add(new AlertRow("Expiring soon", StatusKind.Warn, name,
                        $"Batch {b.BatchNumber} · {qty} · expires in {days} day{(days == 1 ? "" : "s")}", 4));
                }
            }

            foreach (var s in stock.Where(s => s.IsLowStock))
            {
                var name = s.BrandName is null ? s.GenericName : $"{s.GenericName} ({s.BrandName})";
                var out_ = s.SellableQuantity <= 0;

                alerts.Add(new AlertRow(
                    out_ ? "Out of stock" : "Low stock",
                    out_ ? StatusKind.Bad : StatusKind.Warn,
                    name,
                    $"{s.SellableQuantity:N0} {s.UnitName.ToLowerInvariant()} left · reorder at {s.ReorderLevel:N0}",
                    out_ ? 2 : 3));
            }

            var ordered = alerts.OrderBy(a => a.Severity).ThenBy(a => a.Title).ToList();

            Alerts.Clear();
            foreach (var alert in ordered.Take(MaxAlerts))
            {
                Alerts.Add(alert);
            }

            AlertSummary = ordered.Count == 0
                ? "Low-stock and expiration warnings."
                : ordered.Count > MaxAlerts
                    ? $"Showing the {MaxAlerts} most urgent of {ordered.Count} alerts."
                    : $"{ordered.Count} item{(ordered.Count == 1 ? "" : "s")} need attention.";

            Metrics[1] = Metrics[1] with { Value = stock.Count.ToString("N0") };
            Metrics[2] = Metrics[2] with { Value = stock.Count(s => s.IsLowStock).ToString("N0") };
            Metrics[3] = Metrics[3] with { Value = stock.Count(s => s.ExpiringSoonQuantity > 0).ToString("N0") };
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading dashboard failed");
            AlertSummary = "Alerts could not be loaded.";
            Metrics[1] = Metrics[1] with { Value = "—" };
            Metrics[2] = Metrics[2] with { Value = "—" };
            Metrics[3] = Metrics[3] with { Value = "—" };
        }

        _isLoaded = true;
        OnPropertyChanged(nameof(HasAlerts));
        OnPropertyChanged(nameof(IsAllClear));
    }

    private static string GetGreeting(DateTime now) => now.Hour switch
    {
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening"
    };
}
