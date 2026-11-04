using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed class ReportOption : ObservableObject
{
    private bool _isSelected;

    public ReportOption(ReportInfo info) => Info = info;

    public ReportInfo Info { get; }

    public string Title => Info.Title;
    public string Description => Info.Description;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed record TotalCard(string Label, string Value);

/// <summary>Sales and inventory reports with CSV export.</summary>
public sealed class ReportsViewModel : PageViewModel
{
    private readonly ReportService _reports;
    private readonly DialogService _dialogs;

    private ReportOption? _selected;
    private DateTime? _from;
    private DateTime? _to;
    private ReportTable? _table;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private int _loadVersion;

    public ReportsViewModel(ReportService reports, DialogService dialogs)
        : base("Reports", "Sales and inventory insights")
    {
        _reports = reports;
        _dialogs = dialogs;

        foreach (var info in ReportService.Catalog)
        {
            Options.Add(new ReportOption(info));
        }

        SelectCommand = new RelayCommand(p => { if (p is ReportOption o) Select(o); });
        PresetCommand = new RelayCommand(p => ApplyPreset(p as string));
        ExportCommand = new RelayCommand(Export, () => Table is { Rows.Count: > 0 });

        _from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _to = DateTime.Today;

        Select(Options[0]);
    }

    public ObservableCollection<ReportOption> Options { get; } = [];

    public ObservableCollection<TotalCard> Totals { get; } = [];

    public ICommand SelectCommand { get; }
    public ICommand PresetCommand { get; }
    public ICommand ExportCommand { get; }

    public ReportOption? Selected
    {
        get => _selected;
        private set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(ShowRange));
                OnPropertyChanged(nameof(ShowUpTo));
                OnPropertyChanged(nameof(ShowFrom));
                OnPropertyChanged(nameof(Heading));
            }
        }
    }

    public string Heading => Selected?.Title ?? string.Empty;

    public bool ShowRange => Selected?.Info.DateMode == ReportDateMode.Range;
    public bool ShowUpTo => Selected?.Info.DateMode == ReportDateMode.UpTo;
    public bool ShowFrom => ShowRange;

    public DateTime? From
    {
        get => _from;
        set { if (SetProperty(ref _from, value)) _ = LoadAsync(); }
    }

    public DateTime? To
    {
        get => _to;
        set { if (SetProperty(ref _to, value)) _ = LoadAsync(); }
    }

    public ReportTable? Table
    {
        get => _table;
        private set
        {
            if (SetProperty(ref _table, value))
            {
                OnPropertyChanged(nameof(ReportSubtitle));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(RowCount));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ReportSubtitle => Table?.Subtitle ?? string.Empty;

    public bool IsEmpty => Table is { Rows.Count: 0 };

    public string RowCount => Table is null ? string.Empty : $"{Table.Rows.Count:N0} row{(Table.Rows.Count == 1 ? "" : "s")}";

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

    private void Select(ReportOption option)
    {
        foreach (var o in Options)
        {
            o.IsSelected = o == option;
        }

        Selected = option;
        StatusMessage = string.Empty;

        // The expiry report is about the future; the sales ones are about the past.
        if (option.Info.DateMode == ReportDateMode.UpTo)
        {
            _to = DateTime.Today.AddDays(90);
            OnPropertyChanged(nameof(To));
        }
        else if (option.Info.DateMode == ReportDateMode.Range && _to > DateTime.Today)
        {
            _to = DateTime.Today;
            OnPropertyChanged(nameof(To));
        }

        _ = LoadAsync();
    }

    private void ApplyPreset(string? preset)
    {
        var today = DateTime.Today;

        (DateTime? from, DateTime? to) range = preset switch
        {
            "today" => (today, today),
            "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "7" => (today.AddDays(-6), today),
            "30" => (today.AddDays(-29), today),
            "month" => (new DateTime(today.Year, today.Month, 1), today),
            "lastmonth" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                            new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            "year" => (new DateTime(today.Year, 1, 1), today),
            "d30" => (null, today.AddDays(30)),
            "d60" => (null, today.AddDays(60)),
            "d90" => (null, today.AddDays(90)),
            "d180" => (null, today.AddDays(180)),
            _ => (null, null)
        };

        var from = range.from;
        var to = range.to;

        if (from is not null)
        {
            _from = from;
            OnPropertyChanged(nameof(From));
        }

        if (to is not null)
        {
            _to = to;
            OnPropertyChanged(nameof(To));
        }

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var option = Selected;
        if (option is null)
        {
            return;
        }

        var version = ++_loadVersion;
        ErrorMessage = string.Empty;

        var to = DateOnly.FromDateTime(_to ?? DateTime.Today);
        var from = DateOnly.FromDateTime(_from ?? _to ?? DateTime.Today);

        if (option.Info.DateMode == ReportDateMode.Range && from > to)
        {
            ErrorMessage = "The start date is after the end date.";
            return;
        }

        try
        {
            var table = await _reports.BuildAsync(option.Info.Kind, from, to);

            // A newer request may have started while this one was running.
            if (version != _loadVersion)
            {
                return;
            }

            Totals.Clear();
            foreach (var (label, value) in table.Totals)
            {
                Totals.Add(new TotalCard(label, value));
            }

            Table = table;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Building report failed");
            ErrorMessage = "The report could not be created.";
        }
    }

    private void Export()
    {
        if (Table is null)
        {
            return;
        }

        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        var safeName = string.Concat(Table.Title.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        var path = _dialogs.PickSaveFile(
            "Export report",
            $"{safeName}_{DateTime.Now:yyyyMMdd}.csv",
            "CSV file for Excel (*.csv)|*.csv");

        if (path is null)
        {
            return;
        }

        try
        {
            File.WriteAllText(path, Table.ToCsv(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            StatusMessage = $"Saved {Table.Rows.Count:N0} rows to {path}.";
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Exporting report failed");
            ErrorMessage = "The file could not be saved. Choose another location, or close it if it is open in Excel.";
        }
    }
}
