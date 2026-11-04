
using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Services;

public sealed record ReceiveLineInput(
    int MedicineId,
    string? BatchNumber,
    DateOnly ExpiryDate,
    int Quantity,
    long UnitCostCentavos);

public sealed record ReceiveStockInput(
    ReceiptType ReceiptType,
    int? SupplierId,
    string? InvoiceNumber,
    DateOnly ReceivedDate,
    string? Notes,
    bool UpdateMedicineCosts,
    IReadOnlyList<ReceiveLineInput> Lines);

public sealed record AdjustStockInput(
    int BatchId,
    int QuantityChange,
    AdjustmentReason Reason,
    string? Note);

/// <summary>Stock totals for one medicine, summed across its batches.</summary>
public sealed record StockSummaryItem(
    int MedicineId,
    string GenericName,
    string? BrandName,
    string? Strength,
    string UnitName,
    int ReorderLevel,
    bool IsActive,
    int SellableQuantity,
    int ExpiringSoonQuantity,
    int ExpiredQuantity,
    int HeldQuantity,
    DateOnly? NextExpiry)
{
    /// <summary>Only medicines with a reorder level set can be "low"; unstocked catalog items are not alerts.</summary>
    public bool IsLowStock => ReorderLevel > 0 && SellableQuantity <= ReorderLevel;
}

public sealed record BatchItem(
    int Id,
    int MedicineId,
    string GenericName,
    string? BrandName,
    string BatchNumber,
    DateOnly ExpiryDate,
    int QuantityReceived,
    int QuantityOnHand,
    long UnitCostCentavos,
    BatchStatus Status,
    string? StatusNote,
    string? SupplierName,
    DateOnly? ReceivedDate);

public sealed record MovementItem(
    DateTime CreatedAtUtc,
    string GenericName,
    string? BrandName,
    string BatchNumber,
    MovementType MovementType,
    int QuantityChange,
    int BalanceAfter,
    string? Notes,
    string? UserName,
    AdjustmentReason? AdjustmentReason);

/// <summary>
/// Stock receiving, batch tracking and stock movement history.
/// Every quantity change happens inside one database transaction and
/// is recorded as an append-only stock movement.
/// </summary>
public class InventoryService
{
    /// <summary>A batch expiring within this many days is flagged "expiring soon".</summary>
    public const int ExpiringSoonDays = 90;

    public const int MaxQuantityPerLine = 1_000_000;

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public InventoryService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    // ------------------------------------------------------------------
    // Receiving
    // ------------------------------------------------------------------

