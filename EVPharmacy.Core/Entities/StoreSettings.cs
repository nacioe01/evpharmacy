
namespace EVPharmacy.Core.Entities;

public enum DiscountType
{
    None,
    SeniorCitizen,
    Pwd
}

/// <summary>
/// The single settings row (Id is always 1): business details for receipts and the tax rules.
/// Percentages are in basis points (1200 = 12.00%).
/// </summary>
public class StoreSettings
{
    public const int SingletonId = 1;

    public const int MaxNameLength = 120;
    public const int MaxAddressLength = 250;
    public const int MaxTinLength = 30;
    public const int MaxPermitLength = 60;
    public const int MaxContactLength = 60;
    public const int MaxFooterLength = 200;

    public int Id { get; set; } = SingletonId;

    public string StoreName { get; set; } = "EV Pharmacy";

    public string? Address { get; set; }

    /// <summary>Taxpayer Identification Number printed on receipts.</summary>
    public string? Tin { get; set; }

    /// <summary>BIR permit / authority to print / accreditation line, as the owner wants it printed.</summary>
    public string? PermitInfo { get; set; }

    public string? ContactNumber { get; set; }

    public string? ReceiptFooter { get; set; }

    public bool IsVatRegistered { get; set; } = true;

    public int VatRateBps { get; set; } = 1200;

    public int SeniorDiscountBps { get; set; } = 2000;

    public int PwdDiscountBps { get; set; } = 2000;

    /// <summary>False until an administrator reviews and saves the settings once.</summary>
    public bool IsConfirmed { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public TaxSettings ToTaxSettings() =>
        new(IsVatRegistered, VatRateBps, SeniorDiscountBps, PwdDiscountBps);
}
