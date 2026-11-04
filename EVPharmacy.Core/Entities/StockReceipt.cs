
namespace EVPharmacy.Core.Entities;

public enum ReceiptType
{
    /// <summary>Stock delivered by a supplier.</summary>
    Delivery,

    /// <summary>Stock already on the shelf when the system started being used.</summary>
    OpeningBalance
}

/// <summary>Header for one stock receiving event (a delivery or an opening balance entry).</summary>
public class StockReceipt
{
    public const int MaxInvoiceLength = 60;
    public const int MaxNotesLength = 300;

    public int Id { get; set; }

    public ReceiptType ReceiptType { get; set; } = ReceiptType.Delivery;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string? InvoiceNumber { get; set; }

    /// <summary>The date the goods were physically received.</summary>
    public DateOnly ReceivedDate { get; set; }

    public string? Notes { get; set; }

    public int? ReceivedByUserId { get; set; }
    public User? ReceivedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<InventoryBatch> Batches { get; set; } = [];
}
