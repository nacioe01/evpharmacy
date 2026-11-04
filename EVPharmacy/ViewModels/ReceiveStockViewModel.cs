using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

/// <summary>One medicine being received, with its batch details.</summary>
public sealed class ReceiveLineViewModel : ObservableObject
{
    private string _batchNumber = string.Empty;
    private DateTime? _expiryDate;
    private string _quantityText = string.Empty;
    private string _unitCostText;
    private string _error = string.Empty;

    public ReceiveLineViewModel(MedicineListItem medicine, Action<ReceiveLineViewModel> onChanged)
    {
        MedicineId = medicine.Id;
        Name = medicine.BrandName is null ? medicine.GenericName : $"{medicine.GenericName} ({medicine.BrandName})";
        Detail = string.Join(" · ", new[] { medicine.Strength, medicine.DosageFormName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        Unit = medicine.UnitName;
        _unitCostText = Money.ToPesos(medicine.PurchaseCostCentavos).ToString("0.00");
        Changed = onChanged;
    }

    public int MedicineId { get; }
    public string Name { get; }
    public string Detail { get; }
    public string Unit { get; }
    public Action<ReceiveLineViewModel> Changed { get; }

    public string BatchNumber
    {
        get => _batchNumber;
        set { if (SetProperty(ref _batchNumber, value)) Touch(); }
    }

    public DateTime? ExpiryDate
    {
        get => _expiryDate;
        set { if (SetProperty(ref _expiryDate, value)) Touch(); }
    }

    public string QuantityText
    {
        get => _quantityText;
        set { if (SetProperty(ref _quantityText, value)) Touch(); }
    }

    public string UnitCostText
    {
        get => _unitCostText;
        set { if (SetProperty(ref _unitCostText, value)) Touch(); }
    }

    public string Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }

    /// <summary>Line total in centavos when quantity and cost are valid, otherwise null.</summary>
    public long? TotalCentavos =>
        int.TryParse(QuantityText?.Trim(), out var qty) && qty > 0 &&
        Money.TryParsePesos(UnitCostText, out var cost) && cost >= 0
            ? qty * Money.ToCentavos(cost)
            : null;

    private void Touch()
    {
        Error = string.Empty;
        Changed(this);
    }
}

/// <summary>Form for receiving a delivery or entering opening stock.</summary>
public sealed class ReceiveStockViewModel : ObservableObject
{
    private const decimal MaxPesos = 10_000_000m;

    private readonly InventoryService _inventory;
    private readonly MedicineService _medicines;
    private readonly int? _userId;
    private readonly Action<string> _onSaved;

    private CancellationTokenSource? _searchCts;
    private bool _isOpeningBalance;
    private int? _supplierId;
    private string _invoiceNumber = string.Empty;
    private DateTime? _receivedDate = DateTime.Today;
    private string _notes = string.Empty;
    private bool _updateCosts;
    private string _searchText = string.Empty;
    private string _errorMessage = string.Empty;
    private string _supplierError = string.Empty;
    private string _dateError = string.Empty;

    private ReceiveStockViewModel(
        InventoryService inventory,
        MedicineService medicines,
        int? userId,
        bool isOpeningBalance,
        Action<string> onSaved,
        Action onCancel)
    {
        _inventory = inventory;
        _medicines = medicines;
        _userId = userId;
        _isOpeningBalance = isOpeningBalance;
        _onSaved = onSaved;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(onCancel);
        AddMedicineCommand = new RelayCommand(p => { if (p is MedicineRow r) AddLine(r.Item); });
        RemoveLineCommand = new RelayCommand(p =>
        {
            if (p is ReceiveLineViewModel line)
            {
                Lines.Remove(line);
                OnLinesChanged();
            }
        });
        SubmitSearchCommand = new AsyncRelayCommand(SubmitSearchAsync);
    }

    public static async Task<ReceiveStockViewModel> CreateAsync(
        InventoryService inventory,
        MedicineService medicines,
        SupplierService suppliers,
        int? userId,
        bool isOpeningBalance,
        Action<string> onSaved,
        Action onCancel)
    {
        var form = new ReceiveStockViewModel(inventory, medicines, userId, isOpeningBalance, onSaved, onCancel);

        foreach (var supplier in await suppliers.SearchAsync())
        {
            form.Suppliers.Add(supplier);
        }

        return form;
    }

    public string Title => IsOpeningBalance ? "Opening stock balance" : "Receive stock";

    public string Subtitle => IsOpeningBalance
        ? "Enter stock that is already on your shelves. No supplier is needed."
        : "Record a supplier delivery. Each medicine needs its batch number and expiry date.";

    public string SaveText => IsOpeningBalance ? "Save opening stock" : "Receive stock";

    public ObservableCollection<Supplier> Suppliers { get; } = [];
    public ObservableCollection<ReceiveLineViewModel> Lines { get; } = [];
    public ObservableCollection<MedicineRow> SearchResults { get; } = [];

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddMedicineCommand { get; }
    public ICommand RemoveLineCommand { get; }
    public ICommand SubmitSearchCommand { get; }

