
using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EVPharmacy.Data.Services;

public enum ReportKind
{
    DailySales,
    PaymentMethods,
    TopSellers,
    SeniorPwdBook,
    VoidedSales,
    InventoryValuation,
    ExpiryReport,
    LowStock
}

/// <summary>Which date inputs a report needs.</summary>
public enum ReportDateMode
{
    None,
    Range,
    UpTo
}

public sealed record ReportInfo(ReportKind Kind, string Title, string Description, ReportDateMode DateMode);

public sealed record ReportColumn(string Header, bool IsNumber = false);

/// <summary>A report as plain text cells, ready to show in a grid or export as CSV.</summary>
public sealed class ReportTable
{
    public required string Title { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    public required IReadOnlyList<ReportColumn> Columns { get; init; }

    public required IReadOnlyList<string[]> Rows { get; init; }

    /// <summary>Headline figures shown above the table.</summary>
    public IReadOnlyList<(string Label, string Value)> Totals { get; init; } = [];

    /// <summary>CSV that opens cleanly in Excel (UTF-8 with BOM). Numbers have no thousands separators.</summary>
    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.Append('﻿');
        sb.AppendLine(string.Join(",", Columns.Select(c => Quote(c.Header))));

        foreach (var row in Rows)
        {
            sb.AppendLine(string.Join(",", row.Select((cell, i) =>
                Quote(i < Columns.Count && Columns[i].IsNumber ? cell.Replace(",", string.Empty) : cell))));
        }

        return sb.ToString();
    }

    private static string Quote(string value)
    {
        // Guard against spreadsheet formula injection from user-entered text.
        if (value.Length > 0 && value[0] is '=' or '+' or '@')
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

/// <summary>
/// Sales and inventory reports. Money columns are shown in pesos without a currency sign
/// (the header says so), which keeps exported CSV files numeric.
/// </summary>
public class ReportService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly InventoryService _inventory;

    public ReportService(IDbContextFactory<AppDbContext> contextFactory, InventoryService inventory)
    {
        _contextFactory = contextFactory;
        _inventory = inventory;
    }

    public static IReadOnlyList<ReportInfo> Catalog { get; } =
    [
        new(ReportKind.DailySales, "Daily sales", "Sales per day with discounts and the VAT breakdown.", ReportDateMode.Range),
        new(ReportKind.PaymentMethods, "Sales by payment method", "Cash, card and e-wallet totals.", ReportDateMode.Range),
        new(ReportKind.TopSellers, "Top-selling medicines", "Units sold, sales and estimated margin per medicine.", ReportDateMode.Range),
        new(ReportKind.SeniorPwdBook, "Senior Citizen / PWD sales", "Every discounted sale with the customer name and ID number.", ReportDateMode.Range),
        new(ReportKind.VoidedSales, "Voided sales", "Cancelled sales with who voided them and why.", ReportDateMode.Range),
        new(ReportKind.InventoryValuation, "Inventory valuation", "Stock on hand valued at cost and at selling price.", ReportDateMode.None),
        new(ReportKind.ExpiryReport, "Expiry report", "Batches that are expired or expire on or before a date.", ReportDateMode.UpTo),
        new(ReportKind.LowStock, "Low stock", "Medicines at or below their reorder level.", ReportDateMode.None)
    ];

    public Task<ReportTable> BuildAsync(ReportKind kind, DateOnly from, DateOnly to) => kind switch
    {
        ReportKind.DailySales => DailySalesAsync(from, to),
        ReportKind.PaymentMethods => PaymentMethodsAsync(from, to),
        ReportKind.TopSellers => TopSellersAsync(from, to),
        ReportKind.SeniorPwdBook => SeniorPwdAsync(from, to),
        ReportKind.VoidedSales => VoidedAsync(from, to),
        ReportKind.InventoryValuation => ValuationAsync(),
        ReportKind.ExpiryReport => ExpiryAsync(to),
        ReportKind.LowStock => LowStockAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    // ------------------------------------------------------------------
    // Sales
    // ------------------------------------------------------------------

    private static (DateTime FromUtc, DateTime ToUtc) Bounds(DateOnly from, DateOnly to) =>
        (from.ToDateTime(TimeOnly.MinValue).ToUniversalTime(),
         to.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime());

    private static string Period(DateOnly from, DateOnly to) =>
        from == to ? from.ToString("MMM d, yyyy") : $"{from:MMM d, yyyy} to {to:MMM d, yyyy}";

    private async Task<ReportTable> DailySalesAsync(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = Bounds(from, to);
        await using var db = await _contextFactory.CreateDbContextAsync();

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.SoldAt >= fromUtc && s.SoldAt < toUtc)
            .Select(s => new
            {
                s.SoldAt, s.SubtotalCentavos, s.VatRemovedCentavos, s.DiscountCentavos, s.TotalCentavos,
                s.VatableSalesCentavos, s.VatExemptSalesCentavos, s.NonVatSalesCentavos, s.VatAmountCentavos
            })
            .ToListAsync();

        var voided = await db.Sales.CountAsync(s => s.Status == SaleStatus.Voided && s.SoldAt >= fromUtc && s.SoldAt < toUtc);

        var days = sales
            .GroupBy(s => DateOnly.FromDateTime(s.SoldAt.ToLocalTime()))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Day = g.Key,
                Count = g.Count(),
                Gross = g.Sum(s => s.SubtotalCentavos),
                VatRemoved = g.Sum(s => s.VatRemovedCentavos),
                Discount = g.Sum(s => s.DiscountCentavos),
                Net = g.Sum(s => s.TotalCentavos),
                Vatable = g.Sum(s => s.VatableSalesCentavos),
                Exempt = g.Sum(s => s.VatExemptSalesCentavos),
                NonVat = g.Sum(s => s.NonVatSalesCentavos),
                Vat = g.Sum(s => s.VatAmountCentavos)
            })
            .ToList();

