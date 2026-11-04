
using EVPharmacy.Data.Services;

namespace EVPharmacy.Services;

/// <summary>
/// Holds the currently signed-in user for the lifetime of the session.
/// </summary>
public class UserSession
{
    public AuthenticatedUser? CurrentUser { get; private set; }

    public bool IsSignedIn => CurrentUser is not null;

    public bool IsAdmin => CurrentUser?.IsAdmin == true;

    public event EventHandler? Changed;

    public void SignIn(AuthenticatedUser user)
    {
        CurrentUser = user;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        CurrentUser = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
