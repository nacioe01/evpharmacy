using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed class AuditRow
{
    public AuditRow(AuditItem item) => Item = item;

    public AuditItem Item { get; }

    public string When => Item.OccurredAtUtc.ToLocalTime().ToString("MMM d, yyyy h:mm:ss tt");
    public string User => Item.Username ?? "—";
    public string Action => Item.Action switch
    {
        "LoginFailed" => "Failed sign-in",
        "StatusChanged" => "Status changed",
        var a => a
    };

    public string Kind => Item.Action switch
    {
        "LoginFailed" or "Deactivated" or "Voided" or "Restore" => StatusKind.Bad,
        "Adjusted" or "StatusChanged" or "Updated" => StatusKind.Warn,
        "Created" or "Login" or "Reactivated" or "Backup" => StatusKind.Good,
        _ => StatusKind.Neutral
    };

    public string Summary => Item.Summary;
    public string Details => Item.Details ?? string.Empty;
}

/// <summary>Administrator view of who did what, and when.</summary>
public sealed class AuditLogViewModel : PageViewModel
{
    private readonly AuditService _audit;

    private CancellationTokenSource? _searchCts;
    private HistoryRange _range = HistoryRange.Today;
    private string _searchText = string.Empty;
    private AuditRow? _selected;
    private string _errorMessage = string.Empty;

    public AuditLogViewModel(AuditService audit)
        : base("Audit log", "A permanent record of important actions")
    {
        _audit = audit;
        SetRangeCommand = new RelayCommand(p => { if (p is HistoryRange r) Range = r; });
        _ = LoadAsync();
    }

    public ObservableCollection<AuditRow> Items { get; } = [];

    public ICommand SetRangeCommand { get; }

    public HistoryRange Range
    {
        get => _range;
        private set
        {
            if (SetProperty(ref _range, value))
            {
                OnPropertyChanged(nameof(IsToday));
                OnPropertyChanged(nameof(IsLast7));
                OnPropertyChanged(nameof(IsLast30));
                OnPropertyChanged(nameof(IsAll));
                _ = LoadAsync();
            }
        }
    }

    public bool IsToday => Range == HistoryRange.Today;
    public bool IsLast7 => Range == HistoryRange.Last7Days;
    public bool IsLast30 => Range == HistoryRange.Last30Days;
    public bool IsAll => Range == HistoryRange.All;

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) _ = ReloadAfterDelayAsync(); }
    }

    public AuditRow? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasDetails));
            }
        }
    }

    public bool HasDetails => Selected is { Details.Length: > 0 };

    public bool IsEmpty => Items.Count == 0;

    public string Footer => Items.Count >= 500
        ? "Showing the 500 most recent entries. Narrow the date range or search to see others."
        : Items.Count == 0 ? string.Empty : $"{Items.Count:N0} entr{(Items.Count == 1 ? "y" : "ies")}";

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private async Task ReloadAfterDelayAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(250, cts.Token);
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Audit search failed");
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var today = DateTime.Today;
            var (from, to) = Range switch
            {
                HistoryRange.Today => ((DateTime?)today, (DateTime?)today.AddDays(1)),
                HistoryRange.Last7Days => (today.AddDays(-6), today.AddDays(1)),
                HistoryRange.Last30Days => (today.AddDays(-29), today.AddDays(1)),
                _ => (null, null)
            };

            var list = await _audit.SearchAsync(from, to, SearchText);

            Items.Clear();
            foreach (var item in list)
            {
                Items.Add(new AuditRow(item));
            }

            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading audit log failed");
            ErrorMessage = "The audit log could not be loaded.";
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Footer));
    }
}
