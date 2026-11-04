
namespace EVPharmacy.Data.Services;

/// <summary>Outcome of a service call that can fail for a user-correctable reason.</summary>
public sealed record OperationResult(bool Success, string? Error = null, int? Id = null)
{
    public static OperationResult Ok(int? id = null) => new(true, null, id);

    public static OperationResult Fail(string error) => new(false, error);
}
