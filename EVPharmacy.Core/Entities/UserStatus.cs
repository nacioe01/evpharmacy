
namespace EVPharmacy.Core.Entities;

/// <summary>
/// Account status. Persisted as lowercase text ("active", "deactivated")
/// to stay compatible with the existing CK_users_status check constraint.
/// </summary>
public enum UserStatus
{
    Active,
    Deactivated
}
