using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public enum InventoryMode
{
    Stock,
    Batches,
    Movements
}

/// <summary>Inventory page: stock levels, batches, movement history and stock receiving.</summary>
public sealed class InventoryViewModel : PageViewModel
{
    private readonly InventoryService _inventory;
    private readonly MedicineService _medicines;
    private readonly SupplierService _suppliers;
    private readonly DialogService _dialogs;
    private readonly UserSession _session;

    private CancellationTokenSource? _searchCts;
    private InventoryMode _mode = InventoryMode.Stock;
    private string _searchText = string.Empty;
    private bool _showEmptyBatches;
    private ObservableObject? _form;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;

    private int _lowStockCount;
    private int _expiringCount;
    private int _expiredCount;

    public InventoryViewModel(
        InventoryService inventory,
        MedicineService medicines,
        SupplierService suppliers,
        DialogService dialogs,
        UserSession session)
        : base("Inventory", "Stock levels, batches and expiry")
    {
        _inventory = inventory;
        _medicines = medicines;
        _suppliers = suppliers;
        _dialogs = dialogs;
        _session = session;

        ReceiveCommand = new RelayCommand(() => _ = OpenFormAsync(false));
        OpeningBalanceCommand = new RelayCommand(() => _ = OpenFormAsync(true));
        SetModeCommand = new RelayCommand(p => { if (p is InventoryMode m) Mode = m; });
        HoldCommand = new RelayCommand(p => { if (p is BatchRow r) _ = ChangeStatusAsync(r, BatchStatus.Quarantined); });
        RecallCommand = new RelayCommand(p => { if (p is BatchRow r) _ = ChangeStatusAsync(r, BatchStatus.Recalled); });
        ReleaseCommand = new RelayCommand(p => { if (p is BatchRow r) _ = ChangeStatusAsync(r, BatchStatus.Available); });
        AdjustCommand = new RelayCommand(p => { if (p is BatchRow r) OpenAdjustForm(r); });

        _ = LoadAsync();
    }

    public ObservableCollection<StockRow> StockRows { get; } = [];
    public ObservableCollection<BatchRow> BatchRows { get; } = [];
    public ObservableCollection<MovementRow> MovementRows { get; } = [];

    public ICommand ReceiveCommand { get; }
    public ICommand OpeningBalanceCommand { get; }
    public ICommand SetModeCommand { get; }
    public ICommand HoldCommand { get; }
    public ICommand RecallCommand { get; }
    public ICommand ReleaseCommand { get; }
    public ICommand AdjustCommand { get; }

