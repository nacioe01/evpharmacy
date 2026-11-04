using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public enum ListKind
{
    Categories,
    DosageForms,
    Units
}

public sealed class LookupRow
{
    public LookupRow(LookupEntry entry) => Entry = entry;

    public LookupEntry Entry { get; }

    public int Id => Entry.Id;
    public string Name => Entry.Name;
    public bool IsActive => Entry.IsActive;
    public string Status => Entry.IsActive ? "Active" : "Inactive";
    public string Kind => Entry.IsActive ? StatusKind.Good : StatusKind.Neutral;
    public string ToggleActiveText => Entry.IsActive ? "Deactivate" : "Reactivate";

    public string Usage => Entry.MedicineCount switch
    {
        0 => "Not used yet",
        1 => "1 medicine",
        var n => $"{n:N0} medicines"
    };
}

/// <summary>Manages the pick-lists used by the medicine form (categories, dosage forms, units).</summary>
public sealed class ListsViewModel : PageViewModel
{
    private readonly LookupService _lookups;
    private readonly DialogService _dialogs;

    private ListKind _kind = ListKind.Categories;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;

    public ListsViewModel(LookupService lookups, DialogService dialogs)
        : base("Catalog lists", "Categories, dosage forms and units of measure")
    {
        _lookups = lookups;
        _dialogs = dialogs;

        SetKindCommand = new RelayCommand(p => { if (p is ListKind k) Kind = k; });
        AddCommand = new RelayCommand(() => _ = AddAsync());
        RenameCommand = new RelayCommand(p => { if (p is LookupRow r) _ = RenameAsync(r); });
        ToggleActiveCommand = new RelayCommand(p => { if (p is LookupRow r) _ = ToggleActiveAsync(r); });

        _ = LoadAsync();
    }

    public ObservableCollection<LookupRow> Items { get; } = [];

    public ICommand SetKindCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand ToggleActiveCommand { get; }

    public ListKind Kind
    {
        get => _kind;
        private set
        {
            if (SetProperty(ref _kind, value))
            {
                StatusMessage = string.Empty;
                OnPropertyChanged(nameof(IsCategories));
                OnPropertyChanged(nameof(IsDosageForms));
                OnPropertyChanged(nameof(IsUnits));
                OnPropertyChanged(nameof(AddText));
                OnPropertyChanged(nameof(Hint));
                _ = LoadAsync();
            }
        }
    }

    public bool IsCategories => Kind == ListKind.Categories;
    public bool IsDosageForms => Kind == ListKind.DosageForms;
    public bool IsUnits => Kind == ListKind.Units;

    public string Singular => Kind switch
    {
        ListKind.Categories => "category",
        ListKind.DosageForms => "dosage form",
        _ => "unit of measure"
    };

    public string AddText => $"+  Add {Singular}";

    public string Hint => Kind switch
    {
        ListKind.Categories => "Group medicines for browsing and reports, for example Antibiotics or Vitamins.",
        ListKind.DosageForms => "The physical form of the medicine, for example Tablet, Syrup or Cream.",
        _ => "How a medicine is counted and sold, for example Piece, Box or Bottle."
    };

    public bool IsEmpty => Items.Count == 0;

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

    // ------------------------------------------------------------------

    private async Task LoadAsync()
    {
        try
        {
            var entries = Kind switch
            {
                ListKind.Categories => await _lookups.GetEntriesAsync<Category>(),
                ListKind.DosageForms => await _lookups.GetEntriesAsync<DosageForm>(),
                _ => await _lookups.GetEntriesAsync<UnitOfMeasure>()
            };

            Items.Clear();
            foreach (var entry in entries)
            {
                Items.Add(new LookupRow(entry));
            }

            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Loading catalog lists failed");
            ErrorMessage = "The list could not be loaded.";
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task AddAsync()
    {
        await PromptUntilAcceptedAsync(
            $"Add {Singular}",
            $"Name of the new {Singular}",
            initial: null,
            acceptText: "Add",
            save: name => Kind switch
            {
                ListKind.Categories => _lookups.AddAsync<Category>(name),
                ListKind.DosageForms => _lookups.AddAsync<DosageForm>(name),
                _ => _lookups.AddAsync<UnitOfMeasure>(name)
            },
            successMessage: name => $"\"{name}\" was added.");
    }

    private async Task RenameAsync(LookupRow row)
    {
        await PromptUntilAcceptedAsync(
            $"Rename {Singular}",
            $"New name for \"{row.Name}\"",
            initial: row.Name,
            acceptText: "Save",
            save: name => Kind switch
            {
                ListKind.Categories => _lookups.RenameAsync<Category>(row.Id, name),
                ListKind.DosageForms => _lookups.RenameAsync<DosageForm>(row.Id, name),
                _ => _lookups.RenameAsync<UnitOfMeasure>(row.Id, name)
            },
            successMessage: name => $"Renamed to \"{name}\".");
    }

    /// <summary>Asks for a name, re-asking with the error message until it is accepted or cancelled.</summary>
    private async Task PromptUntilAcceptedAsync(
        string title,
        string label,
        string? initial,
        string acceptText,
        Func<string, Task<OperationResult>> save,
        Func<string, string> successMessage)
    {
        string? error = null;
        var value = initial;

        try
        {
            while (true)
            {
                value = _dialogs.PromptForText(title, label, value, error, acceptText);
                if (value is null)
                {
                    return;
                }

                var result = await save(value);
                if (result.Success)
                {
                    StatusMessage = successMessage(value.Trim());
                    await LoadAsync();
                    return;
                }

                error = result.Error;
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Saving catalog list entry failed");
            ErrorMessage = "The change could not be saved.";
        }
    }

    private async Task ToggleActiveAsync(LookupRow row)
    {
        var activating = !row.IsActive;

        if (!activating)
        {
            var inUse = row.Entry.MedicineCount > 0
                ? $"\n\n{row.Usage} use it. They keep it, but it will no longer be offered for new medicines."
                : string.Empty;

            var confirm = MessageBox.Show(
                $"Deactivate \"{row.Name}\"?{inUse}",
                $"Deactivate {Singular}",
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
            var result = Kind switch
            {
                ListKind.Categories => await _lookups.SetActiveAsync<Category>(row.Id, activating),
                ListKind.DosageForms => await _lookups.SetActiveAsync<DosageForm>(row.Id, activating),
                _ => await _lookups.SetActiveAsync<UnitOfMeasure>(row.Id, activating)
            };

            if (result.Success)
            {
                StatusMessage = $"\"{row.Name}\" was {(activating ? "reactivated" : "deactivated")}.";
                await LoadAsync();
            }
            else
            {
                ErrorMessage = result.Error ?? "The change could not be saved.";
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Toggling catalog list entry failed");
            ErrorMessage = "The change could not be saved.";
        }
    }
}
