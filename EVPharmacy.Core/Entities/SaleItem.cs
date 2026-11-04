
namespace EVPharmacy.Core.Entities;

/// <summary>
/// One batch allocation of a sale. Selling 12 units that FEFO takes from two batches
/// produces two rows, so the exact batches sold are always traceable (for recalls).
/// </summary>
public class SaleItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }

    public int BatchId { get; set; }
    public InventoryBatch? Batch { get; set; }

    /// <summary>Name as it was at the time of sale, so receipts never change retroactively.</summary>
    public required string MedicineName { get; set; }

    public int Quantity { get; set; }

    public long UnitPriceCentavos { get; set; }

    /// <summary>Cost of the batch at the time of sale, for margin reports.</summary>
    public long UnitCostCentavos { get; set; }

    public long LineTotalCentavos { get; set; }

    /// <summary>The medicine's VAT-exempt flag as it was at the time of sale.</summary>
    public bool IsVatExempt { get; set; }

    public long VatAmountCentavos { get; set; }

    public long VatRemovedCentavos { get; set; }

    public long DiscountCentavos { get; set; }

    /// <summary>What the customer paid for this row (LineTotal minus VAT removed minus discount).</summary>
    public long NetCentavos { get; set; }

    public PrescriptionClass PrescriptionClass { get; set; }
}
