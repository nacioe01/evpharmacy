using EVPharmacy.Data.Services;

namespace EVPharmacy.Services;

/// <summary>
/// Holds the currently signed-in user for the lifetime of the session.
/// It also tells the audit trail who is acting, so every change is attributed.
/// </summary>
public class UserSession
{
    private readonly AuditContextAccessor? _auditWho;

    public UserSession(AuditContextAccessor auditWho)
    {
        _auditWho = auditWho;
    }

    /// <summary>For tests and design-time use, where no audit trail is wired up.</summary>
    public UserSession()
    {
    }

    public AuthenticatedUser? CurrentUser { get; private set; }

    public bool IsSignedIn => CurrentUser is not null;

    public bool IsAdmin => CurrentUser?.IsAdmin == true;

    public event EventHandler? Changed;

    public void SignIn(AuthenticatedUser user)
    {
        CurrentUser = user;
        PublishToAudit();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        CurrentUser = null;
        PublishToAudit();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void PublishToAudit()
    {
        if (_auditWho is null)
        {
            return;
        }

        _auditWho.UserId = CurrentUser?.Id;
        _auditWho.Username = CurrentUser?.Username;
    }
}