    public bool IsOpeningBalance
    {
        get => _isOpeningBalance;
        set
        {
            if (SetProperty(ref _isOpeningBalance, value))
            {
                SupplierError = string.Empty;
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Subtitle));
                OnPropertyChanged(nameof(SaveText));
                OnPropertyChanged(nameof(ShowSupplier));
            }
        }
    }

    public bool ShowSupplier => !IsOpeningBalance;

    public int? SupplierId
    {
        get => _supplierId;
        set { if (SetProperty(ref _supplierId, value)) SupplierError = string.Empty; }
    }

    public string InvoiceNumber { get => _invoiceNumber; set => SetProperty(ref _invoiceNumber, value); }

    public DateTime? ReceivedDate
    {
        get => _receivedDate;
        set { if (SetProperty(ref _receivedDate, value)) DateError = string.Empty; }
    }

    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public bool UpdateCosts { get => _updateCosts; set => SetProperty(ref _updateCosts, value); }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = SearchAfterDelayAsync();
            }
        }
    }

    public bool HasSearchResults => SearchResults.Count > 0;

    public bool ShowNoResults => !string.IsNullOrWhiteSpace(SearchText) && SearchResults.Count == 0;

    public bool HasLines => Lines.Count > 0;

    public bool HasNoLines => Lines.Count == 0;

    public string TotalText
    {
        get
        {
            var totals = Lines.Select(l => l.TotalCentavos).ToList();
            var sum = totals.Where(t => t.HasValue).Sum(t => t!.Value);
            var count = Lines.Count;
            return count == 0
                ? string.Empty
                : $"{count} item{(count == 1 ? "" : "s")} · Total cost {Money.Format(sum)}";
        }
    }

    public string SupplierError { get => _supplierError; private set => SetProperty(ref _supplierError, value); }
    public string DateError { get => _dateError; private set => SetProperty(ref _dateError, value); }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // ------------------------------------------------------------------

    private async Task SearchAfterDelayAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(250, cts.Token);
            await RunSearchAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Debounced medicine search failed");
        }
    }

    private async Task RunSearchAsync()
    {
        SearchResults.Clear();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            try
            {
                var found = await _medicines.SearchAsync(SearchText);
                foreach (var item in found.Take(8))
                {
                    SearchResults.Add(new MedicineRow(item));
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "Medicine search in receiving failed");
                ErrorMessage = "Medicines could not be searched.";
            }
        }

        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    /// <summary>Enter key (or a barcode scanner): add the match when there is exactly one.</summary>
    private async Task SubmitSearchAsync()
    {
        _searchCts?.Cancel();
        await RunSearchAsync();

        if (SearchResults.Count == 1)
        {
            AddLine(SearchResults[0].Item);
        }
    }

    private void AddLine(MedicineListItem medicine)
    {
        Lines.Add(new ReceiveLineViewModel(medicine, _ => OnPropertyChanged(nameof(TotalText))));
        SearchText = string.Empty;
        SearchResults.Clear();
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(ShowNoResults));
        OnLinesChanged();
    }

    private void OnLinesChanged()
    {
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(HasNoLines));
        OnPropertyChanged(nameof(TotalText));
    }

    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;

        var input = Validate();
        if (input is null)
        {
            return;
        }

        try
        {
            var result = await _inventory.ReceiveAsync(input, _userId);

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "The stock could not be saved.";
                return;
            }

            var units = input.Lines.Sum(l => l.Quantity);
            _onSaved(IsOpeningBalance
                ? $"Opening stock saved: {input.Lines.Count} item(s), {units:N0} units."
                : $"Stock received: {input.Lines.Count} item(s), {units:N0} units.");
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Receiving stock failed");
            ErrorMessage = "The stock could not be saved. Nothing was recorded, and your entries are still here. Please try again.";
        }
    }

    private ReceiveStockInput? Validate()
    {
        var valid = true;

        if (!IsOpeningBalance && SupplierId is null)
        {
            SupplierError = "Choose the supplier.";
            valid = false;
        }

        if (ReceivedDate is null)
        {
            DateError = "Choose the date received.";
            valid = false;
        }
        else if (DateOnly.FromDateTime(ReceivedDate.Value) > InventoryService.Today)
        {
            DateError = "The date cannot be in the future.";
            valid = false;
        }

        if (Lines.Count == 0)
        {
            ErrorMessage = "Add at least one medicine to receive.";
            return null;
        }

        var lines = new List<ReceiveLineInput>();
        var today = InventoryService.Today;

        foreach (var line in Lines)
        {
            line.Error = string.Empty;

            if (string.IsNullOrWhiteSpace(line.BatchNumber))
            {
                line.Error = "Enter the batch or lot number.";
            }
            else if (line.ExpiryDate is null)
            {
                line.Error = "Choose the expiry date.";
            }
            else if (DateOnly.FromDateTime(line.ExpiryDate.Value) <= today)
            {
                line.Error = "This batch is already expired and cannot be received.";
            }
            else if (!int.TryParse(line.QuantityText?.Trim(), out var qty) ||
                     qty <= 0 || qty > InventoryService.MaxQuantityPerLine)
            {
                line.Error = "Enter the quantity as a whole number above zero.";
            }
            else if (!Money.TryParsePesos(line.UnitCostText, out var cost) || cost < 0 || cost > MaxPesos)
            {
                line.Error = "Enter the unit cost, for example 12.50.";
            }
            else
            {
                lines.Add(new ReceiveLineInput(
                    line.MedicineId,
                    line.BatchNumber,
                    DateOnly.FromDateTime(line.ExpiryDate.Value),
                    qty,
                    Money.ToCentavos(cost)));
                continue;
            }

            valid = false;
        }

        if (!valid)
        {
            ErrorMessage = "Please fix the highlighted fields.";
            return null;
        }

        return new ReceiveStockInput(
            IsOpeningBalance ? ReceiptType.OpeningBalance : ReceiptType.Delivery,
            IsOpeningBalance ? null : SupplierId,
            InvoiceNumber,
            DateOnly.FromDateTime(ReceivedDate!.Value),
            Notes,
            UpdateCosts,
            lines);
    }
}