        return new ReportTable
        {
            Title = "Daily sales",
            Subtitle = $"{Period(from, to)}. Voided sales are not included ({voided} voided in this period).",
            Columns =
            [
                new("Date"), new("Sales", true), new("Gross sales (₱)", true), new("Less VAT (₱)", true),
                new("Less discounts (₱)", true), new("Net sales (₱)", true), new("VATable sales (₱)", true),
                new("VAT-exempt sales (₱)", true), new("Non-VAT sales (₱)", true), new("VAT (₱)", true)
            ],
            Rows = days.Select(d => new[]
            {
                d.Day.ToString("ddd, MMM d, yyyy"), d.Count.ToString("N0"), P(d.Gross), P(d.VatRemoved), P(d.Discount),
                P(d.Net), P(d.Vatable), P(d.Exempt), P(d.NonVat), P(d.Vat)
            }).ToList(),
            Totals =
            [
                ("Sales", sales.Count.ToString("N0")),
                ("Net sales", Money.Format(sales.Sum(s => s.TotalCentavos))),
                ("Discounts given", Money.Format(sales.Sum(s => s.DiscountCentavos))),
                ("VAT collected", Money.Format(sales.Sum(s => s.VatAmountCentavos)))
            ]
        };
    }

    private async Task<ReportTable> PaymentMethodsAsync(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = Bounds(from, to);
        await using var db = await _contextFactory.CreateDbContextAsync();

        var rows = await db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.SoldAt >= fromUtc && s.SoldAt < toUtc)
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new { Method = g.Key, Count = g.Count(), Total = g.Sum(s => s.TotalCentavos) })
            .ToListAsync();

        var grand = rows.Sum(r => r.Total);

        return new ReportTable
        {
            Title = "Sales by payment method",
            Subtitle = $"{Period(from, to)}. Voided sales are not included.",
            Columns = [new("Payment method"), new("Sales", true), new("Amount (₱)", true), new("Share", true)],
            Rows = rows.OrderByDescending(r => r.Total).Select(r => new[]
            {
                r.Method switch { PaymentMethod.Cash => "Cash", PaymentMethod.Card => "Card", _ => "E-wallet" },
                r.Count.ToString("N0"), P(r.Total), grand == 0 ? "0%" : $"{(decimal)r.Total / grand:P0}"
            }).ToList(),
            Totals = [("Sales", rows.Sum(r => r.Count).ToString("N0")), ("Total", Money.Format(grand))]
        };
    }

    private async Task<ReportTable> TopSellersAsync(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = Bounds(from, to);
        await using var db = await _contextFactory.CreateDbContextAsync();

        var items = await db.SaleItems.AsNoTracking()
            .Where(i => i.Sale!.Status == SaleStatus.Completed && i.Sale.SoldAt >= fromUtc && i.Sale.SoldAt < toUtc)
            .Select(i => new { i.MedicineId, i.MedicineName, i.Quantity, i.NetCentavos, i.VatAmountCentavos, i.UnitCostCentavos })
            .ToListAsync();

        // Revenue excludes VAT, so margin is not overstated for VAT-inclusive prices.
        var rows = items
            .GroupBy(i => i.MedicineId)
            .Select(g =>
            {
                var units = g.Sum(i => i.Quantity);
                var revenue = g.Sum(i => i.NetCentavos - i.VatAmountCentavos);
                var cost = g.Sum(i => i.Quantity * i.UnitCostCentavos);
                return new { Name = g.First().MedicineName, Units = units, Revenue = revenue, Cost = cost, Margin = revenue - cost };
            })
            .OrderByDescending(r => r.Units).ThenByDescending(r => r.Revenue)
            .Take(100)
            .ToList();

        return new ReportTable
        {
            Title = "Top-selling medicines",
            Subtitle = $"{Period(from, to)}. Sales are shown without VAT; margin = sales minus the cost of the batches sold.",
            Columns = [new("Medicine"), new("Units sold", true), new("Sales ex-VAT (₱)", true), new("Cost (₱)", true), new("Margin (₱)", true), new("Margin %", true)],
            Rows = rows.Select(r => new[]
            {
                r.Name, r.Units.ToString("N0"), P(r.Revenue), P(r.Cost), P(r.Margin),
                r.Revenue == 0 ? "—" : $"{(decimal)r.Margin / r.Revenue:P1}"
            }).ToList(),
            Totals = [("Medicines sold", rows.Count.ToString("N0")), ("Units", rows.Sum(r => r.Units).ToString("N0")), ("Margin", Money.Format(rows.Sum(r => r.Margin)))]
        };
    }

    private async Task<ReportTable> SeniorPwdAsync(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = Bounds(from, to);
        await using var db = await _contextFactory.CreateDbContextAsync();

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.DiscountType != DiscountType.None
                        && s.SoldAt >= fromUtc && s.SoldAt < toUtc)
            .OrderBy(s => s.SoldAt)
            .Select(s => new
            {
                s.SoldAt, s.ReceiptNumber, s.DiscountType, s.CustomerName, s.CustomerIdNumber,
                s.SubtotalCentavos, s.VatRemovedCentavos, s.DiscountCentavos, s.TotalCentavos
            })
            .ToListAsync();

        return new ReportTable
        {
            Title = "Senior Citizen / PWD sales",
            Subtitle = $"{Period(from, to)}. Completed sales only.",
            Columns =
            [
                new("Date"), new("Receipt no."), new("Type"), new("Customer name"), new("ID number"),
                new("Gross (₱)", true), new("VAT removed (₱)", true), new("Discount (₱)", true), new("Amount paid (₱)", true)
            ],
            Rows = sales.Select(s => new[]
            {
                s.SoldAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt"), s.ReceiptNumber,
                s.DiscountType == DiscountType.Pwd ? "PWD" : "Senior Citizen",
                s.CustomerName ?? string.Empty, s.CustomerIdNumber ?? string.Empty,
                P(s.SubtotalCentavos), P(s.VatRemovedCentavos), P(s.DiscountCentavos), P(s.TotalCentavos)
            }).ToList(),
            Totals =
            [
                ("Sales", sales.Count.ToString("N0")),
                ("Total discounts", Money.Format(sales.Sum(s => s.DiscountCentavos))),
                ("VAT removed", Money.Format(sales.Sum(s => s.VatRemovedCentavos))),
                ("Amount paid", Money.Format(sales.Sum(s => s.TotalCentavos)))
            ]
        };
    }

    private async Task<ReportTable> VoidedAsync(DateOnly from, DateOnly to)
    {
        var (fromUtc, toUtc) = Bounds(from, to);
        await using var db = await _contextFactory.CreateDbContextAsync();

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Voided && s.SoldAt >= fromUtc && s.SoldAt < toUtc)
            .OrderBy(s => s.SoldAt)
            .Select(s => new
            {
                s.SoldAt, s.ReceiptNumber, s.TotalCentavos, s.VoidedAt, s.VoidReason,
                VoidedBy = s.VoidedByUser != null ? s.VoidedByUser.Username : null
            })
            .ToListAsync();

        return new ReportTable
        {
            Title = "Voided sales",
            Subtitle = Period(from, to),
            Columns = [new("Sold"), new("Receipt no."), new("Amount (₱)", true), new("Voided"), new("Voided by"), new("Reason")],
            Rows = sales.Select(s => new[]
            {
                s.SoldAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt"), s.ReceiptNumber, P(s.TotalCentavos),
                s.VoidedAt?.ToLocalTime().ToString("MMM d, yyyy h:mm tt") ?? string.Empty, s.VoidedBy ?? "—", s.VoidReason ?? string.Empty
            }).ToList(),
            Totals = [("Voided sales", sales.Count.ToString("N0")), ("Amount", Money.Format(sales.Sum(s => s.TotalCentavos)))]
        };
    }

    // ------------------------------------------------------------------
    // Inventory
    // ------------------------------------------------------------------

    private async Task<ReportTable> ValuationAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var today = InventoryService.Today;

        var batches = await db.InventoryBatches.AsNoTracking()
            .Where(b => b.QuantityOnHand > 0)
            .Select(b => new
            {
                b.MedicineId, Name = b.Medicine!.GenericName, Brand = b.Medicine.BrandName,
                Price = b.Medicine.SellingPriceCentavos, b.QuantityOnHand, b.UnitCostCentavos, b.ExpiryDate
            })
            .ToListAsync();

        var rows = batches
            .GroupBy(b => b.MedicineId)
            .Select(g => new
            {
                Name = g.First().Brand is null ? g.First().Name : $"{g.First().Name} ({g.First().Brand})",
                Qty = g.Sum(b => b.QuantityOnHand),
                Expired = g.Where(b => b.ExpiryDate < today).Sum(b => b.QuantityOnHand),
                Cost = g.Sum(b => (long)b.QuantityOnHand * b.UnitCostCentavos),
                Retail = g.Sum(b => (long)b.QuantityOnHand * b.Price)
            })
            .OrderBy(r => r.Name)
            .ToList();

        return new ReportTable
        {
            Title = "Inventory valuation",
            Subtitle = $"Stock on hand as of {DateTime.Now:MMM d, yyyy h:mm tt}, including expired and on-hold stock.",
            Columns = [new("Medicine"), new("On hand", true), new("Of which expired", true), new("Value at cost (₱)", true), new("Value at selling price (₱)", true)],
            Rows = rows.Select(r => new[] { r.Name, r.Qty.ToString("N0"), r.Expired.ToString("N0"), P(r.Cost), P(r.Retail) }).ToList(),
            Totals =
            [
                ("Medicines in stock", rows.Count.ToString("N0")),
                ("Units on hand", rows.Sum(r => r.Qty).ToString("N0")),
                ("Value at cost", Money.Format(rows.Sum(r => r.Cost))),
                ("Value at selling price", Money.Format(rows.Sum(r => r.Retail)))
            ]
        };
    }

    private async Task<ReportTable> ExpiryAsync(DateOnly upTo)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var today = InventoryService.Today;

        var batches = await db.InventoryBatches.AsNoTracking()
            .Where(b => b.QuantityOnHand > 0 && b.ExpiryDate <= upTo)
            .OrderBy(b => b.ExpiryDate)
            .Select(b => new
            {
                Name = b.Medicine!.GenericName, Brand = b.Medicine.BrandName, b.BatchNumber, b.ExpiryDate,
                b.QuantityOnHand, b.UnitCostCentavos, b.Status
            })
            .ToListAsync();

        return new ReportTable
        {
            Title = "Expiry report",
            Subtitle = $"Batches with stock that are expired or expire on or before {upTo:MMM d, yyyy}.",
            Columns = [new("Medicine"), new("Batch / lot"), new("Expiry date"), new("Days left", true), new("Quantity", true), new("Value at cost (₱)", true), new("Status")],
            Rows = batches.Select(b =>
            {
                var days = b.ExpiryDate.DayNumber - today.DayNumber;
                return new[]
                {
                    b.Brand is null ? b.Name : $"{b.Name} ({b.Brand})", b.BatchNumber, b.ExpiryDate.ToString("MMM d, yyyy"),
                    days.ToString("N0"), b.QuantityOnHand.ToString("N0"), P((long)b.QuantityOnHand * b.UnitCostCentavos),
                    b.Status == BatchStatus.Recalled ? "Recalled" : b.Status == BatchStatus.Quarantined ? "On hold" : days < 0 ? "Expired" : "Expiring"
                };
            }).ToList(),
            Totals =
            [
                ("Batches", batches.Count.ToString("N0")),
                ("Units", batches.Sum(b => b.QuantityOnHand).ToString("N0")),
                ("Value at cost", Money.Format(batches.Sum(b => (long)b.QuantityOnHand * b.UnitCostCentavos)))
            ]
        };
    }

    private async Task<ReportTable> LowStockAsync()
    {
        var all = await _inventory.GetStockSummaryAsync();
        var low = all.Where(s => s.IsLowStock).OrderBy(s => s.SellableQuantity - s.ReorderLevel).ThenBy(s => s.GenericName).ToList();

        return new ReportTable
        {
            Title = "Low stock",
            Subtitle = "Medicines with a reorder level whose sellable stock is at or below it.",
            Columns = [new("Medicine"), new("In stock", true), new("Reorder level", true), new("Short by", true), new("Expiring soon", true), new("On hold", true)],
            Rows = low.Select(s => new[]
            {
                s.BrandName is null ? s.GenericName : $"{s.GenericName} ({s.BrandName})",
                s.SellableQuantity.ToString("N0"), s.ReorderLevel.ToString("N0"),
                Math.Max(0, s.ReorderLevel - s.SellableQuantity).ToString("N0"),
                s.ExpiringSoonQuantity.ToString("N0"), s.HeldQuantity.ToString("N0")
            }).ToList(),
            Totals = [("Medicines to reorder", low.Count.ToString("N0")), ("Out of stock", low.Count(s => s.SellableQuantity <= 0).ToString("N0"))]
        };
    }

    private static string P(long centavos) => Money.ToPesos(centavos).ToString("N2");
}
