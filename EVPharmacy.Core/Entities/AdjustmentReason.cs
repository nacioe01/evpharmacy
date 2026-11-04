
namespace EVPharmacy.Core.Entities;

/// <summary>Why a batch quantity was changed outside of receiving and selling.</summary>
public enum AdjustmentReason
{
    Damaged,
    ExpiredWriteOff,
    LostOrStolen,
    ReturnedToSupplier,
    CountCorrection,
    Other
}

public static class AdjustmentReasons
{
    public static string Label(AdjustmentReason reason) => reason switch
    {
        AdjustmentReason.Damaged => "Damaged",
        AdjustmentReason.ExpiredWriteOff => "Expired (write-off)",
        AdjustmentReason.LostOrStolen => "Lost or stolen",
        AdjustmentReason.ReturnedToSupplier => "Returned to supplier",
        AdjustmentReason.CountCorrection => "Stock count correction",
        _ => "Other"
    };

    /// <summary>Only these reasons may add stock. The rest always remove it.</summary>
    public static bool AllowsIncrease(AdjustmentReason reason) =>
        reason is AdjustmentReason.CountCorrection or AdjustmentReason.Other;

    /// <summary>A written explanation is mandatory for these reasons.</summary>
    public static bool RequiresNote(AdjustmentReason reason) =>
        reason is AdjustmentReason.CountCorrection or AdjustmentReason.Other;
}
