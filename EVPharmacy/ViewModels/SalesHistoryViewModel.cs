using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public enum HistoryRange
{
    Today,
    Yesterday,
    Last7Days,
    Last30Days,
    All
}

public sealed class SaleRow
{
    public SaleRow(SaleListItem item) => Item = item;

    public SaleListItem Item { get; }

    public int Id => Item.Id;
    public string Receipt => Item.ReceiptNumber;
    public string When => Item.SoldAtUtc.ToLocalTime().ToString("MMM d, h:mm tt");
    public string Cashier => Item.CashierName ?? "—";
    public string Items => Item.ItemCount.ToString("N0");
    public string Total => Money.Format(Item.TotalCentavos);

    public string Payment => Item.PaymentMethod switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        _ => "E-wallet"
    };

    public string Discount => Item.DiscountType switch
    {
        DiscountType.SeniorCitizen => "Senior",
        DiscountType.Pwd => "PWD",
        _ => "—"
    };

    public string Status => Item.Status == SaleStatus.Voided ? "Voided" : "Completed";
    public string Kind => Item.Status == SaleStatus.Voided ? StatusKind.Bad : StatusKind.Good;
    public bool IsVoided => Item.Status == SaleStatus.Voided;
}

/// <summary>Past sales with a receipt preview, reprint, and (for administrators) voiding.</summary>
public sealed class SalesHistoryViewModel : PageViewModel
{
    private readonly SalesService _sales;
    private readonly SettingsService _settings;
    private readonly ReceiptPrinter _printer;
    private readonly DialogService _dialogs;
    private readonly UserSession _session;

    private CancellationTokenSource? _searchCts;
    private HistoryRange _range = HistoryRange.Today;
    private string _searchText = string.Empty;
    private bool _includeVoided = true;
    private SaleRow? _selected;
    private FlowDocument? _receiptDocument;
    private string _summary = string.Empty;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;

    public SalesHistoryViewModel(
        SalesService sales,
        SettingsService settings,
        ReceiptPrinter printer,
        DialogService dialogs,
        UserSession session)
        : base("Sales history", "Past transactions, receipts and voids")
    {
        _sales = sales;
        _settings = settings;
        _printer = printer;
        _dialogs = dialogs;
        _session = session;

        SetRangeCommand = new RelayCommand(p => { if (p is HistoryRange r) Range = r; });
        PrintCommand = new AsyncRelayCommand(PrintAsync, () => Selected is not null);
        VoidCommand = new AsyncRelayCommand(VoidAsync, () => CanVoid);

        _ = LoadAsync();
    }

    public ObservableCollection<SaleRow> Items { get; } = [];

    public ICommand SetRangeCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand VoidCommand { get; }

    public HistoryRange Range
    {
        get => _range;
        private set
        {
            if (SetProperty(ref _range, value))
            {
                OnPropertyChanged(nameof(IsToday));
                OnPropertyChanged(nameof(IsYesterday));
                OnPropertyChanged(nameof(IsLast7));
                OnPropertyChanged(nameof(IsLast30));
                OnPropertyChanged(nameof(IsAll));
                _ = LoadAsync();
            }
        }
    }

