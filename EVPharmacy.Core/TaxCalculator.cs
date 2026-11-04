
using EVPharmacy.Core.Entities;

namespace EVPharmacy.Core;

/// <summary>The tax rules in force, taken from store settings (never hardcoded).</summary>
public sealed record TaxSettings(
    bool IsVatRegistered,
    int VatRateBps,
    int SeniorDiscountBps,
    int PwdDiscountBps)
{
    /// <summary>Discount percentage for a customer type, in basis points (2000 = 20%).</summary>
    public int DiscountBps(DiscountType type) => type switch
    {
        DiscountType.SeniorCitizen => SeniorDiscountBps,
        DiscountType.Pwd => PwdDiscountBps,
        _ => 0
    };
}

/// <summary>One sale line before tax. Prices are VAT-inclusive, as is usual for retail.</summary>
public sealed record TaxLineInput(long GrossCentavos, bool IsVatExempt);

public sealed record TaxLineResult(
    long Gross,
    // VAT stripped out because the buyer is Senior Citizen / PWD.
    long VatRemoved,
    long Discount,
    // What the customer pays for this line.
    long Net,
    // VAT contained in what the customer pays (0 for exempt or discounted sales).
    long VatAmount,
    long VatableSales,
    long VatExemptSales,
    long NonVatSales);

public sealed record TaxTotals(
    long Gross,
    long VatRemoved,
    long Discount,
    long Net,
    long VatAmount,
    long VatableSales,
    long VatExemptSales,
    long NonVatSales,
    IReadOnlyList<TaxLineResult> Lines);

/// <summary>
/// Philippine VAT and Senior Citizen / PWD discount computation.
///
/// Regular sale, VAT-registered store:
///   a VATable item's VAT-inclusive price contains VAT = gross x rate / (1 + rate).
///   A VAT-exempt item has no VAT.
///
/// Senior Citizen / PWD sale of medicines (RA 9994, RA 10754) with a VAT-registered store:
///   the VAT is removed first (price / (1 + rate)), then the discount is taken on that
///   VAT-exclusive amount. An item that is already VAT-exempt only gets the discount.
///
/// Store not VAT-registered: no VAT is computed; the discount applies to the price.
///
/// All figures are integer centavos, rounded half away from zero per line.
/// The percentages and the VAT-registration status come from settings, and a medicine
/// is only VAT-exempt when its own flag says so. Nothing is assumed.
/// </summary>
public static class TaxCalculator
{
    private const int BpsScale = 10_000;

    public static TaxTotals Calculate(
        TaxSettings settings,
        DiscountType discountType,
        IEnumerable<TaxLineInput> lines)
    {
        var results = lines.Select(l => CalculateLine(settings, discountType, l)).ToList();

        return new TaxTotals(
            results.Sum(r => r.Gross),
            results.Sum(r => r.VatRemoved),
            results.Sum(r => r.Discount),
            results.Sum(r => r.Net),
            results.Sum(r => r.VatAmount),
            results.Sum(r => r.VatableSales),
            results.Sum(r => r.VatExemptSales),
            results.Sum(r => r.NonVatSales),
            results);
    }

    public static TaxLineResult CalculateLine(TaxSettings settings, DiscountType discountType, TaxLineInput line)
    {
        var gross = line.GrossCentavos;
        var discountBps = settings.DiscountBps(discountType);
        var hasDiscount = discountType != DiscountType.None && discountBps > 0;

        // Store that is not VAT-registered: no VAT at all.
        if (!settings.IsVatRegistered)
        {
            var discount = hasDiscount ? Round(gross, discountBps, BpsScale) : 0;
            return new TaxLineResult(gross, 0, discount, gross - discount, 0, 0, 0, gross);
        }

        // VAT-exempt item: no VAT to remove, discount (if any) on the price.
        if (line.IsVatExempt)
        {
            var discount = hasDiscount ? Round(gross, discountBps, BpsScale) : 0;
            return new TaxLineResult(gross, 0, discount, gross - discount, 0, 0, gross, 0);
        }

        // VATable item, regular customer.
        if (!hasDiscount)
        {
            var vat = Round(gross, settings.VatRateBps, BpsScale + settings.VatRateBps);
            return new TaxLineResult(gross, 0, 0, gross, vat, gross - vat, 0, 0);
        }

        // VATable item sold to a Senior Citizen / PWD: remove VAT, then discount the VAT-exclusive amount.
        var vatExclusive = Round(gross, BpsScale, BpsScale + settings.VatRateBps);
        var vatRemoved = gross - vatExclusive;
        var discountAmount = Round(vatExclusive, discountBps, BpsScale);

        return new TaxLineResult(
            gross, vatRemoved, discountAmount, vatExclusive - discountAmount,
            0, 0, vatExclusive, 0);
    }

    /// <summary>
    /// Splits <paramref name="amount"/> across parts in proportion to <paramref name="weights"/>,
    /// giving the rounding remainder to the last part so the parts always add up exactly.
    /// </summary>
    public static long[] Split(long amount, IReadOnlyList<int> weights)
    {
        var parts = new long[weights.Count];
        var totalWeight = weights.Sum(w => (long)w);
        if (parts.Length == 0 || totalWeight == 0)
        {
            return parts;
        }

        long assigned = 0;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            parts[i] = (long)Math.Round((decimal)amount * weights[i] / totalWeight, 0, MidpointRounding.AwayFromZero);
            assigned += parts[i];
        }

        parts[^1] = amount - assigned;
        return parts;
    }

    private static long Round(long value, int numerator, int denominator) =>
        (long)Math.Round((decimal)value * numerator / denominator, 0, MidpointRounding.AwayFromZero);
}