    public async Task<OperationResult> ReceiveAsync(ReceiveStockInput input, int? userId)
    {
        var error = ValidateReceive(input);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        if (input.SupplierId is int supplierId &&
            !await db.Suppliers.AnyAsync(s => s.Id == supplierId && s.IsActive))
        {
            return OperationResult.Fail("The selected supplier is no longer available.");
        }

        var medicineIds = input.Lines.Select(l => l.MedicineId).Distinct().ToList();
        var medicines = await db.Medicines
            .Where(m => medicineIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        foreach (var line in input.Lines)
        {
            if (!medicines.TryGetValue(line.MedicineId, out var med))
            {
                return OperationResult.Fail("A selected medicine no longer exists.");
            }

            if (!med.IsActive)
            {
                return OperationResult.Fail($"{med.GenericName} is inactive. Reactivate it before receiving stock.");
            }
        }

        // Existing batches for these medicines, so a repeated batch number adds to the same lot.
        var existing = await db.InventoryBatches
            .Where(b => medicineIds.Contains(b.MedicineId))
            .ToListAsync();

        var receipt = new StockReceipt
        {
            ReceiptType = input.ReceiptType,
            SupplierId = input.ReceiptType == ReceiptType.Delivery ? input.SupplierId : null,
            InvoiceNumber = Clean(input.InvoiceNumber),
            ReceivedDate = input.ReceivedDate,
            Notes = Clean(input.Notes),
            ReceivedByUserId = userId
        };
        db.StockReceipts.Add(receipt);

        var movementType = input.ReceiptType == ReceiptType.OpeningBalance
            ? MovementType.OpeningBalance
            : MovementType.Receipt;

        foreach (var line in input.Lines)
        {
            var batchNumber = line.BatchNumber!.Trim();
            var med = medicines[line.MedicineId];

            var batch = existing.FirstOrDefault(b =>
                b.MedicineId == line.MedicineId &&
                string.Equals(b.BatchNumber, batchNumber, StringComparison.OrdinalIgnoreCase));

            if (batch is null)
            {
                batch = new InventoryBatch
                {
                    MedicineId = line.MedicineId,
                    BatchNumber = batchNumber,
                    ExpiryDate = line.ExpiryDate,
                    UnitCostCentavos = line.UnitCostCentavos,
                    StockReceipt = receipt
                };
                db.InventoryBatches.Add(batch);
                existing.Add(batch);
            }
            else if (batch.ExpiryDate != line.ExpiryDate)
            {
                return OperationResult.Fail(
                    $"Batch {batchNumber} of {med.GenericName} already exists with expiry " +
                    $"{batch.ExpiryDate:MMM d, yyyy}. A batch number cannot have two expiry dates.");
            }
            else if (batch.Status != BatchStatus.Available)
            {
                return OperationResult.Fail(
                    $"Batch {batchNumber} of {med.GenericName} is {batch.Status.ToString().ToLowerInvariant()}. " +
                    "Stock cannot be added to it.");
            }

            batch.QuantityReceived += line.Quantity;
            batch.QuantityOnHand += line.Quantity;
            if (batch.Id != 0)
            {
                batch.UpdatedAt = DateTime.UtcNow;
            }

            db.StockMovements.Add(new StockMovement
            {
                MedicineId = line.MedicineId,
                Batch = batch,
                MovementType = movementType,
                QuantityChange = line.Quantity,
                BalanceAfter = batch.QuantityOnHand,
                StockReceipt = receipt,
                Notes = movementType == MovementType.OpeningBalance ? "Opening balance" : null,
                UserId = userId
            });

            if (input.UpdateMedicineCosts && med.PurchaseCostCentavos != line.UnitCostCentavos)
            {
                db.MedicinePriceHistories.Add(new MedicinePriceHistory
                {
                    Medicine = med,
                    OldPurchaseCostCentavos = med.PurchaseCostCentavos,
                    NewPurchaseCostCentavos = line.UnitCostCentavos,
                    OldSellingPriceCentavos = med.SellingPriceCentavos,
                    NewSellingPriceCentavos = med.SellingPriceCentavos,
                    Reason = "Updated from stock receiving",
                    ChangedByUserId = userId
                });

                med.PurchaseCostCentavos = line.UnitCostCentavos;
                med.UpdatedAt = DateTime.UtcNow;
            }
        }

        try
        {
            // One SaveChanges is one transaction: receipt, batches, movements and cost updates together.
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return OperationResult.Fail("A batch with the same number already exists. Please check the batch numbers.");
        }

        return OperationResult.Ok(receipt.Id);
    }

    public static string? ValidateReceive(ReceiveStockInput input)
    {
        if (input.Lines.Count == 0)
            return "Add at least one medicine to receive.";
        if (input.ReceiptType == ReceiptType.Delivery && input.SupplierId is null)
            return "Choose the supplier for this delivery.";
        if (Clean(input.InvoiceNumber)?.Length > StockReceipt.MaxInvoiceLength)
            return $"Invoice number must be {StockReceipt.MaxInvoiceLength} characters or fewer.";
        if (Clean(input.Notes)?.Length > StockReceipt.MaxNotesLength)
            return $"Notes must be {StockReceipt.MaxNotesLength} characters or fewer.";
        if (input.ReceivedDate > Today)
            return "The received date cannot be in the future.";

        foreach (var line in input.Lines)
        {
            var batch = Clean(line.BatchNumber);
            if (batch is null)
                return "Every line needs a batch or lot number.";
            if (batch.Length > InventoryBatch.MaxBatchNumberLength)
                return $"Batch number must be {InventoryBatch.MaxBatchNumberLength} characters or fewer.";
            if (line.Quantity <= 0 || line.Quantity > MaxQuantityPerLine)
                return "Quantity must be a whole number greater than zero.";
            if (line.UnitCostCentavos < 0)
                return "Unit cost cannot be negative.";
            if (line.ExpiryDate <= Today)
                return $"Batch {batch} is already expired or expires today and cannot be received.";
        }

        // The same batch twice in one delivery with different expiry dates is a typing mistake.
        var conflict = input.Lines
            .GroupBy(l => (l.MedicineId, Batch: Clean(l.BatchNumber)!.ToUpperInvariant()))
            .FirstOrDefault(g => g.Select(l => l.ExpiryDate).Distinct().Count() > 1);
        if (conflict is not null)
            return $"Batch {conflict.Key.Batch} appears more than once with different expiry dates.";

        return null;
    }

    // ------------------------------------------------------------------
    // Queries
    // ------------------------------------------------------------------

    public async Task<List<StockSummaryItem>> GetStockSummaryAsync(
        string? search = null,
        bool includeInactive = false)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var medQuery = db.Medicines.AsNoTracking().Where(m => includeInactive || m.IsActive);

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{EscapeLike(term)}%";
            medQuery = medQuery.Where(m =>
                EF.Functions.Like(m.GenericName, pattern, "\\") ||
                (m.BrandName != null && EF.Functions.Like(m.BrandName, pattern, "\\")) ||
                (m.Barcode != null && m.Barcode == term));
        }

