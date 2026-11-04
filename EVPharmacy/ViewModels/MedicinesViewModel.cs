using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

/// <summary>Medicine catalog page: searchable list plus the add / edit form.</summary>
public sealed class MedicinesViewModel : PageViewModel
{
    private readonly MedicineService _medicines;
    private readonly LookupService _lookups;
    private readonly DialogService _dialogs;
    private readonly UserSession _session;

    private CancellationTokenSource? _searchCts;
    private string _searchText = string.Empty;
    private bool _showInactive;
    private MedicineFormViewModel? _form;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;

    public MedicinesViewModel(
        MedicineService medicines,
        LookupService lookups,
        DialogService dialogs,
        UserSession session)
        : base("Medicines", "Product master records")
    {
        _medicines = medicines;
        _lookups = lookups;
        _dialogs = dialogs;
        _session = session;

        AddCommand = new RelayCommand(() => _ = OpenFormAsync(null));
        EditCommand = new RelayCommand(p => { if (p is MedicineRow r) _ = OpenFormAsync(r.Id); });
        ToggleActiveCommand = new RelayCommand(p => { if (p is MedicineRow r) _ = ToggleActiveAsync(r); });

        _ = LoadAsync();
    }

    public ObservableCollection<MedicineRow> Items { get; } = [];

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

    public MedicineFormViewModel? Form
    {
        get => _form;
        private set
        {
            if (SetProperty(ref _form, value))
            {
                OnPropertyChanged(nameof(IsFormOpen));
                OnPropertyChanged(nameof(IsListVisible));
            }
        }
    }

    public bool IsFormOpen => Form is not null;

    public bool IsListVisible => Form is null;

    public bool IsEmpty => Items.Count == 0;

    public string EmptyText => string.IsNullOrWhiteSpace(SearchText)
        ? "No medicines yet. Click \"Add medicine\" to create the first one."
        : "No medicines match your search.";

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private async Task ReloadAfterDelayAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            // Wait for a pause in typing so we do not query on every keystroke.
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
            var list = await _medicines.SearchAsync(SearchText, ShowInactive);

            Items.Clear();
            foreach (var item in list)
            {
                Items.Add(new MedicineRow(item));
            }

            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading medicines failed");
            ErrorMessage = "The medicine list could not be loaded.";
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private async Task OpenFormAsync(int? medicineId)
    {
        try
        {
            StatusMessage = string.Empty;

            Form = await MedicineFormViewModel.CreateAsync(
                _medicines, _lookups, _dialogs,
                _session.CurrentUser?.Id,
                medicineId,
                onSaved: message => _ = CloseFormAsync(message),
                onCancel: () => Form = null);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Opening medicine form failed");
            ErrorMessage = "The medicine form could not be opened.";
        }
    }

    private async Task CloseFormAsync(string message)
    {
        Form = null;
        StatusMessage = message;
        await LoadAsync();
    }

    private async Task ToggleActiveAsync(MedicineRow row)
    {
        var activating = !row.IsActive;

        if (!activating)
        {
            var confirm = MessageBox.Show(
                $"Deactivate {row.GenericName}?\n\nIt will no longer be available for sale, " +
                "but its history is kept and you can reactivate it at any time.",
                "Deactivate medicine",
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
            var result = await _medicines.SetActiveAsync(row.Id, activating);

            if (result.Success)
            {
                StatusMessage = $"{row.GenericName} was {(activating ? "reactivated" : "deactivated")}.";
                await LoadAsync();
            }
            else
            {
                ErrorMessage = result.Error ?? "The change could not be saved.";
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Toggling medicine active state failed");
            ErrorMessage = "The change could not be saved.";
        }
    }
}
