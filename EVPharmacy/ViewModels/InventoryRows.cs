using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;

namespace EVPharmacy.ViewModels;

/// <summary>Display kinds used by the status pill style (good / warn / bad / neutral).</summary>
public static class StatusKind
{
    public const string Good = "good";
    public const string Warn = "warn";
    public const string Bad = "bad";
    public const string Neutral = "neutral";
}

public sealed class StockRow
{
    public StockRow(StockSummaryItem item)
    {
        Item = item;

        if (item.SellableQuantity <= 0)
        {
            Status = "Out of stock";
            Kind = StatusKind.Bad;
        }
        else if (item.IsLowStock)
        {
            Status = "Low stock";
            Kind = StatusKind.Warn;
        }
        else
        {
            Status = "In stock";
            Kind = StatusKind.Good;
        }
    }

    public StockSummaryItem Item { get; }

    public string Medicine => Item.GenericName;
    public string Brand => Item.BrandName ?? "—";
    public string Strength => Item.Strength ?? "—";
    public string OnHand => $"{Item.SellableQuantity:N0} {Item.UnitName.ToLowerInvariant()}";
    public int ReorderLevel => Item.ReorderLevel;
    public string ExpiringSoon => Item.ExpiringSoonQuantity > 0 ? Item.ExpiringSoonQuantity.ToString("N0") : "—";
    public string Expired => Item.ExpiredQuantity > 0 ? Item.ExpiredQuantity.ToString("N0") : "—";
    public string Held => Item.HeldQuantity > 0 ? Item.HeldQuantity.ToString("N0") : "—";
    public string NextExpiry => Item.NextExpiry is DateOnly d ? d.ToString("MMM d, yyyy") : "—";
    public string Status { get; }
    public string Kind { get; }
}

public sealed class BatchRow
{
    public BatchRow(BatchItem item)
    {
        Item = item;

        var today = InventoryService.Today;
        var daysLeft = item.ExpiryDate.DayNumber - today.DayNumber;

        if (item.Status == BatchStatus.Recalled)
        {
            Status = "Recalled";
            Kind = StatusKind.Bad;
        }
        else if (item.Status == BatchStatus.Quarantined)
        {
            Status = "On hold";
            Kind = StatusKind.Warn;
        }
        else if (daysLeft < 0)
        {
            Status = "Expired";
            Kind = StatusKind.Bad;
        }
        else if (daysLeft <= InventoryService.ExpiringSoonDays)
        {
            Status = "Expiring soon";
            Kind = StatusKind.Warn;
        }
        else
        {
            Status = "Available";
            Kind = StatusKind.Good;
        }

        ExpiryDetail = daysLeft < 0
            ? $"{-daysLeft} day{(daysLeft == -1 ? "" : "s")} ago"
            : daysLeft == 0 ? "today" : $"in {daysLeft} day{(daysLeft == 1 ? "" : "s")}";
    }

    public BatchItem Item { get; }

    public int Id => Item.Id;
    public string Medicine => Item.BrandName is null ? Item.GenericName : $"{Item.GenericName} ({Item.BrandName})";
    public string BatchNumber => Item.BatchNumber;
    public string Expiry => Item.ExpiryDate.ToString("MMM d, yyyy");
    public string ExpiryDetail { get; }
    public string Quantity => $"{Item.QuantityOnHand:N0} / {Item.QuantityReceived:N0}";
    public string UnitCost => Money.Format(Item.UnitCostCentavos);
    public string Supplier => Item.SupplierName ?? "—";
    public string Received => Item.ReceivedDate is DateOnly d ? d.ToString("MMM d, yyyy") : "—";
    public string Source => Item.SupplierName ?? (Item.ReceivedDate is null ? "—" : "Opening balance");
    public string Note => Item.StatusNote ?? string.Empty;
    public string Status { get; }
    public string Kind { get; }

    public bool CanAdjust => Item.QuantityOnHand > 0;
    public bool CanHold => Item.Status == BatchStatus.Available;
    public bool CanRecall => Item.Status != BatchStatus.Recalled;
    public bool CanRelease => Item.Status != BatchStatus.Available;
}

public sealed class MovementRow
{
    public MovementRow(MovementItem item) => Item = item;

    public MovementItem Item { get; }

    public string When => Item.CreatedAtUtc.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
    public string Medicine => Item.BrandName is null ? Item.GenericName : $"{Item.GenericName} ({Item.BrandName})";
    public string BatchNumber => Item.BatchNumber;

    public string Type => Item.MovementType switch
    {
        MovementType.OpeningBalance => "Opening balance",
        MovementType.Receipt => "Stock received",
        MovementType.Sale => "Sale",
        MovementType.Adjustment => Item.AdjustmentReason is AdjustmentReason r
            ? AdjustmentReasons.Label(r)
            : "Adjustment",
        MovementType.Return => "Return",
        _ => Item.MovementType.ToString()
    };

    public string Change => Item.QuantityChange > 0 ? $"+{Item.QuantityChange:N0}" : Item.QuantityChange.ToString("N0");
    public string Kind => Item.QuantityChange >= 0 ? StatusKind.Good : StatusKind.Bad;
    public string Balance => Item.BalanceAfter.ToString("N0");
    public string By => Item.UserName ?? "—";
    public string Notes => Item.Notes ?? string.Empty;
}