    public bool IsToday => Range == HistoryRange.Today;
    public bool IsYesterday => Range == HistoryRange.Yesterday;
    public bool IsLast7 => Range == HistoryRange.Last7Days;
    public bool IsLast30 => Range == HistoryRange.Last30Days;
    public bool IsAll => Range == HistoryRange.All;

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) _ = ReloadAfterDelayAsync(); }
    }

    public bool IncludeVoided
    {
        get => _includeVoided;
        set { if (SetProperty(ref _includeVoided, value)) _ = LoadAsync(); }
    }

    public SaleRow? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanVoid));
                _ = ShowReceiptAsync();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasSelection => Selected is not null;

    public bool IsAdmin => _session.IsAdmin;

    /// <summary>Only administrators can void, and only completed sales.</summary>
    public bool CanVoid => IsAdmin && Selected is { IsVoided: false };

    public FlowDocument? ReceiptDocument
    {
        get => _receiptDocument;
        private set => SetProperty(ref _receiptDocument, value);
    }

    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }

    public bool IsEmpty => Items.Count == 0;

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatus)); }
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // ------------------------------------------------------------------

    private (DateTime? From, DateTime? To) RangeBounds()
    {
        var today = DateTime.Today;
        return Range switch
        {
            HistoryRange.Today => (today, today.AddDays(1)),
            HistoryRange.Yesterday => (today.AddDays(-1), today),
            HistoryRange.Last7Days => (today.AddDays(-6), today.AddDays(1)),
            HistoryRange.Last30Days => (today.AddDays(-29), today.AddDays(1)),
            _ => (null, null)
        };
    }

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
            ErrorLogger.Log(ex, "Sales history search failed");
        }
    }

    private async Task LoadAsync(int? keepSelectedId = null)
    {
        try
        {
            var (from, to) = RangeBounds();
            var list = await _sales.GetHistoryAsync(from, to, SearchText, IncludeVoided);

            Items.Clear();
            foreach (var item in list)
            {
                Items.Add(new SaleRow(item));
            }

            var completed = list.Where(i => i.Status == SaleStatus.Completed).ToList();
            Summary = list.Count == 0
                ? string.Empty
                : $"{completed.Count:N0} completed sale{(completed.Count == 1 ? "" : "s")} · {Money.Format(completed.Sum(i => i.TotalCentavos))}" +
                  (list.Count > completed.Count ? $" · {list.Count - completed.Count} voided (not counted)" : string.Empty);

            Selected = keepSelectedId is int id ? Items.FirstOrDefault(r => r.Id == id) : null;
            if (Selected is null)
            {
                ReceiptDocument = null;
            }

            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading sales history failed");
            ErrorMessage = "The sales history could not be loaded.";
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task ShowReceiptAsync()
    {
        var row = Selected;
        if (row is null)
        {
            ReceiptDocument = null;
            return;
        }

        try
        {
            var sale = await _sales.GetSaleAsync(row.Id);
            var store = await _settings.GetAsync();

            // Ignore a slow result if the user has already clicked another row.
            if (sale is not null && Selected?.Id == row.Id)
            {
                ReceiptDocument = ReceiptBuilder.Build(sale, store);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Showing receipt failed");
            ErrorMessage = "The receipt could not be shown.";
        }
    }

    private async Task PrintAsync()
    {
        if (Selected is null)
        {
            return;
        }

        try
        {
            if (await _printer.PrintAsync(Selected.Id, isReprint: true))
            {
                StatusMessage = $"Receipt {Selected.Receipt} sent to the printer.";
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Printing receipt failed");
            ErrorMessage = "The receipt could not be printed. Check that the printer is connected and try again.";
        }
    }

    private async Task VoidAsync()
    {
        var row = Selected;
        if (row is null || !CanVoid)
        {
            return;
        }

        string? reason = null;
        string? error = null;

        while (true)
        {
            reason = _dialogs.PromptForText(
                "Void sale",
                $"Reason for voiding receipt {row.Receipt} ({row.Total})",
                reason, error, acceptText: "Void sale", maxLength: Sale.MaxVoidReasonLength);

            if (reason is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                break;
            }

            error = "Please give a reason.";
        }

        var confirm = MessageBox.Show(
            $"Void receipt {row.Receipt}?\n\nThe sale stays on record marked as voided, and the stock goes back to the batches it came from. This cannot be undone.",
            "Void sale",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = await _sales.VoidSaleAsync(row.Id, reason, _session.CurrentUser?.Id);

            if (result.Success)
            {
                StatusMessage = $"Receipt {row.Receipt} was voided and its stock returned.";
                await LoadAsync(row.Id);
            }
            else
            {
                ErrorMessage = result.Error ?? "The sale could not be voided.";
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Voiding sale failed");
            ErrorMessage = "The sale could not be voided. Nothing was changed.";
        }
    }
}
