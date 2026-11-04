using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed record ReceiptLineRow(string Name, string QtyText, string TotalText);

public sealed record BreakdownRow(string Label, string Value);

/// <summary>A product in the POS search list.</summary>
public sealed class PosProductRow
{
    public PosProductRow(PosProduct product) => Product = product;

    public PosProduct Product { get; }

    public string Name => Product.BrandName is null
        ? Product.GenericName
        : $"{Product.GenericName} ({Product.BrandName})";

    public string Detail => string.Join(" · ", new[] { Product.Strength, Product.DosageFormName }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    public string Price => Money.Format(Product.PriceCentavos);

    public bool CanAdd => Product.AvailableQuantity > 0;

    public string Stock => Product.AvailableQuantity > 0
        ? $"{Product.AvailableQuantity:N0} {Product.UnitName.ToLowerInvariant()}"
        : "Out of stock";

    public string StockKind => Product.AvailableQuantity <= 0 ? StatusKind.Bad : StatusKind.Good;

    public string Rx => Product.PrescriptionClass switch
    {
        PrescriptionClass.Prescription => "Rx",
        PrescriptionClass.Controlled => "Controlled",
        _ => string.Empty
    };

    public bool IsRx => Product.PrescriptionClass != PrescriptionClass.OverTheCounter;
}

/// <summary>One line in the cart. Quantity is kept within what is available.</summary>
public sealed class CartLineViewModel : ObservableObject
{
    private readonly Action<string> _notify;
    private readonly Action _changed;
    private int _quantity;

    public CartLineViewModel(PosProduct product, Action<string> notify, Action changed)
    {
        Product = product;
        _notify = notify;
        _changed = changed;
        _quantity = 1;
    }

    public PosProduct Product { get; }

    public int MedicineId => Product.MedicineId;

    public string Name => Product.BrandName is null
        ? Product.GenericName
        : $"{Product.GenericName} ({Product.BrandName})";

    public string Detail => string.Join(" · ", new[] { Product.Strength, Product.DosageFormName }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    public string UnitPriceText => $"{Money.Format(Product.PriceCentavos)} each";

    public bool IsRx => Product.PrescriptionClass != PrescriptionClass.OverTheCounter;

    public string Rx => Product.PrescriptionClass == PrescriptionClass.Controlled ? "Controlled" : "Rx";

    public int Quantity
    {
        get => _quantity;
        set => SetQuantity(value);
    }

    /// <summary>Two-way text box binding; invalid text snaps back to the current quantity.</summary>
    public string QuantityText
    {
        get => _quantity.ToString();
        set
        {
            if (int.TryParse(value?.Trim(), out var parsed))
            {
                SetQuantity(parsed);
            }

            OnPropertyChanged();
        }
    }

    public long LineTotalCentavos => _quantity * Product.PriceCentavos;

    public string LineTotalText => Money.Format(LineTotalCentavos);

    public void SetQuantity(int value)
    {
        var clamped = Math.Max(1, value);

        if (clamped > Product.AvailableQuantity)
        {
            clamped = Product.AvailableQuantity;
            _notify($"Only {Product.AvailableQuantity:N0} of {Name} available.");
        }

        if (clamped != _quantity)
        {
            _quantity = clamped;
            OnPropertyChanged(nameof(Quantity));
            OnPropertyChanged(nameof(LineTotalCentavos));
            OnPropertyChanged(nameof(LineTotalText));
            _changed();
        }

        OnPropertyChanged(nameof(QuantityText));
    }
}

public sealed class PosViewModel : PageViewModel
{
    private readonly SalesService _sales;
    private readonly SettingsService _settings;
    private readonly ReceiptPrinter _printer;
    private readonly UserSession _session;

    private CancellationTokenSource? _searchCts;
    private string _searchText = string.Empty;
    private PaymentMethod _method = PaymentMethod.Cash;
    private string _tenderedText = string.Empty;
    private string _referenceText = string.Empty;
    private bool _rxVerified;
    private string _errorMessage = string.Empty;
    private string _noticeMessage = string.Empty;
    private SaleDetail? _completedSale;
    private TaxSettings _tax = new(true, 1200, 2000, 2000);
    private bool _settingsConfirmed = true;
    private DiscountType _discountType = DiscountType.None;
    private string _customerName = string.Empty;
    private string _customerId = string.Empty;

