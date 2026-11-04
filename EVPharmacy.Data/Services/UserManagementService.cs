
using EVPharmacy.Core.Entities;
using EVPharmacy.Core.Security;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Services;

public sealed record UserListItem(
    int Id,
    string Username,
    bool IsAdmin,
    UserStatus Status,
    DateTime CreatedAtUtc);

/// <summary>
/// Staff accounts for administrators. Accounts are deactivated, never deleted, so the
/// history of who made each sale or change stays intact.
/// </summary>
public class UserManagementService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public UserManagementService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<UserListItem>> ListAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var users = await db.Users.AsNoTracking().ToListAsync();

        return users
            .OrderByDescending(u => u.IsAdmin)
            .ThenBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .Select(u => new UserListItem(u.Id, u.Username, u.IsAdmin, u.Status, u.CreatedAt))
            .ToList();
    }

    public async Task<OperationResult> CreateAsync(string? username, string? password, bool isAdmin)
    {
        var name = username?.Trim() ?? string.Empty;

        var error = UserService.ValidateUsername(name) ?? PasswordPolicy.Validate(password);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        // The username column is NOCASE, so "Admin" and "admin" collide.
        if (await db.Users.AnyAsync(u => u.Username == name))
        {
            return OperationResult.Fail($"The username \"{name}\" is already taken.");
        }

        var user = new User
        {
            Username = name,
            PasswordHash = PasswordHasher.HashPassword(password!),
            IsAdmin = isAdmin,
            Status = UserStatus.Active
        };
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return OperationResult.Fail($"The username \"{name}\" is already taken.");
        }

        return OperationResult.Ok(user.Id);
    }

    public async Task<OperationResult> ResetPasswordAsync(int userId, string? newPassword)
    {
        var error = PasswordPolicy.Validate(newPassword);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return OperationResult.Fail("This user no longer exists.");
        }

        user.PasswordHash = PasswordHasher.HashPassword(newPassword!);
        await db.SaveChangesAsync();

        return OperationResult.Ok(userId);
    }

    public async Task<OperationResult> SetActiveAsync(int userId, bool active, int? actingUserId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return OperationResult.Fail("This user no longer exists.");
        }

        if (!active)
        {
            if (userId == actingUserId)
            {
                return OperationResult.Fail("You cannot deactivate your own account.");
            }

            if (user.IsAdmin && user.Status == UserStatus.Active && await IsLastActiveAdminAsync(db, userId))
            {
                return OperationResult.Fail("This is the only active administrator. Make another user an administrator first.");
            }
        }

        user.Status = active ? UserStatus.Active : UserStatus.Deactivated;
        await db.SaveChangesAsync();

        return OperationResult.Ok(userId);
    }

    public async Task<OperationResult> SetAdminAsync(int userId, bool isAdmin, int? actingUserId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return OperationResult.Fail("This user no longer exists.");
        }

        if (!isAdmin)
        {
            if (userId == actingUserId)
            {
                return OperationResult.Fail("You cannot remove your own administrator access. Ask another administrator.");
            }

            if (user.IsAdmin && user.Status == UserStatus.Active && await IsLastActiveAdminAsync(db, userId))
            {
                return OperationResult.Fail("This is the only active administrator.");
            }
        }

        user.IsAdmin = isAdmin;
        await db.SaveChangesAsync();

        return OperationResult.Ok(userId);
    }

    private static async Task<bool> IsLastActiveAdminAsync(AppDbContext db, int userId) =>
        !await db.Users.AnyAsync(u => u.Id != userId && u.IsAdmin && u.Status == UserStatus.Active);
}
