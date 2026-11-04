using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

/// <summary>Add / edit form for a single medicine.</summary>
public sealed class MedicineFormViewModel : ObservableObject
{
    private const decimal MaxPesos = 10_000_000m;
    private const int MaxReorderLevel = 1_000_000;

    private readonly MedicineService _medicines;
    private readonly LookupService _lookups;
    private readonly DialogService _dialogs;
    private readonly int? _userId;
    private readonly int? _medicineId;
    private readonly Action<string> _onSaved;
    private readonly Action _onCancel;

    // Original prices, to know when a change reason is relevant.
    private long _originalCost;
    private long _originalPrice;

    private string _genericName = string.Empty;
    private string _brandName = string.Empty;
    private string _barcode = string.Empty;
    private int? _categoryId;
    private int? _dosageFormId;
    private int? _unitId;
    private string _strength = string.Empty;
    private string _costText = string.Empty;
    private string _priceText = string.Empty;
    private string _reorderText = "0";
    private PrescriptionClass _prescriptionClass = PrescriptionClass.OverTheCounter;
    private bool _isVatExempt;
    private string _priceChangeReason = string.Empty;

    private string _genericNameError = string.Empty;
    private string _categoryError = string.Empty;
    private string _dosageFormError = string.Empty;
    private string _unitError = string.Empty;
    private string _costError = string.Empty;
    private string _priceError = string.Empty;
    private string _reorderError = string.Empty;
    private string _errorMessage = string.Empty;

    private MedicineFormViewModel(
        MedicineService medicines,
        LookupService lookups,
        DialogService dialogs,
        int? userId,
        int? medicineId,
        Action<string> onSaved,
        Action onCancel)
    {
        _medicines = medicines;
        _lookups = lookups;
        _dialogs = dialogs;
        _userId = userId;
        _medicineId = medicineId;
        _onSaved = onSaved;
        _onCancel = onCancel;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(_onCancel);
        AddCategoryCommand = new AsyncRelayCommand(() => AddLookupAsync<Category>(
            "New category", "Category name", id => CategoryId = id));
        AddDosageFormCommand = new AsyncRelayCommand(() => AddLookupAsync<DosageForm>(
            "New dosage form", "Dosage form name", id => DosageFormId = id));
        AddUnitCommand = new AsyncRelayCommand(() => AddLookupAsync<UnitOfMeasure>(
            "New unit of measure", "Unit name", id => UnitId = id));
    }

    /// <summary>Creates the form (add mode when <paramref name="medicineId"/> is null).</summary>
    public static async Task<MedicineFormViewModel> CreateAsync(
        MedicineService medicines,
        LookupService lookups,
        DialogService dialogs,
        int? userId,
        int? medicineId,
        Action<string> onSaved,
        Action onCancel)
    {
        var form = new MedicineFormViewModel(
            medicines, lookups, dialogs, userId, medicineId, onSaved, onCancel);

        Medicine? existing = null;
        if (medicineId is not null)
        {
            existing = await medicines.GetAsync(medicineId.Value)
                ?? throw new InvalidOperationException("This medicine no longer exists.");
        }

        await form.LoadLookupsAsync(existing);

        if (existing is not null)
        {
            form.Populate(existing);
            var history = await medicines.GetPriceHistoryAsync(existing.Id);
            foreach (var entry in history)
            {
                form.PriceHistory.Add(new PriceHistoryRow(entry));
            }
        }

        return form;
    }

    public bool IsEditing => _medicineId is not null;

    public string Title => IsEditing ? "Edit medicine" : "Add medicine";

    public string SaveText => IsEditing ? "Save changes" : "Save medicine";

    public ObservableCollection<Category> Categories { get; } = [];
    public ObservableCollection<DosageForm> DosageForms { get; } = [];
    public ObservableCollection<UnitOfMeasure> Units { get; } = [];
    public ObservableCollection<PriceHistoryRow> PriceHistory { get; } = [];
    public IReadOnlyList<PrescriptionOption> PrescriptionOptions => PrescriptionOption.All;

    public bool HasPriceHistory => PriceHistory.Count > 0;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddCategoryCommand { get; }
    public ICommand AddDosageFormCommand { get; }
    public ICommand AddUnitCommand { get; }

    // ---------------- Fields ----------------

    public string GenericName
    {
        get => _genericName;
        set { if (SetProperty(ref _genericName, value)) GenericNameError = string.Empty; }
    }

    public string BrandName { get => _brandName; set => SetProperty(ref _brandName, value); }