    public PosViewModel(SalesService sales, SettingsService settings, ReceiptPrinter printer, UserSession session)
        : base("Point of Sale", "Sell medicines quickly and accurately")
    {
        _sales = sales;
        _settings = settings;
        _printer = printer;
        _session = session;

        Cart.CollectionChanged += (_, _) => OnCartChanged();

        AddToCartCommand = new RelayCommand(p => { if (p is PosProductRow r) AddToCart(r.Product); });
        IncrementCommand = new RelayCommand(p => { if (p is CartLineViewModel l) l.SetQuantity(l.Quantity + 1); });
        DecrementCommand = new RelayCommand(p => { if (p is CartLineViewModel l) l.SetQuantity(l.Quantity - 1); });
        RemoveLineCommand = new RelayCommand(p => { if (p is CartLineViewModel l) Cart.Remove(l); });
        ClearCartCommand = new RelayCommand(ClearCart, () => Cart.Count > 0);
        SetMethodCommand = new RelayCommand(p => { if (p is PaymentMethod m) Method = m; });
        SetTenderedCommand = new RelayCommand(p => SetTendered(p));
        CheckoutCommand = new AsyncRelayCommand(CheckoutAsync, () => Cart.Count > 0);
        NewSaleCommand = new RelayCommand(StartNewSale);
        PrintReceiptCommand = new AsyncRelayCommand(PrintReceiptAsync, () => CompletedSale is not null);
        SetDiscountCommand = new RelayCommand(p => { if (p is DiscountType t) DiscountType = t; });

        _ = LoadSettingsAsync();
        _ = LoadProductsAsync();
    }

    public ObservableCollection<PosProductRow> Products { get; } = [];
    public ObservableCollection<CartLineViewModel> Cart { get; } = [];

    public ICommand AddToCartCommand { get; }
    public ICommand IncrementCommand { get; }
    public ICommand DecrementCommand { get; }
    public ICommand RemoveLineCommand { get; }
    public ICommand ClearCartCommand { get; }
    public ICommand SetMethodCommand { get; }
    public ICommand SetTenderedCommand { get; }
    public ICommand CheckoutCommand { get; }
    public ICommand NewSaleCommand { get; }
    public ICommand PrintReceiptCommand { get; }
    public ICommand SetDiscountCommand { get; }

