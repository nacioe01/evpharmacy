
namespace EVPharmacy.Core.Entities;

public enum MovementType
{
    OpeningBalance,
    Receipt,
    Sale,
    Adjustment,
    Return
}

/// <summary>
/// Append-only history of every change to a batch quantity.
/// Rows are never edited or deleted; corrections are new rows.
/// </summary>
public class StockMovement
{
    public const int MaxNotesLength = 300;

    public int Id { get; set; }

    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }

    public int BatchId { get; set; }
    public InventoryBatch? Batch { get; set; }

    public MovementType MovementType { get; set; }

    /// <summary>Positive for stock in, negative for stock out.</summary>
    public int QuantityChange { get; set; }

    /// <summary>Quantity left in the batch right after this movement.</summary>
    public int BalanceAfter { get; set; }

    /// <summary>Set only for adjustments.</summary>
    public AdjustmentReason? AdjustmentReason { get; set; }

    public int? StockReceiptId { get; set; }
    public StockReceipt? StockReceipt { get; set; }

    /// <summary>Set for sale and void-return movements.</summary>
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }

    public string? Notes { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