    public string Barcode { get => _barcode; set => SetProperty(ref _barcode, value); }

    public int? CategoryId
    {
        get => _categoryId;
        set { if (SetProperty(ref _categoryId, value)) CategoryError = string.Empty; }
    }

    public int? DosageFormId
    {
        get => _dosageFormId;
        set { if (SetProperty(ref _dosageFormId, value)) DosageFormError = string.Empty; }
    }

    public int? UnitId
    {
        get => _unitId;
        set { if (SetProperty(ref _unitId, value)) UnitError = string.Empty; }
    }

    public string Strength { get => _strength; set => SetProperty(ref _strength, value); }

    public PrescriptionClass PrescriptionClass
    {
        get => _prescriptionClass;
        set => SetProperty(ref _prescriptionClass, value);
    }

    public bool IsVatExempt { get => _isVatExempt; set => SetProperty(ref _isVatExempt, value); }

    public string CostText
    {
        get => _costText;
        set
        {
            if (SetProperty(ref _costText, value))
            {
                CostError = string.Empty;
                OnPricingChanged();
            }
        }
    }

    public string PriceText
    {
        get => _priceText;
        set
        {
            if (SetProperty(ref _priceText, value))
            {
                PriceError = string.Empty;
                OnPricingChanged();
            }
        }
    }

    public string ReorderText
    {
        get => _reorderText;
        set { if (SetProperty(ref _reorderText, value)) ReorderError = string.Empty; }
    }

    public string PriceChangeReason
    {
        get => _priceChangeReason;
        set => SetProperty(ref _priceChangeReason, value);
    }

    // ---------------- Derived display ----------------

    /// <summary>Only shown when editing and a price has actually changed.</summary>
    public bool ShowReason =>
        IsEditing &&
        TryGetCentavos(CostText, out var cost) && TryGetCentavos(PriceText, out var price) &&
        (cost != _originalCost || price != _originalPrice);

    public string MarginText
    {
        get
        {
            if (!TryGetCentavos(CostText, out var cost) ||
                !TryGetCentavos(PriceText, out var price) || price <= 0)
            {
                return string.Empty;
            }

            var profit = price - cost;
            var margin = (decimal)profit / price * 100m;
            return $"Profit per unit: {Money.Format(profit)} ({margin:0.#}% margin)";
        }
    }

    public bool HasPriceWarning =>
        TryGetCentavos(CostText, out var cost) &&
        TryGetCentavos(PriceText, out var price) &&
        price < cost;

    // ---------------- Validation messages ----------------

    public string GenericNameError { get => _genericNameError; private set => SetProperty(ref _genericNameError, value); }
    public string CategoryError { get => _categoryError; private set => SetProperty(ref _categoryError, value); }
    public string DosageFormError { get => _dosageFormError; private set => SetProperty(ref _dosageFormError, value); }
    public string UnitError { get => _unitError; private set => SetProperty(ref _unitError, value); }
    public string CostError { get => _costError; private set => SetProperty(ref _costError, value); }
    public string PriceError { get => _priceError; private set => SetProperty(ref _priceError, value); }
    public string ReorderError { get => _reorderError; private set => SetProperty(ref _reorderError, value); }

    /// <summary>Form-level error (for example a duplicate barcode reported by the database).</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // ---------------- Behavior ----------------

    private async Task LoadLookupsAsync(Medicine? existing)
    {
        // Inactive entries are hidden, except one already used by this medicine.
        var categories = await _lookups.GetAsync<Category>(includeInactive: true);
        var forms = await _lookups.GetAsync<DosageForm>(includeInactive: true);
        var units = await _lookups.GetAsync<UnitOfMeasure>(includeInactive: true);

        Fill(Categories, categories.Where(c => c.IsActive || c.Id == existing?.CategoryId));
        Fill(DosageForms, forms.Where(f => f.IsActive || f.Id == existing?.DosageFormId));
        Fill(Units, units.Where(u => u.IsActive || u.Id == existing?.UnitOfMeasureId));
    }

    private void Populate(Medicine medicine)
    {
        _originalCost = medicine.PurchaseCostCentavos;
        _originalPrice = medicine.SellingPriceCentavos;

        GenericName = medicine.GenericName;
        BrandName = medicine.BrandName ?? string.Empty;
        Barcode = medicine.Barcode ?? string.Empty;
        CategoryId = medicine.CategoryId;
        DosageFormId = medicine.DosageFormId;
        UnitId = medicine.UnitOfMeasureId;
        Strength = medicine.Strength ?? string.Empty;
        PrescriptionClass = medicine.PrescriptionClass;
        IsVatExempt = medicine.IsVatExempt;
        CostText = Money.ToPesos(medicine.PurchaseCostCentavos).ToString("0.00");
        PriceText = Money.ToPesos(medicine.SellingPriceCentavos).ToString("0.00");
        ReorderText = medicine.ReorderLevel.ToString();
    }

