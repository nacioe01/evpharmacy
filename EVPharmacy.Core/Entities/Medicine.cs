
namespace EVPharmacy.Core.Entities;

/// <summary>
/// Product master record. Stock quantities, batches and expiry dates live in
/// inventory batches (a later phase), never here. Money is stored in centavos.
/// </summary>
public class Medicine
{
    public const int MaxNameLength = 120;
    public const int MaxBarcodeLength = 50;
    public const int MaxStrengthLength = 40;

    public int Id { get; set; }

    public required string GenericName { get; set; }

    public string? BrandName { get; set; }

    public string? Barcode { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int DosageFormId { get; set; }
    public DosageForm? DosageForm { get; set; }

    public string? Strength { get; set; }

    public int UnitOfMeasureId { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }

    public long PurchaseCostCentavos { get; set; }

    public long SellingPriceCentavos { get; set; }

    public int ReorderLevel { get; set; }

    public PrescriptionClass PrescriptionClass { get; set; } = PrescriptionClass.OverTheCounter;

    /// <summary>Never assumed. Defaults to false; VAT rules are applied in a later phase.</summary>
    public bool IsVatExempt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public List<MedicinePriceHistory> PriceHistory { get; set; } = [];

    /// <summary>"Brand (Generic) Strength" style label for lists and receipts.</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(BrandName)
            ? GenericName
            : $"{BrandName} ({GenericName})";
}
