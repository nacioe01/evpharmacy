using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Globalization;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

/// <summary>Store details (printed on receipts) and the VAT / Senior Citizen / PWD rules.</summary>
public sealed class SettingsViewModel : PageViewModel
{
    private readonly SettingsService _settings;

    private string _storeName = string.Empty;
    private string _address = string.Empty;
    private string _tin = string.Empty;
    private string _permitInfo = string.Empty;
    private string _contactNumber = string.Empty;
    private string _receiptFooter = string.Empty;
    private bool _isVatRegistered = true;
    private string _vatRateText = "12";
    private string _seniorText = "20";
    private string _pwdText = "20";
    private bool _isConfirmed;
    private string _storeNameError = string.Empty;
    private string _vatError = string.Empty;
    private string _seniorError = string.Empty;
    private string _pwdError = string.Empty;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(SettingsService settings)
        : base("Settings", "Store details, tax rules and receipts")
    {
        _settings = settings;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        _ = LoadAsync();
    }

    public ICommand SaveCommand { get; }

    public string StoreName
    {
        get => _storeName;
        set { if (SetProperty(ref _storeName, value)) StoreNameError = string.Empty; }
    }

    public string Address { get => _address; set => SetProperty(ref _address, value); }
    public string Tin { get => _tin; set => SetProperty(ref _tin, value); }
    public string PermitInfo { get => _permitInfo; set => SetProperty(ref _permitInfo, value); }
    public string ContactNumber { get => _contactNumber; set => SetProperty(ref _contactNumber, value); }
    public string ReceiptFooter { get => _receiptFooter; set => SetProperty(ref _receiptFooter, value); }

    public bool IsVatRegistered
    {
        get => _isVatRegistered;
        set
        {
            if (SetProperty(ref _isVatRegistered, value))
            {
                OnPropertyChanged(nameof(VatNote));
            }
        }
    }

    public string VatNote => IsVatRegistered
        ? "Selling prices are treated as VAT-inclusive. Receipts show VATable, VAT-exempt and VAT amounts."
        : "No VAT is computed. Receipts show the total only (non-VAT).";

    public string VatRateText
    {
        get => _vatRateText;
        set { if (SetProperty(ref _vatRateText, value)) VatError = string.Empty; }
    }

    public string SeniorText
    {
        get => _seniorText;
        set { if (SetProperty(ref _seniorText, value)) SeniorError = string.Empty; }
    }

    public string PwdText
    {
        get => _pwdText;
        set { if (SetProperty(ref _pwdText, value)) PwdError = string.Empty; }
    }

    public bool IsConfirmed
    {
        get => _isConfirmed;
        private set { if (SetProperty(ref _isConfirmed, value)) OnPropertyChanged(nameof(IsUnconfirmed)); }
    }

    public bool IsUnconfirmed => !IsConfirmed;

    public string StoreNameError { get => _storeNameError; private set => SetProperty(ref _storeNameError, value); }
    public string VatError { get => _vatError; private set => SetProperty(ref _vatError, value); }
    public string SeniorError { get => _seniorError; private set => SetProperty(ref _seniorError, value); }
    public string PwdError { get => _pwdError; private set => SetProperty(ref _pwdError, value); }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatus)); }
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    private async Task LoadAsync()
    {
        try
        {
            var s = await _settings.GetAsync();

            StoreName = s.StoreName;
            Address = s.Address ?? string.Empty;
            Tin = s.Tin ?? string.Empty;
            PermitInfo = s.PermitInfo ?? string.Empty;
            ContactNumber = s.ContactNumber ?? string.Empty;
            ReceiptFooter = s.ReceiptFooter ?? string.Empty;
            IsVatRegistered = s.IsVatRegistered;
            VatRateText = Format(s.VatRateBps);
            SeniorText = Format(s.SeniorDiscountBps);
            PwdText = Format(s.PwdDiscountBps);
            IsConfirmed = s.IsConfirmed;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading settings failed");
            ErrorMessage = "The settings could not be loaded.";
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        var valid = true;

        if (string.IsNullOrWhiteSpace(StoreName))
        {
            StoreNameError = "Enter the store name.";
            valid = false;
        }

        valid &= TryPercent(VatRateText, "VAT rate", out var vat, e => VatError = e);
        valid &= TryPercent(SeniorText, "Senior Citizen discount", out var senior, e => SeniorError = e);
        valid &= TryPercent(PwdText, "PWD discount", out var pwd, e => PwdError = e);

        if (!valid)
        {
            ErrorMessage = "Please fix the highlighted fields.";
            return;
        }

        try
        {
            var result = await _settings.SaveAsync(new StoreSettingsInput(
                StoreName, Address, Tin, PermitInfo, ContactNumber, ReceiptFooter,
                IsVatRegistered, vat, senior, pwd));

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "The settings could not be saved.";
                return;
            }

            IsConfirmed = true;
            StatusMessage = "Settings saved. New sales and receipts use them straight away; past sales keep the values they were made with.";
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Saving settings failed");
            ErrorMessage = "The settings could not be saved. Please try again.";
        }
    }

    private static bool TryPercent(string text, string name, out decimal value, Action<string> setError)
    {
        value = 0;

        if (!decimal.TryParse(text?.Trim().TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out value) &&
            !decimal.TryParse(text?.Trim().TrimEnd('%'), NumberStyles.Number, CultureInfo.CurrentCulture, out value))
        {
            setError($"Enter {name} as a number, for example 12.");
            return false;
        }

        if (value < 0 || value > 100)
        {
            setError("Must be between 0 and 100.");
            return false;
        }

        if (decimal.Round(value, 2) != value)
        {
            setError("Use at most two decimal places.");
            return false;
        }

        return true;
    }

    private static string Format(int bps) => (bps / 100m).ToString("0.##", CultureInfo.InvariantCulture);
}
