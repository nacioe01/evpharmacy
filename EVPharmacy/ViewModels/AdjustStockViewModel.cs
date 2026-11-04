using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed record ReasonOption(AdjustmentReason Value, string Label);

public sealed record DirectionOption(bool IsIncrease, string Label);

/// <summary>Form for correcting the quantity of one batch (damage, write-off, count correction...).</summary>
public sealed class AdjustStockViewModel : ObservableObject
{
    private readonly InventoryService _inventory;
    private readonly int? _userId;
    private readonly int _batchId;
    private readonly int _currentQuantity;
    private readonly Action<string> _onSaved;

    private AdjustmentReason? _reason;
    private bool _isIncrease;
    private string _quantityText = string.Empty;
    private string _note = string.Empty;
    private string _reasonError = string.Empty;
    private string _quantityError = string.Empty;
    private string _noteError = string.Empty;
    private string _errorMessage = string.Empty;

    public AdjustStockViewModel(
        InventoryService inventory,
        BatchRow batch,
        int? userId,
        Action<string> onSaved,
        Action onCancel)
    {
        _inventory = inventory;
        _userId = userId;
        _batchId = batch.Id;
        _currentQuantity = batch.Item.QuantityOnHand;
        _onSaved = onSaved;

        MedicineName = batch.Medicine;
        BatchSummary = $"Batch {batch.BatchNumber} · expires {batch.Expiry} ({batch.ExpiryDetail}) · {batch.Status}";

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(onCancel);
    }

    public string MedicineName { get; }
    public string BatchSummary { get; }
    public string CurrentText => $"{_currentQuantity:N0} on hand";

    public IReadOnlyList<ReasonOption> Reasons { get; } =
        Enum.GetValues<AdjustmentReason>()
            .Select(r => new ReasonOption(r, AdjustmentReasons.Label(r)))
            .ToList();

    public IReadOnlyList<DirectionOption> Directions { get; } =
    [
        new(false, "Remove stock"),
        new(true, "Add stock")
    ];

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public AdjustmentReason? Reason
    {
        get => _reason;
        set
        {
            if (SetProperty(ref _reason, value))
            {
                ReasonError = string.Empty;

                // Only count corrections and "other" may add stock.
                if (!AllowsIncrease)
                {
                    IsIncrease = false;
                }

                OnPropertyChanged(nameof(AllowsIncrease));
                OnPropertyChanged(nameof(NoteLabel));
                OnPropertyChanged(nameof(PreviewText));
            }
        }
    }

    public bool AllowsIncrease => Reason is AdjustmentReason r && AdjustmentReasons.AllowsIncrease(r);

    public bool IsIncrease
    {
        get => _isIncrease;
        set
        {
            if (SetProperty(ref _isIncrease, value))
            {
                QuantityError = string.Empty;
                OnPropertyChanged(nameof(PreviewText));
            }
        }
    }

    public string QuantityText
    {
        get => _quantityText;
        set
        {
            if (SetProperty(ref _quantityText, value))
            {
                QuantityError = string.Empty;
                OnPropertyChanged(nameof(PreviewText));
            }
        }
    }

    public string Note
    {
        get => _note;
        set { if (SetProperty(ref _note, value)) NoteError = string.Empty; }
    }

    public string NoteLabel =>
        Reason is AdjustmentReason r && AdjustmentReasons.RequiresNote(r) ? "Note *" : "Note";

    public string PreviewText
    {
        get
        {
            if (!int.TryParse(QuantityText?.Trim(), out var qty) || qty <= 0)
            {
                return string.Empty;
            }

            var balance = _currentQuantity + (IsIncrease ? qty : -qty);
            return balance < 0
                ? $"Cannot remove {qty:N0}: only {_currentQuantity:N0} on hand."
                : $"New quantity on hand: {balance:N0}";
        }
    }

    public string ReasonError { get => _reasonError; private set => SetProperty(ref _reasonError, value); }
    public string QuantityError { get => _quantityError; private set => SetProperty(ref _quantityError, value); }
    public string NoteError { get => _noteError; private set => SetProperty(ref _noteError, value); }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;

        var valid = true;

        if (Reason is null)
        {
            ReasonError = "Choose a reason.";
            valid = false;
        }

        if (!int.TryParse(QuantityText?.Trim(), out var qty) || qty <= 0 ||
            qty > InventoryService.MaxQuantityPerLine)
        {
            QuantityError = "Enter a whole number above zero.";
            valid = false;
        }
        else if (!IsIncrease && qty > _currentQuantity)
        {
            QuantityError = $"Only {_currentQuantity:N0} on hand.";
            valid = false;
        }

        if (Reason is AdjustmentReason r && AdjustmentReasons.RequiresNote(r) &&
            string.IsNullOrWhiteSpace(Note))
        {
            NoteError = "Please explain this adjustment.";
            valid = false;
        }

        if (!valid)
        {
            ErrorMessage = "Please fix the highlighted fields.";
            return;
        }

        try
        {
            var result = await _inventory.AdjustStockAsync(
                new AdjustStockInput(_batchId, IsIncrease ? qty : -qty, Reason!.Value, Note),
                _userId);

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "The adjustment could not be saved.";
                return;
            }

            _onSaved($"Adjusted {MedicineName}: {(IsIncrease ? "+" : "-")}{qty:N0} ({AdjustmentReasons.Label(Reason.Value)}).");
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Adjusting stock failed");
            ErrorMessage = "The adjustment could not be saved. Nothing was changed. Please try again.";
        }
    }
}
