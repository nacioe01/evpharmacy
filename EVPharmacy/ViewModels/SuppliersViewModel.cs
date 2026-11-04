using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed class SupplierRow
{
    public SupplierRow(Supplier supplier) => Supplier = supplier;

    public Supplier Supplier { get; }
    public int Id => Supplier.Id;
    public string Name => Supplier.Name;
    public string Contact => Supplier.ContactPerson ?? "—";
    public string Phone => Supplier.Phone ?? "—";
    public string Email => Supplier.Email ?? "—";
    public bool IsActive => Supplier.IsActive;
    public string Status => Supplier.IsActive ? "Active" : "Inactive";
    public string Kind => Supplier.IsActive ? StatusKind.Good : StatusKind.Neutral;
    public string ToggleActiveText => Supplier.IsActive ? "Deactivate" : "Reactivate";
}

/// <summary>Supplier directory page: searchable list plus the add / edit form.</summary>
public sealed class SuppliersViewModel : PageViewModel
{
    private readonly SupplierService _suppliers;

    private CancellationTokenSource? _searchCts;
    private string _searchText = string.Empty;
    private bool _showInactive;
    private SupplierFormViewModel? _form;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;

    public SuppliersViewModel(SupplierService suppliers)
        : base("Suppliers", "Supplier directory")
    {
        _suppliers = suppliers;

        AddCommand = new RelayCommand(() => _ = OpenFormAsync(null));
        EditCommand = new RelayCommand(p => { if (p is SupplierRow r) _ = OpenFormAsync(r.Id); });
        ToggleActiveCommand = new RelayCommand(p => { if (p is SupplierRow r) _ = ToggleActiveAsync(r); });

        _ = LoadAsync();
    }

    public ObservableCollection<SupplierRow> Items { get; } = [];

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ToggleActiveCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) _ = ReloadAfterDelayAsync(); }
    }

    public bool ShowInactive
    {
        get => _showInactive;
        set { if (SetProperty(ref _showInactive, value)) _ = LoadAsync(); }
    }

    public SupplierFormViewModel? Form
    {
        get => _form;
        private set
        {
            if (SetProperty(ref _form, value))
            {
                OnPropertyChanged(nameof(IsListVisible));
            }
        }
    }

    public bool IsListVisible => Form is null;

    public bool IsEmpty => Items.Count == 0;

    public string EmptyText => string.IsNullOrWhiteSpace(SearchText)
        ? "No suppliers yet. Click \"Add supplier\" to create the first one."
        : "No suppliers match your search.";

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

    private async Task ReloadAfterDelayAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(250, cts.Token);
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var list = await _suppliers.SearchAsync(SearchText, ShowInactive);

            Items.Clear();
            foreach (var supplier in list)
            {
                Items.Add(new SupplierRow(supplier));
            }

            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading suppliers failed");
            ErrorMessage = "The supplier list could not be loaded.";
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private async Task OpenFormAsync(int? supplierId)
    {
        try
        {
            StatusMessage = string.Empty;

            Supplier? existing = null;
            if (supplierId is int id)
            {
                existing = await _suppliers.GetAsync(id)
                    ?? throw new InvalidOperationException("This supplier no longer exists.");
            }

            Form = new SupplierFormViewModel(
                _suppliers, existing,
                onSaved: message => _ = CloseFormAsync(message),
                onCancel: () => Form = null);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Opening supplier form failed");
            ErrorMessage = "The supplier form could not be opened.";
        }
    }

    private async Task CloseFormAsync(string message)
    {
        Form = null;
        StatusMessage = message;
        await LoadAsync();
    }

    private async Task ToggleActiveAsync(SupplierRow row)
    {
        var activating = !row.IsActive;

        if (!activating)
        {
            var confirm = MessageBox.Show(
                $"Deactivate {row.Name}?\n\nIt will no longer appear when receiving stock, " +
                "but past deliveries keep their supplier and you can reactivate it at any time.",
                "Deactivate supplier",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            var result = await _suppliers.SetActiveAsync(row.Id, activating);

            if (result.Success)
            {
                StatusMessage = $"{row.Name} was {(activating ? "reactivated" : "deactivated")}.";
                await LoadAsync();
            }
            else
            {
                ErrorMessage = result.Error ?? "The change could not be saved.";
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Toggling supplier active state failed");
            ErrorMessage = "The change could not be saved.";
        }
    }
}

public sealed class SupplierFormViewModel : ObservableObject
{
    private readonly SupplierService _suppliers;
    private readonly int? _supplierId;
    private readonly Action<string> _onSaved;

    private string _name = string.Empty;
    private string _contactPerson = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _address = string.Empty;
    private string _nameError = string.Empty;
    private string _errorMessage = string.Empty;

    public SupplierFormViewModel(
        SupplierService suppliers,
        Supplier? existing,
        Action<string> onSaved,
        Action onCancel)
    {
        _suppliers = suppliers;
        _supplierId = existing?.Id;
        _onSaved = onSaved;

        if (existing is not null)
        {
            _name = existing.Name;
            _contactPerson = existing.ContactPerson ?? string.Empty;
            _phone = existing.Phone ?? string.Empty;
            _email = existing.Email ?? string.Empty;
            _address = existing.Address ?? string.Empty;
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(onCancel);
    }

    public bool IsEditing => _supplierId is not null;
    public string Title => IsEditing ? "Edit supplier" : "Add supplier";
    public string SaveText => IsEditing ? "Save changes" : "Save supplier";

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) NameError = string.Empty; }
    }

    public string ContactPerson { get => _contactPerson; set => SetProperty(ref _contactPerson, value); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string Address { get => _address; set => SetProperty(ref _address, value); }

    public string NameError { get => _nameError; private set => SetProperty(ref _nameError, value); }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;

        var input = new SupplierInput(Name, ContactPerson, Phone, Email, Address);

        if (string.IsNullOrWhiteSpace(Name))
        {
            NameError = "Enter the supplier name.";
            return;
        }

        var invalid = SupplierService.Validate(input);
        if (invalid is not null)
        {
            ErrorMessage = invalid;
            return;
        }

        try
        {
            var result = _supplierId is int id
                ? await _suppliers.UpdateAsync(id, input)
                : await _suppliers.CreateAsync(input);

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "The supplier could not be saved.";
                return;
            }

            _onSaved(IsEditing ? $"{Name.Trim()} was updated." : $"{Name.Trim()} was added.");
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Saving supplier failed");
            ErrorMessage = "The supplier could not be saved. Your entries are still here, please try again.";
        }
    }
}
