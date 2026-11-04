using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;

namespace EVPharmacy.ViewModels;

public sealed record PrescriptionOption(PrescriptionClass Value, string Label)
{
    public static IReadOnlyList<PrescriptionOption> All { get; } =
    [
        new(PrescriptionClass.OverTheCounter, "Over the counter (OTC)"),
        new(PrescriptionClass.Prescription, "Prescription required (Rx)"),
        new(PrescriptionClass.Controlled, "Controlled / dangerous drug")
    ];

    public static string Short(PrescriptionClass value) => value switch
    {
        PrescriptionClass.Prescription => "Rx",
        PrescriptionClass.Controlled => "Controlled",
        _ => "OTC"
    };
}

/// <summary>Display wrapper for one medicine row in the list.</summary>
public sealed class MedicineRow
{
    public MedicineRow(MedicineListItem item) => Item = item;

    public MedicineListItem Item { get; }

    public int Id => Item.Id;
    public string GenericName => Item.GenericName;
    public string BrandName => Item.BrandName ?? "—";
    public string Strength => Item.Strength ?? "—";
    public string DosageForm => Item.DosageFormName;
    public string Category => Item.CategoryName;
    public string Prescription => PrescriptionOption.Short(Item.PrescriptionClass);
    public string Cost => Money.Format(Item.PurchaseCostCentavos);
    public string Price => Money.Format(Item.SellingPriceCentavos);
    public string Unit => Item.UnitName;
    public int ReorderLevel => Item.ReorderLevel;
    public bool IsActive => Item.IsActive;
    public string Status => Item.IsActive ? "Active" : "Inactive";
    public string ToggleActiveText => Item.IsActive ? "Deactivate" : "Reactivate";
}

/// <summary>Display wrapper for one price history entry.</summary>
public sealed class PriceHistoryRow
{
    public PriceHistoryRow(PriceHistoryEntry entry)
    {
        When = entry.ChangedAtUtc.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
        Cost = Describe(entry.OldPurchaseCostCentavos, entry.NewPurchaseCostCentavos);
        Price = Describe(entry.OldSellingPriceCentavos, entry.NewSellingPriceCentavos);
        Reason = entry.Reason ?? "—";
        ChangedBy = entry.ChangedBy ?? "—";
    }

    public string When { get; }
    public string Cost { get; }
    public string Price { get; }
    public string Reason { get; }
    public string ChangedBy { get; }

    private static string Describe(long? oldValue, long newValue) =>
        oldValue is null || oldValue == newValue
            ? Money.Format(newValue)
            : $"{Money.Format(oldValue.Value)} → {Money.Format(newValue)}";
}