    public InventoryMode Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsStockMode));
                OnPropertyChanged(nameof(IsBatchMode));
                OnPropertyChanged(nameof(IsMovementMode));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(EmptyText));
                _ = LoadAsync();
            }
        }
    }

    public bool IsStockMode => Mode == InventoryMode.Stock;
    public bool IsBatchMode => Mode == InventoryMode.Batches;
    public bool IsMovementMode => Mode == InventoryMode.Movements;

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) _ = ReloadAfterDelayAsync(); }
    }

    public bool ShowEmptyBatches
    {
        get => _showEmptyBatches;
        set { if (SetProperty(ref _showEmptyBatches, value)) _ = LoadAsync(); }
    }

    public ObservableObject? Form
    {
        get => _form;
        private set { if (SetProperty(ref _form, value)) OnPropertyChanged(nameof(IsListVisible)); }
    }

    public bool IsListVisible => Form is null;

    public int LowStockCount { get => _lowStockCount; private set => SetProperty(ref _lowStockCount, value); }
    public int ExpiringCount { get => _expiringCount; private set => SetProperty(ref _expiringCount, value); }
    public int ExpiredCount { get => _expiredCount; private set => SetProperty(ref _expiredCount, value); }

    public bool IsEmpty => Mode switch
    {
        InventoryMode.Stock => StockRows.Count == 0,
        InventoryMode.Batches => BatchRows.Count == 0,
        _ => MovementRows.Count == 0
    };

    public string EmptyText => !string.IsNullOrWhiteSpace(SearchText)
        ? "Nothing matches your search."
        : Mode switch
        {
            InventoryMode.Stock => "No medicines yet. Add medicines first, then receive stock.",
            InventoryMode.Batches => "No stock batches yet. Click \"Receive stock\" to add the first delivery.",
            _ => "No stock movements yet."
        };

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
    }

    private async Task LoadAsync()
    {
        try
        {
            // Alert counts always reflect all stock, not the current search.
            await LoadAlertCountsAsync();

            switch (Mode)
            {
                case InventoryMode.Stock:
                    Fill(StockRows, (await _inventory.GetStockSummaryAsync(SearchText)).Select(i => new StockRow(i)));
                    break;
                case InventoryMode.Batches:
                    Fill(BatchRows, (await _inventory.GetBatchesAsync(SearchText, ShowEmptyBatches)).Select(i => new BatchRow(i)));
                    break;
                default:
                    Fill(MovementRows, (await _inventory.GetMovementsAsync(SearchText)).Select(i => new MovementRow(i)));
                    break;
            }

            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading inventory failed");
            ErrorMessage = "The inventory could not be loaded.";
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private async Task LoadAlertCountsAsync()
    {
        var all = await _inventory.GetStockSummaryAsync();
        LowStockCount = all.Count(i => i.IsLowStock);
        ExpiringCount = all.Count(i => i.ExpiringSoonQuantity > 0);
        ExpiredCount = all.Count(i => i.ExpiredQuantity > 0);
    }

    private async Task OpenFormAsync(bool openingBalance)
    {
        try
        {
            StatusMessage = string.Empty;

            Form = await ReceiveStockViewModel.CreateAsync(
                _inventory, _medicines, _suppliers,
                _session.CurrentUser?.Id,
                openingBalance,
                onSaved: message => _ = CloseFormAsync(message),
                onCancel: () => Form = null);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Opening receive form failed");
            ErrorMessage = "The receiving form could not be opened.";
        }
    }

    private void OpenAdjustForm(BatchRow row)
    {
        StatusMessage = string.Empty;

        Form = new AdjustStockViewModel(
            _inventory, row,
            _session.CurrentUser?.Id,
            onSaved: message => _ = CloseFormAsync(message),
            onCancel: () => Form = null);
    }

    private async Task CloseFormAsync(string message)
    {
        Form = null;
        StatusMessage = message;
        await LoadAsync();
    }

    private async Task ChangeStatusAsync(BatchRow row, BatchStatus status)
    {
        string? reason = null;

        if (status == BatchStatus.Available)
        {
            var confirm = MessageBox.Show(
                $"Release batch {row.BatchNumber} of {row.Item.GenericName}?\n\nIt will be available for sale again (unless it has expired).",
                "Release batch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }
        else
        {
            var verb = status == BatchStatus.Recalled ? "Recall" : "Hold";
            string? error = null;

            while (true)
            {
                reason = _dialogs.PromptForText(
                    $"{verb} batch",
                    $"Reason for {verb.ToLowerInvariant()}ing batch {row.BatchNumber} of {row.Item.GenericName}",
                    reason,
                    error,
                    acceptText: verb,
                    maxLength: InventoryBatch.MaxStatusNoteLength);

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
        }

        try
        {
            var result = await _inventory.SetBatchStatusAsync(row.Id, status, reason);

            if (result.Success)
            {
                StatusMessage = status switch
                {
                    BatchStatus.Recalled => $"Batch {row.BatchNumber} was recalled and can no longer be sold.",
                    BatchStatus.Quarantined => $"Batch {row.BatchNumber} is on hold and cannot be sold.",
                    _ => $"Batch {row.BatchNumber} was released."
                };
                await LoadAsync();
            }
            else
            {
                ErrorMessage = result.Error ?? "The change could not be saved.";
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Changing batch status failed");
            ErrorMessage = "The change could not be saved.";
        }
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
