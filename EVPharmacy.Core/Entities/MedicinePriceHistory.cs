
namespace EVPharmacy.Core.Entities;

/// <summary>
/// Append-only record of a cost or selling price change.
/// The first entry for a medicine has null "old" values.
/// </summary>
public class MedicinePriceHistory
{
    public const int MaxReasonLength = 200;

    public int Id { get; set; }

    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }

    public long? OldPurchaseCostCentavos { get; set; }
    public long NewPurchaseCostCentavos { get; set; }

    public long? OldSellingPriceCentavos { get; set; }
    public long NewSellingPriceCentavos { get; set; }

    public string? Reason { get; set; }

    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
