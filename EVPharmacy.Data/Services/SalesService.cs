
using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Services;

/// <summary>A medicine as shown in the POS search, with what can actually be sold right now.</summary>
public sealed record PosProduct(
    int MedicineId,
    string GenericName,
    string? BrandName,
    string? Strength,
    string DosageFormName,
    string UnitName,
    long PriceCentavos,
    PrescriptionClass PrescriptionClass,
    int AvailableQuantity,
    DateOnly? NextExpiry,
    bool IsVatExempt = false);

public sealed record CartLineInput(int MedicineId, int Quantity);

public sealed record CheckoutInput(
    IReadOnlyList<CartLineInput> Lines,
    PaymentMethod PaymentMethod,
    long AmountTenderedCentavos,
    string? PaymentReference,
    bool RxVerified,
    DiscountType DiscountType = DiscountType.None,
    string? CustomerName = null,
    string? CustomerIdNumber = null);

public sealed record SaleLineDetail(
    string MedicineName,
    int Quantity,
    long UnitPriceCentavos,
    long LineTotalCentavos,
    PrescriptionClass PrescriptionClass,
    bool IsVatExempt,
    long NetCentavos);

/// <summary>A sale as shown on a receipt. Lines are grouped by medicine.</summary>
public sealed record SaleDetail(
    int Id,
    string ReceiptNumber,
    DateTime SoldAtUtc,
    string? CashierName,
    long SubtotalCentavos,
    long TotalCentavos,
    PaymentMethod PaymentMethod,
    long AmountTenderedCentavos,
    long ChangeCentavos,
    string? PaymentReference,
    bool RxVerified,
    SaleStatus Status,
    string? VoidReason,
    IReadOnlyList<SaleLineDetail> Lines,
    bool IsVatRegistered,
    int VatRateBps,
    DiscountType DiscountType,
    int DiscountBps,
    long DiscountCentavos,
    long VatRemovedCentavos,
    long VatableSalesCentavos,
    long VatExemptSalesCentavos,
    long NonVatSalesCentavos,
    long VatAmountCentavos,
    string? CustomerName,
    string? CustomerIdNumber,
    DateTime? VoidedAtUtc,
    string? VoidedByName);

public sealed record SaleListItem(
    int Id,
    string ReceiptNumber,
    DateTime SoldAtUtc,
    string? CashierName,
    int ItemCount,
    long TotalCentavos,
    PaymentMethod PaymentMethod,
    DiscountType DiscountType,
    SaleStatus Status,
    string? CustomerName);

public sealed record TodaySummary(int SaleCount, long TotalCentavos);

/// <summary>
/// Selling: product search, checkout, voiding and receipt data.
/// A checkout deducts stock first-expiry-first-out, snapshots prices, and records
/// stock movements in a single database transaction. It is all saved or none of it is.
/// </summary>
public class SalesService
{
    public const int MaxQuantityPerLine = 10_000;
    public const long MaxSaleCentavos = 1_000_000_000; // 10,000,000 pesos

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SalesService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ------------------------------------------------------------------
    // Product search
    // ------------------------------------------------------------------

