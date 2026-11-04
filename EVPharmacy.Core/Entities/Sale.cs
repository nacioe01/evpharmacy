
namespace EVPharmacy.Core.Entities;

public enum SaleStatus
{
    Completed,

    /// <summary>Cancelled after the fact. The record is kept and stock is returned.</summary>
    Voided
}

public enum PaymentMethod
{
    Cash,
    Card,

    /// <summary>GCash, Maya and similar.</summary>
    EWallet
}

/// <summary>
/// One completed transaction. Sales are never deleted; a mistaken sale is voided
/// and keeps its record. Prices on the items are a snapshot at the time of sale.
/// </summary>
public class Sale
{
    public const int MaxReferenceLength = 50;
    public const int MaxVoidReasonLength = 200;
    public const int MaxCustomerNameLength = 120;
    public const int MaxCustomerIdLength = 50;

    public int Id { get; set; }

    /// <summary>Human-readable and unique, for example 20261104-0007.</summary>
    public required string ReceiptNumber { get; set; }

    public DateTime SoldAt { get; set; } = DateTime.UtcNow;

    public int? CashierUserId { get; set; }
    public User? CashierUser { get; set; }

    /// <summary>Sum of the VAT-inclusive prices before any discount.</summary>
    public long SubtotalCentavos { get; set; }

    /// <summary>Amount due after discount (what the customer pays).</summary>
    public long TotalCentavos { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    /// <summary>Cash handed over by the customer. Equals the total for non-cash payments.</summary>
    public long AmountTenderedCentavos { get; set; }

    public long ChangeCentavos { get; set; }

    /// <summary>Card slip or e-wallet reference, when given.</summary>
    public string? PaymentReference { get; set; }

    /// <summary>The cashier confirmed the prescription for Rx / controlled items in this sale.</summary>
    public bool RxVerified { get; set; }

    // ----- Tax and discount (Phase 5). All amounts are integer centavos. -----

    /// <summary>Snapshot of the rules used, so a reprint never changes if settings change later.</summary>
    public bool IsVatRegistered { get; set; }

    public int VatRateBps { get; set; }

    public DiscountType DiscountType { get; set; } = DiscountType.None;

    public int DiscountBps { get; set; }

    /// <summary>Total Senior Citizen / PWD discount given.</summary>
    public long DiscountCentavos { get; set; }

    /// <summary>VAT removed from the price because the buyer is a Senior Citizen / PWD.</summary>
    public long VatRemovedCentavos { get; set; }

    public long VatableSalesCentavos { get; set; }

    public long VatExemptSalesCentavos { get; set; }

    /// <summary>Sales of a store that is not VAT-registered.</summary>
    public long NonVatSalesCentavos { get; set; }

    /// <summary>VAT contained in the total.</summary>
    public long VatAmountCentavos { get; set; }

    /// <summary>Required for Senior Citizen / PWD sales: the person named on the ID.</summary>
    public string? CustomerName { get; set; }

    /// <summary>Required for Senior Citizen / PWD sales: the ID number presented.</summary>
    public string? CustomerIdNumber { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public string? VoidReason { get; set; }

    public DateTime? VoidedAt { get; set; }

    public int? VoidedByUserId { get; set; }
    public User? VoidedByUser { get; set; }

    public List<SaleItem> Items { get; set; } = [];
}