        var medicines = await medQuery
            .OrderBy(m => m.GenericName).ThenBy(m => m.BrandName)
            .Select(m => new
            {
                m.Id, m.GenericName, m.BrandName, m.Strength,
                Unit = m.UnitOfMeasure!.Name, m.ReorderLevel, m.IsActive
            })
            .ToListAsync();

        var ids = medicines.Select(m => m.Id).ToList();
        var batches = await db.InventoryBatches.AsNoTracking()
            .Where(b => ids.Contains(b.MedicineId) && b.QuantityOnHand > 0)
            .Select(b => new { b.MedicineId, b.QuantityOnHand, b.ExpiryDate, b.Status })
            .ToListAsync();

        var byMedicine = batches.ToLookup(b => b.MedicineId);
        var today = Today;
        var soonLimit = today.AddDays(ExpiringSoonDays);

        return medicines.Select(m =>
        {
            var own = byMedicine[m.Id].ToList();

            var sellable = own.Where(b => b.Status == BatchStatus.Available && b.ExpiryDate >= today).ToList();
            var expired = own.Where(b => b.ExpiryDate < today).Sum(b => b.QuantityOnHand);
            var held = own.Where(b => b.Status != BatchStatus.Available && b.ExpiryDate >= today)
                .Sum(b => b.QuantityOnHand);

            return new StockSummaryItem(
                m.Id, m.GenericName, m.BrandName, m.Strength, m.Unit, m.ReorderLevel, m.IsActive,
                sellable.Sum(b => b.QuantityOnHand),
                sellable.Where(b => b.ExpiryDate <= soonLimit).Sum(b => b.QuantityOnHand),
                expired,
                held,
                sellable.Count == 0 ? null : sellable.Min(b => b.ExpiryDate));
        }).ToList();
    }

    public async Task<List<BatchItem>> GetBatchesAsync(string? search = null, bool includeEmpty = false)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.InventoryBatches.AsNoTracking()
            .Where(b => includeEmpty || b.QuantityOnHand > 0);

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{EscapeLike(term)}%";
            query = query.Where(b =>
                EF.Functions.Like(b.Medicine!.GenericName, pattern, "\\") ||
                (b.Medicine.BrandName != null && EF.Functions.Like(b.Medicine.BrandName, pattern, "\\")) ||
                EF.Functions.Like(b.BatchNumber, pattern, "\\"));
        }

        return await query
            .OrderBy(b => b.ExpiryDate).ThenBy(b => b.Medicine!.GenericName)
            .Select(b => new BatchItem(
                b.Id, b.MedicineId, b.Medicine!.GenericName, b.Medicine.BrandName,
                b.BatchNumber, b.ExpiryDate, b.QuantityReceived, b.QuantityOnHand,
                b.UnitCostCentavos, b.Status, b.StatusNote,
                b.StockReceipt != null && b.StockReceipt.Supplier != null ? b.StockReceipt.Supplier.Name : null,
                b.StockReceipt != null ? b.StockReceipt.ReceivedDate : (DateOnly?)null))
            .ToListAsync();
    }

    public async Task<List<MovementItem>> GetMovementsAsync(string? search = null, int take = 300)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var query = db.StockMovements.AsNoTracking().AsQueryable();

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{EscapeLike(term)}%";
            query = query.Where(m =>
                EF.Functions.Like(m.Medicine!.GenericName, pattern, "\\") ||
                (m.Medicine.BrandName != null && EF.Functions.Like(m.Medicine.BrandName, pattern, "\\")) ||
                EF.Functions.Like(m.Batch!.BatchNumber, pattern, "\\"));
        }

        return await query
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(take)
            .Select(m => new MovementItem(
                m.CreatedAt, m.Medicine!.GenericName, m.Medicine.BrandName, m.Batch!.BatchNumber,
                m.MovementType, m.QuantityChange, m.BalanceAfter, m.Notes,
                m.User != null ? m.User.Username : null,
                m.AdjustmentReason))
            .ToListAsync();
    }

    // ------------------------------------------------------------------
    // Batch status (quarantine / recall)
    // ------------------------------------------------------------------

    public async Task<OperationResult> SetBatchStatusAsync(int batchId, BatchStatus status, string? note)
    {
        var cleanNote = Clean(note);

        if (status != BatchStatus.Available && cleanNote is null)
        {
            return OperationResult.Fail("Please give a reason for quarantining or recalling a batch.");
        }

        if (cleanNote?.Length > InventoryBatch.MaxStatusNoteLength)
        {
            return OperationResult.Fail($"Reason must be {InventoryBatch.MaxStatusNoteLength} characters or fewer.");
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var batch = await db.InventoryBatches.SingleOrDefaultAsync(b => b.Id == batchId);
        if (batch is null)
        {
            return OperationResult.Fail("This batch no longer exists.");
        }

        batch.Status = status;
        batch.StatusNote = status == BatchStatus.Available ? null : cleanNote;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return OperationResult.Ok(batchId);
    }

    // ------------------------------------------------------------------
    // Adjustments
    // ------------------------------------------------------------------

    /// <summary>
    /// Changes a batch quantity for a reason other than receiving or selling
    /// (damage, write-off, count correction, and so on). Recorded as a stock movement.
    /// </summary>
    public async Task<OperationResult> AdjustStockAsync(AdjustStockInput input, int? userId)
    {
        var note = Clean(input.Note);

        if (input.QuantityChange == 0)
            return OperationResult.Fail("Enter a quantity to add or remove.");
        if (Math.Abs((long)input.QuantityChange) > MaxQuantityPerLine)
            return OperationResult.Fail("That quantity is too large.");
        if (input.QuantityChange > 0 && !AdjustmentReasons.AllowsIncrease(input.Reason))
            return OperationResult.Fail($"\"{AdjustmentReasons.Label(input.Reason)}\" can only remove stock.");
        if (AdjustmentReasons.RequiresNote(input.Reason) && note is null)
            return OperationResult.Fail("Please explain this adjustment in the note.");
        if (note?.Length > StockMovement.MaxNotesLength)
            return OperationResult.Fail($"Note must be {StockMovement.MaxNotesLength} characters or fewer.");

        await using var db = await _contextFactory.CreateDbContextAsync();

        var batch = await db.InventoryBatches.SingleOrDefaultAsync(b => b.Id == input.BatchId);
        if (batch is null)
            return OperationResult.Fail("This batch no longer exists.");

        if (input.QuantityChange > 0 && batch.Status != BatchStatus.Available)
            return OperationResult.Fail("Release this batch before adding stock to it.");

        var newBalance = batch.QuantityOnHand + input.QuantityChange;
        if (newBalance < 0)
            return OperationResult.Fail(
                $"Only {batch.QuantityOnHand:N0} left in this batch. You cannot remove {-input.QuantityChange:N0}.");

        batch.QuantityOnHand = newBalance;
        batch.UpdatedAt = DateTime.UtcNow;

        var movement = new StockMovement
        {
            MedicineId = batch.MedicineId,
            Batch = batch,
            MovementType = MovementType.Adjustment,
            QuantityChange = input.QuantityChange,
            BalanceAfter = newBalance,
            AdjustmentReason = input.Reason,
            Notes = note,
            UserId = userId
        };
        db.StockMovements.Add(movement);

        // One SaveChanges: the new balance and its movement record are saved together or not at all.
        await db.SaveChangesAsync();

        return OperationResult.Ok(movement.Id);
    }

    // ------------------------------------------------------------------
    // FEFO
    // ------------------------------------------------------------------

    /// <summary>
    /// Plans which batches to take stock from for <paramref name="quantity"/> units of a
    /// medicine, earliest expiry first. Only batches that are available and not expired are
    /// used, so recalled, on-hold and expired stock can never be selected.
    /// This only plans; it does not change any stock.
    /// </summary>
    public async Task<FefoPlan> GetFefoPlanAsync(int medicineId, int quantity)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var today = Today;

        var sellable = await db.InventoryBatches.AsNoTracking()
            .Where(b => b.MedicineId == medicineId
                        && b.Status == BatchStatus.Available
                        && b.QuantityOnHand > 0
                        && b.ExpiryDate >= today)
            .Select(b => new FefoBatch(b.Id, b.BatchNumber, b.ExpiryDate, b.QuantityOnHand))
            .ToListAsync();

        return FefoAllocator.Allocate(sellable, quantity);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