    private async Task AddLookupAsync<T>(
        string title,
        string label,
        Action<int> select) where T : LookupItem, new()
    {
        string? error = null;
        string? name = string.Empty;

        // Re-ask until the name is accepted or the user cancels.
        while (true)
        {
            name = _dialogs.PromptForText(title, label, name, error);
            if (name is null)
            {
                return;
            }

            var result = await _lookups.AddAsync<T>(name);
            if (result.Success && result.Id is int id)
            {
                var items = await _lookups.GetAsync<T>();
                RefreshLookup(items);
                select(id);
                return;
            }

            error = result.Error;
        }
    }

    private void RefreshLookup<T>(List<T> items) where T : LookupItem
    {
        switch (items)
        {
            case List<Category> c: Fill(Categories, c); break;
            case List<DosageForm> f: Fill(DosageForms, f); break;
            case List<UnitOfMeasure> u: Fill(Units, u); break;
        }
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
            var result = _medicineId is int id
                ? await _medicines.UpdateAsync(id, input, _userId)
                : await _medicines.CreateAsync(input, _userId);

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "The medicine could not be saved.";
                return;
            }

            _onSaved(IsEditing
                ? $"{input.GenericName!.Trim()} was updated."
                : $"{input.GenericName!.Trim()} was added.");
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Saving medicine failed");
            ErrorMessage = "The medicine could not be saved. Your entries are still here, please try again.";
        }
    }

    /// <summary>Checks every field, shows messages beside them, and builds the service input.</summary>
    private MedicineInput? Validate()
    {
        var valid = true;

        if (string.IsNullOrWhiteSpace(GenericName))
        {
            GenericNameError = "Enter the generic name.";
            valid = false;
        }
        else if (GenericName.Trim().Length > Medicine.MaxNameLength)
        {
            GenericNameError = $"Use {Medicine.MaxNameLength} characters or fewer.";
            valid = false;
        }

        if (CategoryId is null) { CategoryError = "Choose a category."; valid = false; }
        if (DosageFormId is null) { DosageFormError = "Choose a dosage form."; valid = false; }
        if (UnitId is null) { UnitError = "Choose a unit of measure."; valid = false; }

        var costOk = TryParseMoney(CostText, "purchase cost", out var cost, out var costError);
        CostError = costError;

        var priceOk = TryParseMoney(PriceText, "selling price", out var price, out var priceError);
        PriceError = priceError;

        valid &= costOk && priceOk;

        if (!int.TryParse(ReorderText?.Trim(), out var reorder) ||
            reorder < 0 || reorder > MaxReorderLevel)
        {
            ReorderError = "Enter a whole number, 0 or higher.";
            valid = false;
        }

        if (!valid)
        {
            ErrorMessage = "Please fix the highlighted fields.";
            return null;
        }

        return new MedicineInput(
            GenericName, BrandName, Barcode,
            CategoryId!.Value, DosageFormId!.Value, Strength, UnitId!.Value,
            cost, price, reorder, PrescriptionClass, IsVatExempt,
            ShowReason ? PriceChangeReason : null);
    }

    private static bool TryParseMoney(string text, string name, out long centavos, out string error)
    {
        centavos = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = $"Enter the {name}.";
            return false;
        }

        if (!Money.TryParsePesos(text, out var pesos))
        {
            error = "Enter an amount such as 12.50.";
            return false;
        }

        if (pesos < 0 || pesos > MaxPesos)
        {
            error = "Amount must be between 0 and 10,000,000.";
            return false;
        }

        centavos = Money.ToCentavos(pesos);
        return true;
    }

    private static bool TryGetCentavos(string text, out long centavos)
    {
        centavos = 0;
        if (!Money.TryParsePesos(text, out var pesos) || pesos < 0 || pesos > MaxPesos)
        {
            return false;
        }

        centavos = Money.ToCentavos(pesos);
        return true;
    }

    private void OnPricingChanged()
    {
        OnPropertyChanged(nameof(MarginText));
        OnPropertyChanged(nameof(HasPriceWarning));
        OnPropertyChanged(nameof(ShowReason));
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
