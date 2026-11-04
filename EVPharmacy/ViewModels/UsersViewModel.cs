using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed class UserRow
{
    public UserRow(UserListItem item, bool isSelf)
    {
        Item = item;
        IsSelf = isSelf;
    }

    public UserListItem Item { get; }
    public bool IsSelf { get; }

    public int Id => Item.Id;
    public string Username => IsSelf ? $"{Item.Username} (you)" : Item.Username;
    public string Role => Item.IsAdmin ? "Administrator" : "Staff";
    public string RoleKind => Item.IsAdmin ? StatusKind.Warn : StatusKind.Neutral;
    public bool IsActive => Item.Status == UserStatus.Active;
    public string Status => IsActive ? "Active" : "Deactivated";
    public string StatusPill => IsActive ? StatusKind.Good : StatusKind.Bad;
    public string Created => Item.CreatedAtUtc.ToLocalTime().ToString("MMM d, yyyy");
    public string ToggleActiveText => IsActive ? "Deactivate" : "Reactivate";
    public string ToggleRoleText => Item.IsAdmin ? "Make staff" : "Make admin";
}

/// <summary>Administrator page for staff accounts.</summary>
public sealed class UsersViewModel : PageViewModel
{
    private readonly UserManagementService _users;
    private readonly DialogService _dialogs;
    private readonly UserSession _session;

    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;

    public UsersViewModel(UserManagementService users, DialogService dialogs, UserSession session)
        : base("Users", "Staff accounts and roles")
    {
        _users = users;
        _dialogs = dialogs;
        _session = session;

        AddCommand = new RelayCommand(() => _ = AddAsync());
        ResetPasswordCommand = new RelayCommand(p => { if (p is UserRow r) _ = ResetPasswordAsync(r); });
        ToggleActiveCommand = new RelayCommand(p => { if (p is UserRow r) _ = ToggleActiveAsync(r); });
        ToggleRoleCommand = new RelayCommand(p => { if (p is UserRow r) _ = ToggleRoleAsync(r); });

        _ = LoadAsync();
    }

    public ObservableCollection<UserRow> Items { get; } = [];

    public ICommand AddCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand ToggleRoleCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatus)); }
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private async Task LoadAsync()
    {
        try
        {
            var list = await _users.ListAsync();

            Items.Clear();
            foreach (var u in list)
            {
                Items.Add(new UserRow(u, u.Id == _session.CurrentUser?.Id));
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading users failed");
            ErrorMessage = "The user list could not be loaded.";
        }
    }

    private async Task AddAsync()
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        var details = _dialogs.ShowNewUserDialog();
        if (details is null)
        {
            return;
        }

        await RunAsync(
            () => _users.CreateAsync(details.Username, details.Password, details.IsAdministrator),
            $"{details.Username} was added as {(details.IsAdministrator ? "an administrator" : "staff")}.");
    }

    private async Task ResetPasswordAsync(UserRow row)
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        var password = _dialogs.ShowResetPasswordDialog(row.Item.Username);
        if (password is null)
        {
            return;
        }

        await RunAsync(
            () => _users.ResetPasswordAsync(row.Id, password),
            $"The password for {row.Item.Username} was changed.");
    }

    private async Task ToggleActiveAsync(UserRow row)
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        var activating = !row.IsActive;

        if (!activating)
        {
            var confirm = MessageBox.Show(
                $"Deactivate {row.Item.Username}?\n\nThey will no longer be able to sign in. Their past sales and changes stay on record.",
                "Deactivate user",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await RunAsync(
            () => _users.SetActiveAsync(row.Id, activating, _session.CurrentUser?.Id),
            $"{row.Item.Username} was {(activating ? "reactivated" : "deactivated")}.");
    }

    private async Task ToggleRoleAsync(UserRow row)
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        var makeAdmin = !row.Item.IsAdmin;

        await RunAsync(
            () => _users.SetAdminAsync(row.Id, makeAdmin, _session.CurrentUser?.Id),
            $"{row.Item.Username} is now {(makeAdmin ? "an administrator" : "staff")}.");
    }

    private async Task RunAsync(Func<Task<OperationResult>> action, string successMessage)
    {
        try
        {
            var result = await action();

            if (result.Success)
            {
                StatusMessage = successMessage;
                await LoadAsync();
            }
            else
            {
                ErrorMessage = result.Error ?? "The change could not be saved.";
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "User management action failed");
            ErrorMessage = "The change could not be saved.";
        }
    }
}