    /// <summary>Active medicines matching the text (name, brand or exact barcode); in-stock first.</summary>
    public async Task<List<PosProduct>> SearchProductsAsync(string? search, int take = 40)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.Medicines.AsNoTracking().Where(m => m.IsActive);

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(m =>
                EF.Functions.Like(m.GenericName, pattern, "\\") ||
                (m.BrandName != null && EF.Functions.Like(m.BrandName, pattern, "\\")) ||
                (m.Barcode != null && m.Barcode == term));
        }

        var medicines = await query
            .OrderBy(m => m.GenericName).ThenBy(m => m.BrandName)
            .Take(300)
            .Select(m => new
            {
                m.Id, m.GenericName, m.BrandName, m.Strength,
                Form = m.DosageForm!.Name, Unit = m.UnitOfMeasure!.Name,
                m.SellingPriceCentavos, m.PrescriptionClass, m.IsVatExempt
            })
            .ToListAsync();

        var ids = medicines.Select(m => m.Id).ToList();
        var today = InventoryService.Today;

        var batches = await db.InventoryBatches.AsNoTracking()
            .Where(b => ids.Contains(b.MedicineId)
                        && b.Status == BatchStatus.Available
                        && b.QuantityOnHand > 0
                        && b.ExpiryDate >= today)
            .Select(b => new { b.MedicineId, b.QuantityOnHand, b.ExpiryDate })
            .ToListAsync();

        var byMedicine = batches.ToLookup(b => b.MedicineId);

        return medicines
            .Select(m =>
            {
                var own = byMedicine[m.Id].ToList();
                return new PosProduct(
                    m.Id, m.GenericName, m.BrandName, m.Strength, m.Form, m.Unit,
                    m.SellingPriceCentavos, m.PrescriptionClass,
                    own.Sum(b => b.QuantityOnHand),
                    own.Count == 0 ? null : own.Min(b => b.ExpiryDate),
                    m.IsVatExempt);
            })
            .OrderByDescending(p => p.AvailableQuantity > 0)
            .ThenBy(p => p.GenericName)
            .Take(take)
            .ToList();
    }

    // ------------------------------------------------------------------
    // Checkout
    // ------------------------------------------------------------------

    public async Task<OperationResult> CompleteSaleAsync(CheckoutInput input, int? cashierUserId)
    {
        // Same medicine on two cart lines is one demand.
        var demand = input.Lines
            .GroupBy(l => l.MedicineId)
            .Select(g => new CartLineInput(g.Key, g.Sum(l => l.Quantity)))
            .ToList();

        if (demand.Count == 0)
            return OperationResult.Fail("The cart is empty.");
        if (demand.Any(d => d.Quantity <= 0 || d.Quantity > MaxQuantityPerLine))
            return OperationResult.Fail($"Quantities must be whole numbers from 1 to {MaxQuantityPerLine:N0}.");
        if (!Enum.IsDefined(input.PaymentMethod))
            return OperationResult.Fail("Choose a payment method.");

        var reference = Clean(input.PaymentReference);
        if (reference?.Length > Sale.MaxReferenceLength)
            return OperationResult.Fail($"Reference must be {Sale.MaxReferenceLength} characters or fewer.");

        var customerName = Clean(input.CustomerName);
        var customerId = Clean(input.CustomerIdNumber);
        if (!Enum.IsDefined(input.DiscountType))
            return OperationResult.Fail("Choose a valid discount type.");
        if (input.DiscountType != DiscountType.None)
        {
            var who = input.DiscountType == DiscountType.SeniorCitizen ? "Senior Citizen" : "PWD";
            if (customerName is null)
                return OperationResult.Fail($"Enter the {who}'s full name as shown on the ID.");
            if (customerId is null)
                return OperationResult.Fail($"Enter the {who} ID number.");
        }
        if (customerName?.Length > Sale.MaxCustomerNameLength)
            return OperationResult.Fail($"Customer name must be {Sale.MaxCustomerNameLength} characters or fewer.");
        if (customerId?.Length > Sale.MaxCustomerIdLength)
            return OperationResult.Fail($"ID number must be {Sale.MaxCustomerIdLength} characters or fewer.");

        await using var db = await _contextFactory.CreateDbContextAsync();

        var storeSettings = await db.StoreSettingsRows.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == StoreSettings.SingletonId) ?? new StoreSettings();
        var taxSettings = storeSettings.ToTaxSettings();

        var ids = demand.Select(d => d.MedicineId).ToList();
        var medicines = await db.Medicines.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        foreach (var d in demand)
        {
            if (!medicines.TryGetValue(d.MedicineId, out var med))
                return OperationResult.Fail("A medicine in the cart no longer exists.");
            if (!med.IsActive)
                return OperationResult.Fail($"{med.GenericName} is inactive and cannot be sold.");
        }

        var rxItem = demand.Select(d => medicines[d.MedicineId])
            .FirstOrDefault(m => m.PrescriptionClass != PrescriptionClass.OverTheCounter);
        if (rxItem is not null && !input.RxVerified)
            return OperationResult.Fail($"{rxItem.GenericName} needs a prescription. Confirm the prescription was checked.");

        // Tracked batches: the same objects are deducted and saved in one transaction.
        var today = InventoryService.Today;
        var batches = await db.InventoryBatches
            .Where(b => ids.Contains(b.MedicineId)
                        && b.Status == BatchStatus.Available
                        && b.QuantityOnHand > 0
                        && b.ExpiryDate >= today)
            .ToListAsync();

        var sale = new Sale
        {
            ReceiptNumber = string.Empty, // assigned below
            CashierUserId = cashierUserId,
            RxVerified = rxItem is not null && input.RxVerified,
            PaymentMethod = input.PaymentMethod,
            PaymentReference = reference,
            IsVatRegistered = taxSettings.IsVatRegistered,
            VatRateBps = taxSettings.VatRateBps,
            DiscountType = input.DiscountType,
            DiscountBps = taxSettings.DiscountBps(input.DiscountType),
            CustomerName = input.DiscountType == DiscountType.None ? null : customerName,
            CustomerIdNumber = input.DiscountType == DiscountType.None ? null : customerId
        };

        var movements = new List<StockMovement>();
        var lineResults = new List<TaxLineResult>();

        foreach (var d in demand)
        {
            var med = medicines[d.MedicineId];

            var plan = FefoAllocator.Allocate(
                batches.Where(b => b.MedicineId == d.MedicineId)
                       .Select(b => new FefoBatch(b.Id, b.BatchNumber, b.ExpiryDate, b.QuantityOnHand)),
                d.Quantity);

            if (!plan.IsFullyCovered)
            {
                var available = d.Quantity - plan.Shortfall;
                return OperationResult.Fail(
                    available == 0
                        ? $"{med.GenericName} is out of stock."
                        : $"Only {available:N0} of {med.GenericName} can be sold right now (you asked for {d.Quantity:N0}).");
            }

            var name = DisplayName(med);

            // Tax is worked out per medicine line, then split across the batch rows so every row adds up exactly.
            var lineGross = d.Quantity * med.SellingPriceCentavos;
            var tax = TaxCalculator.CalculateLine(taxSettings, input.DiscountType, new TaxLineInput(lineGross, med.IsVatExempt));
            lineResults.Add(tax);

            var weights = plan.Allocations.Select(a => a.Quantity).ToList();
            var vatParts = TaxCalculator.Split(tax.VatAmount, weights);
            var removedParts = TaxCalculator.Split(tax.VatRemoved, weights);
            var discountParts = TaxCalculator.Split(tax.Discount, weights);

            for (var i = 0; i < plan.Allocations.Count; i++)
            {
                var alloc = plan.Allocations[i];
                var batch = batches.Single(b => b.Id == alloc.BatchId);
                batch.QuantityOnHand -= alloc.Quantity;
                batch.UpdatedAt = DateTime.UtcNow;

                var lineTotal = alloc.Quantity * med.SellingPriceCentavos;

                sale.Items.Add(new SaleItem
                {
                    MedicineId = med.Id,
                    Batch = batch,
                    MedicineName = name,
                    Quantity = alloc.Quantity,
                    UnitPriceCentavos = med.SellingPriceCentavos,
                    UnitCostCentavos = batch.UnitCostCentavos,
                    LineTotalCentavos = lineTotal,
                    IsVatExempt = med.IsVatExempt,
                    VatAmountCentavos = vatParts[i],
                    VatRemovedCentavos = removedParts[i],
                    DiscountCentavos = discountParts[i],
                    NetCentavos = lineTotal - removedParts[i] - discountParts[i],
                    PrescriptionClass = med.PrescriptionClass
                });

                movements.Add(new StockMovement
                {
                    MedicineId = med.Id,
                    Batch = batch,
                    MovementType = MovementType.Sale,
                    QuantityChange = -alloc.Quantity,
                    BalanceAfter = batch.QuantityOnHand,
                    Sale = sale,
                    UserId = cashierUserId
                });
            }
        }

        sale.SubtotalCentavos = lineResults.Sum(r => r.Gross);
        sale.DiscountCentavos = lineResults.Sum(r => r.Discount);
        sale.VatRemovedCentavos = lineResults.Sum(r => r.VatRemoved);
        sale.VatAmountCentavos = lineResults.Sum(r => r.VatAmount);
        sale.VatableSalesCentavos = lineResults.Sum(r => r.VatableSales);
        sale.VatExemptSalesCentavos = lineResults.Sum(r => r.VatExemptSales);
        sale.NonVatSalesCentavos = lineResults.Sum(r => r.NonVatSales);
        sale.TotalCentavos = lineResults.Sum(r => r.Net);

        if (sale.TotalCentavos <= 0)
            return OperationResult.Fail("The total is zero. Check the selling prices of the items.");
        if (sale.TotalCentavos > MaxSaleCentavos)
            return OperationResult.Fail("The total is too large for a single sale.");

        if (input.PaymentMethod == PaymentMethod.Cash)
        {
            if (input.AmountTenderedCentavos < sale.TotalCentavos)
                return OperationResult.Fail(
                    $"The cash received ({Money.Format(input.AmountTenderedCentavos)}) is less than the total ({Money.Format(sale.TotalCentavos)}).");

            sale.AmountTenderedCentavos = input.AmountTenderedCentavos;
            sale.ChangeCentavos = input.AmountTenderedCentavos - sale.TotalCentavos;
        }
        else
        {
            sale.AmountTenderedCentavos = sale.TotalCentavos;
            sale.ChangeCentavos = 0;
        }

        // Receipt number: local date + running count for the day.
        var prefix = DateTime.Now.ToString("yyyyMMdd");
        var countToday = await db.Sales.CountAsync(s => s.ReceiptNumber.StartsWith(prefix));
        sale.ReceiptNumber = $"{prefix}-{countToday + 1:D4}";

        db.Sales.Add(sale);
        db.StockMovements.AddRange(movements);

        try
        {
            // One SaveChanges is one transaction: sale, items, stock deduction and movements together.
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return OperationResult.Fail("The receipt number was already taken. Nothing was recorded; please try again.");
        }

        return OperationResult.Ok(sale.Id);
    }

    // ------------------------------------------------------------------
    // Void
    // ------------------------------------------------------------------

    /// <summary>
    /// Cancels a completed sale. The sale record is kept (marked voided) and the
    /// stock goes back to the exact batches it was taken from.
    /// </summary>
    public async Task<OperationResult> VoidSaleAsync(int saleId, string? reason, int? userId)
    {
        var cleanReason = Clean(reason);
        if (cleanReason is null)
            return OperationResult.Fail("Please give a reason for voiding this sale.");
        if (cleanReason.Length > Sale.MaxVoidReasonLength)
            return OperationResult.Fail($"Reason must be {Sale.MaxVoidReasonLength} characters or fewer.");

        await using var db = await _contextFactory.CreateDbContextAsync();

        var sale = await db.Sales.Include(s => s.Items).SingleOrDefaultAsync(s => s.Id == saleId);
        if (sale is null)
            return OperationResult.Fail("This sale no longer exists.");
        if (sale.Status == SaleStatus.Voided)
            return OperationResult.Fail("This sale was already voided.");

        var batchIds = sale.Items.Select(i => i.BatchId).Distinct().ToList();
        var batches = await db.InventoryBatches.Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id);

        foreach (var item in sale.Items)
        {
            var batch = batches[item.BatchId];
            batch.QuantityOnHand += item.Quantity;
            batch.UpdatedAt = DateTime.UtcNow;

            db.StockMovements.Add(new StockMovement
            {
                MedicineId = item.MedicineId,
                BatchId = batch.Id,
                MovementType = MovementType.Return,
                QuantityChange = item.Quantity,
                BalanceAfter = batch.QuantityOnHand,
                SaleId = sale.Id,
                Notes = $"Void of sale {sale.ReceiptNumber}: {cleanReason}",
                UserId = userId
            });
        }

        sale.Status = SaleStatus.Voided;
        sale.VoidReason = cleanReason;
        sale.VoidedAt = DateTime.UtcNow;
        sale.VoidedByUserId = userId;

        await db.SaveChangesAsync();

        return OperationResult.Ok(sale.Id);
    }

    // ------------------------------------------------------------------
    // Reading
    // ------------------------------------------------------------------

    public async Task<SaleDetail?> GetSaleAsync(int saleId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var sale = await db.Sales.AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.CashierUser)
            .Include(s => s.VoidedByUser)
            .SingleOrDefaultAsync(s => s.Id == saleId);

        if (sale is null)
        {
            return null;
        }

        // One receipt line per medicine, even when FEFO took it from several batches.
        var lines = sale.Items
            .GroupBy(i => new { i.MedicineId, i.UnitPriceCentavos })
            .Select(g => new SaleLineDetail(
                g.First().MedicineName,
                g.Sum(i => i.Quantity),
                g.Key.UnitPriceCentavos,
                g.Sum(i => i.LineTotalCentavos),
                g.First().PrescriptionClass,
                g.First().IsVatExempt,
                g.Sum(i => i.NetCentavos)))
            .ToList();

        return new SaleDetail(
            sale.Id, sale.ReceiptNumber, sale.SoldAt, sale.CashierUser?.Username,
            sale.SubtotalCentavos, sale.TotalCentavos, sale.PaymentMethod,
            sale.AmountTenderedCentavos, sale.ChangeCentavos, sale.PaymentReference,
            sale.RxVerified, sale.Status, sale.VoidReason, lines,
            sale.IsVatRegistered, sale.VatRateBps, sale.DiscountType, sale.DiscountBps,
            sale.DiscountCentavos, sale.VatRemovedCentavos, sale.VatableSalesCentavos,
            sale.VatExemptSalesCentavos, sale.NonVatSalesCentavos, sale.VatAmountCentavos,
            sale.CustomerName, sale.CustomerIdNumber, sale.VoidedAt, sale.VoidedByUser?.Username);
    }

    /// <summary>
    /// Sales between two local dates (from inclusive, to exclusive), newest first.
    /// Search matches the receipt number or the Senior Citizen / PWD customer name.
    /// </summary>
    public async Task<List<SaleListItem>> GetHistoryAsync(
        DateTime? fromLocal,
        DateTime? toLocalExclusive,
        string? search,
        bool includeVoided = true,
        int take = 500)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.Sales.AsNoTracking().AsQueryable();

        if (fromLocal is DateTime from)
        {
            var fromUtc = from.ToUniversalTime();
            query = query.Where(s => s.SoldAt >= fromUtc);
        }

        if (toLocalExclusive is DateTime to)
        {
            var toUtc = to.ToUniversalTime();
            query = query.Where(s => s.SoldAt < toUtc);
        }

        if (!includeVoided)
        {
            query = query.Where(s => s.Status == SaleStatus.Completed);
        }

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(s =>
                EF.Functions.Like(s.ReceiptNumber, pattern, "\\") ||
                (s.CustomerName != null && EF.Functions.Like(s.CustomerName, pattern, "\\")));
        }

        return await query
            .OrderByDescending(s => s.SoldAt).ThenByDescending(s => s.Id)
            .Take(take)
            .Select(s => new SaleListItem(
                s.Id, s.ReceiptNumber, s.SoldAt,
                s.CashierUser != null ? s.CashierUser.Username : null,
                s.Items.Sum(i => i.Quantity),
                s.TotalCentavos, s.PaymentMethod, s.DiscountType, s.Status, s.CustomerName))
            .ToListAsync();
    }

    /// <summary>Completed (not voided) sales made today, by the local calendar day.</summary>
    public async Task<TodaySummary> GetTodaySummaryAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var startUtc = DateTime.Today.ToUniversalTime();
        var endUtc = DateTime.Today.AddDays(1).ToUniversalTime();

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.SoldAt >= startUtc && s.SoldAt < endUtc)
            .Select(s => s.TotalCentavos)
            .ToListAsync();

        return new TodaySummary(sales.Count, sales.Sum());
    }

    private static string DisplayName(Medicine med)
    {
        var name = string.IsNullOrWhiteSpace(med.BrandName) ? med.GenericName : $"{med.GenericName} ({med.BrandName})";
        return string.IsNullOrWhiteSpace(med.Strength) ? name : $"{name} {med.Strength}";
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