    // ---------------- Search ----------------

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) _ = ReloadAfterDelayAsync(); }
    }

    public bool ShowNoProducts => Products.Count == 0;

    public string NoProductsText => string.IsNullOrWhiteSpace(SearchText)
        ? "No medicines yet. Add medicines and receive stock first."
        : "No medicines match your search.";

    // ---------------- Cart + totals ----------------

    /// <summary>VAT and discount worked out with the same calculator the sale itself uses.</summary>
    public TaxTotals Tax => TaxCalculator.Calculate(
        _tax, DiscountType, Cart.Select(l => new TaxLineInput(l.LineTotalCentavos, l.Product.IsVatExempt)));

    /// <summary>Amount due after any discount.</summary>
    public long TotalCentavos => Tax.Net;

    public string TotalText => Money.Format(TotalCentavos);

    public int ItemCount => Cart.Sum(l => l.Quantity);

    public string CartSummary => Cart.Count == 0
        ? "No items yet"
        : $"{Cart.Count} line{(Cart.Count == 1 ? "" : "s")} · {ItemCount:N0} item{(ItemCount == 1 ? "" : "s")}";

    public bool IsCartEmpty => Cart.Count == 0;

    public bool HasCartItems => Cart.Count > 0;

    public bool RequiresRx => Cart.Any(l => l.IsRx);

    public bool RxVerified
    {
        get => _rxVerified;
        set => SetProperty(ref _rxVerified, value);
    }

    // ---------------- Discount and tax ----------------

    public DiscountType DiscountType
    {
        get => _discountType;
        private set
        {
            if (SetProperty(ref _discountType, value))
            {
                ErrorMessage = string.Empty;
                OnPropertyChanged(nameof(IsNoDiscount));
                OnPropertyChanged(nameof(IsSenior));
                OnPropertyChanged(nameof(IsPwd));
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(CustomerIdLabel));
                OnPropertyChanged(nameof(DiscountHint));
                OnTotalsChanged();
            }
        }
    }

    public bool IsNoDiscount => DiscountType == DiscountType.None;
    public bool IsSenior => DiscountType == DiscountType.SeniorCitizen;
    public bool IsPwd => DiscountType == DiscountType.Pwd;
    public bool HasDiscount => DiscountType != DiscountType.None;

    public string CustomerName
    {
        get => _customerName;
        set { if (SetProperty(ref _customerName, value)) ErrorMessage = string.Empty; }
    }

    public string CustomerId
    {
        get => _customerId;
        set { if (SetProperty(ref _customerId, value)) ErrorMessage = string.Empty; }
    }

    public string CustomerIdLabel => DiscountType == DiscountType.Pwd ? "PWD ID number *" : "Senior Citizen ID number *";

    public string DiscountHint
    {
        get
        {
            var pct = _tax.DiscountBps(DiscountType) / 100m;
            var who = DiscountType == DiscountType.Pwd ? "PWD" : "Senior Citizen";
            return _tax.IsVatRegistered
                ? $"{who} purchase: VAT is removed, then {pct:0.##}% is taken off. Check the ID and use the name shown on it."
                : $"{who} purchase: {pct:0.##}% is taken off. Check the ID and use the name shown on it.";
        }
    }

    /// <summary>Rows shown above the total, for example subtotal, VAT removed, discount.</summary>
    public IReadOnlyList<BreakdownRow> Breakdown
    {
        get
        {
            var t = Tax;
            var rows = new List<BreakdownRow>();

            if (Cart.Count == 0)
            {
                return rows;
            }

            if (HasDiscount)
            {
                var pct = _tax.DiscountBps(DiscountType) / 100m;
                var label = DiscountType == DiscountType.Pwd ? "PWD" : "Senior Citizen";

                rows.Add(new BreakdownRow("Subtotal", Money.Format(t.Gross)));
                if (t.VatRemoved > 0)
                {
                    rows.Add(new BreakdownRow("Less: VAT", $"-{Money.Format(t.VatRemoved)}"));
                }

                rows.Add(new BreakdownRow($"Less: {pct:0.##}% {label} discount", $"-{Money.Format(t.Discount)}"));
            }
            else if (_tax.IsVatRegistered && t.VatAmount > 0)
            {
                rows.Add(new BreakdownRow("VAT included", Money.Format(t.VatAmount)));
            }

            return rows;
        }
    }

    public bool HasBreakdown => Breakdown.Count > 0;

    public bool SettingsNotConfirmed => !_settingsConfirmed;

    // ---------------- Payment ----------------

    public PaymentMethod Method
    {
        get => _method;
        private set
        {
            if (SetProperty(ref _method, value))
            {
                OnPropertyChanged(nameof(IsCash));
                OnPropertyChanged(nameof(IsCard));
                OnPropertyChanged(nameof(IsEWallet));
                OnPropertyChanged(nameof(ShowReference));
                OnPropertyChanged(nameof(ChangeText));
                OnPropertyChanged(nameof(ChangeKind));
            }
        }
    }

    public bool IsCash => Method == PaymentMethod.Cash;
    public bool IsCard => Method == PaymentMethod.Card;
    public bool IsEWallet => Method == PaymentMethod.EWallet;
    public bool ShowReference => Method != PaymentMethod.Cash;

    public string TenderedText
    {
        get => _tenderedText;
        set
        {
            if (SetProperty(ref _tenderedText, value))
            {
                ErrorMessage = string.Empty;
                OnPropertyChanged(nameof(ChangeText));
                OnPropertyChanged(nameof(ChangeKind));
            }
        }
    }

    public string ReferenceText
    {
        get => _referenceText;
        set => SetProperty(ref _referenceText, value);
    }

    public string ChangeText
    {
        get
        {
            if (!IsCash || Cart.Count == 0)
            {
                return string.Empty;
            }

            if (!Money.TryParsePesos(TenderedText, out var pesos) || pesos < 0)
            {
                return "Enter the cash received";
            }

            var diff = Money.ToCentavos(pesos) - TotalCentavos;
            return diff >= 0
                ? $"Change: {Money.Format(diff)}"
                : $"Short by {Money.Format(-diff)}";
        }
    }

    public string ChangeKind
    {
        get
        {
            if (!IsCash || Cart.Count == 0 || !Money.TryParsePesos(TenderedText, out var pesos))
            {
                return StatusKind.Neutral;
            }

            return Money.ToCentavos(pesos) >= TotalCentavos ? StatusKind.Good : StatusKind.Bad;
        }
    }

    public IReadOnlyList<decimal> QuickBills { get; } = [100m, 200m, 500m, 1000m];

    // ---------------- Messages / completion ----------------

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string NoticeMessage
    {
        get => _noticeMessage;
        private set { if (SetProperty(ref _noticeMessage, value)) OnPropertyChanged(nameof(HasNotice)); }
    }

    public bool HasNotice => !string.IsNullOrEmpty(NoticeMessage);

    public SaleDetail? CompletedSale
    {
        get => _completedSale;
        private set
        {
            if (SetProperty(ref _completedSale, value))
            {
                OnPropertyChanged(nameof(IsSaleComplete));
                OnPropertyChanged(nameof(IsSelling));
            }
        }
    }

    public bool IsSaleComplete => CompletedSale is not null;

    public bool IsSelling => CompletedSale is null;

    public string CompletedReceiptNumber => CompletedSale is null ? string.Empty : $"Receipt {CompletedSale.ReceiptNumber}";

    public string CompletedTotal => CompletedSale is null ? string.Empty : Money.Format(CompletedSale.TotalCentavos);

    public IReadOnlyList<ReceiptLineRow> CompletedLines =>
        CompletedSale?.Lines
            .Select(l => new ReceiptLineRow(l.MedicineName, $"{l.Quantity:N0} × {Money.Format(l.UnitPriceCentavos)}", Money.Format(l.LineTotalCentavos)))
            .ToList()
        ?? [];

    /// <summary>Tax / discount lines for the completed sale, for the on-screen summary.</summary>
    public IReadOnlyList<BreakdownRow> CompletedBreakdown
    {
        get
        {
            var s = CompletedSale;
            var rows = new List<BreakdownRow>();
            if (s is null)
            {
                return rows;
            }

            if (s.DiscountType != DiscountType.None)
            {
                var label = s.DiscountType == DiscountType.Pwd ? "PWD" : "Senior Citizen";
                rows.Add(new BreakdownRow("Subtotal", Money.Format(s.SubtotalCentavos)));
                if (s.VatRemovedCentavos > 0)
                {
                    rows.Add(new BreakdownRow("Less: VAT", $"-{Money.Format(s.VatRemovedCentavos)}"));
                }

                rows.Add(new BreakdownRow($"Less: {s.DiscountBps / 100m:0.##}% {label} discount", $"-{Money.Format(s.DiscountCentavos)}"));
                rows.Add(new BreakdownRow($"{label}: {s.CustomerName}", s.CustomerIdNumber ?? string.Empty));
            }
            else if (s.IsVatRegistered && s.VatAmountCentavos > 0)
            {
                rows.Add(new BreakdownRow("VAT included", Money.Format(s.VatAmountCentavos)));
            }

            return rows;
        }
    }

    public string CompletedSummary
    {
        get
        {
            var s = CompletedSale;
            if (s is null)
            {
                return string.Empty;
            }

            return s.PaymentMethod == PaymentMethod.Cash
                ? $"Cash {Money.Format(s.AmountTenderedCentavos)} · Change {Money.Format(s.ChangeCentavos)}"
                : $"{(s.PaymentMethod == PaymentMethod.Card ? "Card" : "E-wallet")} {Money.Format(s.TotalCentavos)}" +
                  (string.IsNullOrEmpty(s.PaymentReference) ? string.Empty : $" · Ref {s.PaymentReference}");
        }
    }

    // ------------------------------------------------------------------

    private async Task LoadSettingsAsync()
    {
        try
        {
            var s = await _settings.GetAsync();
            _tax = s.ToTaxSettings();
            _settingsConfirmed = s.IsConfirmed;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading store settings for POS failed");
        }

        OnPropertyChanged(nameof(SettingsNotConfirmed));
        OnPropertyChanged(nameof(DiscountHint));
        OnTotalsChanged();
    }

    private void OnTotalsChanged()
    {
        OnPropertyChanged(nameof(Tax));
        OnPropertyChanged(nameof(TotalCentavos));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(Breakdown));
        OnPropertyChanged(nameof(HasBreakdown));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(ChangeKind));
    }

    private async Task ReloadAfterDelayAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(250, cts.Token);
            await LoadProductsAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "POS search failed");
        }
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            var found = await _sales.SearchProductsAsync(SearchText);

            Products.Clear();
            foreach (var p in found)
            {
                Products.Add(new PosProductRow(p));
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading POS products failed");
            ErrorMessage = "The medicine list could not be loaded.";
        }

        OnPropertyChanged(nameof(ShowNoProducts));
        OnPropertyChanged(nameof(NoProductsText));
    }

    private void AddToCart(PosProduct product)
    {
        ErrorMessage = string.Empty;
        NoticeMessage = string.Empty;

        if (product.AvailableQuantity <= 0)
        {
            NoticeMessage = $"{product.GenericName} is out of stock.";
            return;
        }

        var existing = Cart.FirstOrDefault(l => l.MedicineId == product.MedicineId);
        if (existing is not null)
        {
            existing.SetQuantity(existing.Quantity + 1);
            return;
        }

        Cart.Add(new CartLineViewModel(product, message => NoticeMessage = message, OnCartChanged));
    }

    private void OnCartChanged()
    {
        OnTotalsChanged();
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(CartSummary));
        OnPropertyChanged(nameof(IsCartEmpty));
        OnPropertyChanged(nameof(HasCartItems));
        OnPropertyChanged(nameof(RequiresRx));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(ChangeKind));

        if (!RequiresRx)
        {
            RxVerified = false;
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private void ClearCart()
    {
        Cart.Clear();
        ResetPayment();
        ErrorMessage = string.Empty;
        NoticeMessage = string.Empty;
    }

    private void ResetPayment()
    {
        TenderedText = string.Empty;
        ReferenceText = string.Empty;
        RxVerified = false;
        Method = PaymentMethod.Cash;
        DiscountType = DiscountType.None;
        CustomerName = string.Empty;
        CustomerId = string.Empty;
    }

    private void SetTendered(object? parameter)
    {
        // "exact" fills the total; a number fills that amount.
        if (parameter is string s && s == "exact")
        {
            TenderedText = Money.ToPesos(TotalCentavos).ToString("0.00");
        }
        else if (parameter is decimal d)
        {
            TenderedText = d.ToString("0.00");
        }
    }

    private async Task CheckoutAsync()
    {
        ErrorMessage = string.Empty;
        NoticeMessage = string.Empty;

        if (Cart.Count == 0)
        {
            return;
        }

        long tendered = 0;
        if (Method == PaymentMethod.Cash)
        {
            if (!Money.TryParsePesos(TenderedText, out var pesos) || pesos < 0)
            {
                ErrorMessage = "Enter the cash received from the customer.";
                return;
            }

            tendered = Money.ToCentavos(pesos);

            if (tendered < TotalCentavos)
            {
                ErrorMessage = $"The cash received is less than the total ({TotalText}).";
                return;
            }
        }

        if (HasDiscount)
        {
            var who = DiscountType == DiscountType.Pwd ? "PWD" : "Senior Citizen";

            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                ErrorMessage = $"Enter the {who}'s full name as shown on the ID.";
                return;
            }

            if (string.IsNullOrWhiteSpace(CustomerId))
            {
                ErrorMessage = $"Enter the {who} ID number.";
                return;
            }
        }

        if (RequiresRx && !RxVerified)
        {
            ErrorMessage = "This sale includes prescription medicines. Tick the box to confirm the prescription was checked.";
            return;
        }

        try
        {
            var input = new CheckoutInput(
                Cart.Select(l => new CartLineInput(l.MedicineId, l.Quantity)).ToList(),
                Method,
                tendered,
                ReferenceText,
                RxVerified,
                DiscountType,
                CustomerName,
                CustomerId);

            var result = await _sales.CompleteSaleAsync(input, _session.CurrentUser?.Id);

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "The sale could not be completed.";
                // Stock may have changed since the list was loaded.
                await LoadProductsAsync();
                return;
            }

            CompletedSale = await _sales.GetSaleAsync(result.Id!.Value);
            OnPropertyChanged(nameof(CompletedSummary));
            OnPropertyChanged(nameof(CompletedReceiptNumber));
            OnPropertyChanged(nameof(CompletedTotal));
            OnPropertyChanged(nameof(CompletedLines));
            OnPropertyChanged(nameof(CompletedBreakdown));

            Cart.Clear();
            ResetPayment();
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Completing sale failed");
            ErrorMessage = "The sale could not be completed. Nothing was recorded and the cart is unchanged. Please try again.";
        }
    }

    private async Task PrintReceiptAsync()
    {
        if (CompletedSale is null)
        {
            return;
        }

        try
        {
            if (await _printer.PrintAsync(CompletedSale.Id, isReprint: false))
            {
                NoticeMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Printing receipt failed");
            ErrorMessage = "The receipt could not be printed. Check that the printer is connected and try again.";
        }
    }

    private void StartNewSale()
    {
        CompletedSale = null;
        SearchText = string.Empty;
        ErrorMessage = string.Empty;
        NoticeMessage = string.Empty;
    }
}
