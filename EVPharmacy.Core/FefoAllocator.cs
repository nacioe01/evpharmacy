
namespace EVPharmacy.Core;

/// <summary>A batch as seen by the allocator. Only sellable batches should be passed in.</summary>
public sealed record FefoBatch(int BatchId, string BatchNumber, DateOnly ExpiryDate, int QuantityOnHand);

public sealed record FefoAllocation(int BatchId, string BatchNumber, DateOnly ExpiryDate, int Quantity);

/// <param name="Shortfall">Units that could not be covered by available stock (0 when fully covered).</param>
public sealed record FefoPlan(IReadOnlyList<FefoAllocation> Allocations, int Shortfall)
{
    public bool IsFullyCovered => Shortfall == 0;

    public int TotalAllocated => Allocations.Sum(a => a.Quantity);
}

/// <summary>
/// First-Expiry-First-Out selection: take stock from the batch that expires
/// soonest, then the next, until the requested quantity is covered.
/// Pure logic with no database access, so it is easy to test.
/// </summary>
public static class FefoAllocator
{
    public static FefoPlan Allocate(IEnumerable<FefoBatch> batches, int quantity)
    {
        if (quantity <= 0)
        {
            return new FefoPlan([], 0);
        }

        var remaining = quantity;
        var allocations = new List<FefoAllocation>();

        // Ties on expiry date: the lower id (received earlier) goes first.
        foreach (var batch in batches
                     .Where(b => b.QuantityOnHand > 0)
                     .OrderBy(b => b.ExpiryDate)
                     .ThenBy(b => b.BatchId))
        {
            if (remaining == 0)
            {
                break;
            }

            var take = Math.Min(batch.QuantityOnHand, remaining);
            allocations.Add(new FefoAllocation(batch.BatchId, batch.BatchNumber, batch.ExpiryDate, take));
            remaining -= take;
        }

        return new FefoPlan(allocations, remaining);
    }
}
