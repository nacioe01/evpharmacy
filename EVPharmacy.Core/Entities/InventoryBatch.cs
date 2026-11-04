
namespace EVPharmacy.Core.Entities;

public enum BatchStatus
{
    /// <summary>Can be sold (unless expired).</summary>
    Available,

    /// <summary>Held back pending a check. Cannot be sold.</summary>
    Quarantined,

    /// <summary>Recalled by the supplier or manufacturer. Cannot be sold.</summary>
    Recalled
}

/// <summary>
/// One lot of a medicine, with its own expiry date and remaining quantity.
/// Stock is always tracked per batch so FEFO selling and recalls are possible.
/// </summary>
public class InventoryBatch
{
    public const int MaxBatchNumberLength = 50;
    public const int MaxStatusNoteLength = 200;

    public int Id { get; set; }

    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }

    public int? StockReceiptId { get; set; }
    public StockReceipt? StockReceipt { get; set; }

    public required string BatchNumber { get; set; }

    public DateOnly ExpiryDate { get; set; }

    public int QuantityReceived { get; set; }

    public int QuantityOnHand { get; set; }

    public long UnitCostCentavos { get; set; }

    public BatchStatus Status { get; set; } = BatchStatus.Available;

    public string? StatusNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsExpired(DateOnly today) => ExpiryDate < today;

    /// <summary>True when this batch may be sold today.</summary>
    public bool IsSellable(DateOnly today) =>
        Status == BatchStatus.Available && !IsExpired(today) && QuantityOnHand > 0;
}
