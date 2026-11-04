
using EVPharmacy.Core.Entities;
using EVPharmacy.Core.Security;
using EVPharmacy.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace EVPharmacy.Data.Services;

public enum AuthenticationResult
{
    Success,
    InvalidCredentials,
    AccountDeactivated
}

public sealed record AuthenticatedUser(int Id, string Username, bool IsAdmin);

public sealed record AuthenticationOutcome(
    AuthenticationResult Result,
    AuthenticatedUser? User = null);

/// <summary>
/// User account operations. Each call uses its own short-lived DbContext,
/// so the service itself is safe to register as a singleton.
/// </summary>
public class UserService
{
    public const int MaxUsernameLength = 36;

    // Used to keep login timing similar whether or not the username exists.
    private static readonly Lazy<string> DummyHash =
        new(() => PasswordHasher.HashPassword(Guid.NewGuid().ToString()));

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    private readonly AuditService _audit;

    public UserService(IDbContextFactory<AppDbContext> contextFactory, AuditService audit)
    {
        _contextFactory = contextFactory;
        _audit = audit;
    }

    public async Task<bool> AnyUsersExistAsync()
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync();
        return await dbContext.Users.AnyAsync();
    }

    /// <summary>
    /// Creates an administrator account.
    /// Returns false when the username is already taken.
    /// Throws ArgumentException when input fails validation.
    /// </summary>
    public async Task<bool> CreateAdminAsync(string username, string password)
    {
        var normalizedUsername = username?.Trim() ?? string.Empty;

        var usernameError = ValidateUsername(normalizedUsername);
        if (usernameError is not null)
        {
            throw new ArgumentException(usernameError);
        }

        var passwordError = PasswordPolicy.Validate(password);
        if (passwordError is not null)
        {
            throw new ArgumentException(passwordError);
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync();

        // Comparison is case-insensitive via the NOCASE column collation.
        var exists = await dbContext.Users.AnyAsync(
            user => user.Username == normalizedUsername);

        if (exists)
        {
            return false;
        }

        dbContext.Users.Add(new User
        {
            Username = normalizedUsername,
            PasswordHash = PasswordHasher.HashPassword(password),
            Status = UserStatus.Active,
            IsAdmin = true
        });

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index caught a concurrent duplicate.
            return false;
        }

        return true;
    }

    public async Task<AuthenticationOutcome> AuthenticateAsync(string username, string password)
    {
        var outcome = await AuthenticateCoreAsync(username, password);
        var name = username?.Trim() ?? string.Empty;

        // The attempted password is never logged.
        switch (outcome.Result)
        {
            case AuthenticationResult.Success when outcome.User is not null:
                await _audit.LogEventAsync("Login", $"{outcome.User.Username} signed in",
                    userId: outcome.User.Id, username: outcome.User.Username);
                break;
            case AuthenticationResult.AccountDeactivated:
                await _audit.LogEventAsync("LoginFailed", $"Sign-in blocked: account \"{name}\" is deactivated", username: name);
                break;
            default:
                if (name.Length > 0)
                {
                    await _audit.LogEventAsync("LoginFailed", $"Failed sign-in attempt for \"{name}\"", username: name);
                }

                break;
        }

        return outcome;
    }

    private async Task<AuthenticationOutcome> AuthenticateCoreAsync(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return new AuthenticationOutcome(AuthenticationResult.InvalidCredentials);
        }

        var normalizedUsername = username.Trim();

        await using var dbContext = await _contextFactory.CreateDbContextAsync();

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Username == normalizedUsername);

        if (user is null)
        {
            PasswordHasher.VerifyPassword(password, DummyHash.Value);
            return new AuthenticationOutcome(AuthenticationResult.InvalidCredentials);
        }

        if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
        {
            return new AuthenticationOutcome(AuthenticationResult.InvalidCredentials);
        }

        if (user.Status != UserStatus.Active)
        {
            return new AuthenticationOutcome(AuthenticationResult.AccountDeactivated);
        }

        return new AuthenticationOutcome(
            AuthenticationResult.Success,
            new AuthenticatedUser(user.Id, user.Username, user.IsAdmin));
    }

    public static string? ValidateUsername(string? username)
    {
        var value = username?.Trim() ?? string.Empty;

        if (value.Length == 0)
        {
            return "Username is required.";
        }

        if (value.Length > MaxUsernameLength)
        {
            return $"Username must be {MaxUsernameLength} characters or fewer.";
        }

        if (value.Any(char.IsWhiteSpace))
        {
            return "Username cannot contain spaces.";
        }

        return null;
    }
}
