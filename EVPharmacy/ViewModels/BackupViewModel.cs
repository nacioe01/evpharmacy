using EVPharmacy.Data.Database;
using EVPharmacy.Data.Services;
using EVPharmacy.Mvvm;
using EVPharmacy.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace EVPharmacy.ViewModels;

public sealed class BackupRow
{
    public BackupRow(BackupInfo info) => Info = info;

    public BackupInfo Info { get; }

    public string When => Info.CreatedAt.ToString("MMM d, yyyy h:mm tt");
    public string Kind => Info.Kind;

    public string KindPill => Info.Kind switch
    {
        "Manual" => StatusKind.Good,
        "Automatic" => StatusKind.Neutral,
        "Before restore" => StatusKind.Warn,
        "Before update" => StatusKind.Warn,
        _ => StatusKind.Neutral
    };

    public string Size => FormatSize(Info.SizeBytes);

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#} MB"
    };
}

/// <summary>Database backups and restore. Administrator only.</summary>
public sealed class BackupViewModel : PageViewModel
{
    private readonly DatabaseBackupService _backup;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly AuditService _audit;
    private readonly DialogService _dialogs;
    private readonly UserSession _session;

    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _databaseInfo = string.Empty;
    private string _lastBackupText = string.Empty;
    private bool _isBusy;

    public BackupViewModel(
        DatabaseBackupService backup,
        IDbContextFactory<AppDbContext> contextFactory,
        AuditService audit,
        DialogService dialogs,
        UserSession session)
        : base("Backup and restore", "Protect your data and recover from mistakes")
    {
        _backup = backup;
        _contextFactory = contextFactory;
        _audit = audit;
        _dialogs = dialogs;
        _session = session;

        BackUpNowCommand = new AsyncRelayCommand(BackUpNowAsync, () => !_isBusy);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        RestoreFromFileCommand = new AsyncRelayCommand(RestoreFromFileAsync, () => !_isBusy);
        RestoreCommand = new RelayCommand(p => { if (p is BackupRow r) _ = RestoreAsync(r.Info.Path); });
        SaveCopyCommand = new RelayCommand(p => { if (p is BackupRow r) SaveCopy(r); });

        Refresh();
    }

    public ObservableCollection<BackupRow> Backups { get; } = [];

    public ICommand BackUpNowCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand RestoreFromFileCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand SaveCopyCommand { get; }

    public string DatabasePath => AppDatabase.DatabasePath;

    public string BackupFolder => AppDatabase.BackupDirectory;

    public string DatabaseInfo { get => _databaseInfo; private set => SetProperty(ref _databaseInfo, value); }

    public string LastBackupText { get => _lastBackupText; private set => SetProperty(ref _lastBackupText, value); }

    public bool IsEmpty => Backups.Count == 0;

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

    private void Refresh()
    {
        try
        {
            Backups.Clear();
            foreach (var info in _backup.ListBackups())
            {
                Backups.Add(new BackupRow(info));
            }

            var size = File.Exists(AppDatabase.DatabasePath) ? new FileInfo(AppDatabase.DatabasePath).Length : 0;
            DatabaseInfo = $"Database size: {BackupRow.FormatSize(size)}";

            LastBackupText = Backups.Count == 0
                ? "No backups yet."
                : $"Last backup: {Backups[0].When} ({Backups[0].Kind.ToLowerInvariant()})";
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Listing backups failed");
            ErrorMessage = "The list of backups could not be loaded.";
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task BackUpNowAsync()
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        SetBusy(true);

        try
        {
            var path = await Task.Run(() => _backup.CreateBackup("manual"));
            await _audit.LogEventAsync("Backup", $"Manual backup created: {Path.GetFileName(path)}");

            StatusMessage = $"Backup saved: {Path.GetFileName(path)}";
            Refresh();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Manual backup failed");
            ErrorMessage = "The backup could not be created. Check that there is enough free disk space.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(AppDatabase.BackupDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppDatabase.BackupDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Opening backup folder failed");
            ErrorMessage = "The backup folder could not be opened.";
        }
    }

    private void SaveCopy(BackupRow row)
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        var target = _dialogs.PickSaveFile(
            "Save a copy of this backup",
            row.Info.FileName,
            "Database backup (*.db)|*.db");

        if (target is null)
        {
            return;
        }

        try
        {
            File.Copy(row.Info.Path, target, overwrite: true);
            StatusMessage = $"A copy was saved to {target}.";
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Copying backup failed");
            ErrorMessage = "The copy could not be saved. Choose another location and try again.";
        }
    }

    private async Task RestoreFromFileAsync()
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        var path = _dialogs.PickOpenFile("Choose a backup to restore", "Database backup (*.db)|*.db|All files (*.*)|*.*");
        if (path is not null)
        {
            await RestoreAsync(path);
        }
    }

    private async Task RestoreAsync(string path)
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        try
        {
            IReadOnlyCollection<string> known;
            await using (var db = await _contextFactory.CreateDbContextAsync())
            {
                known = db.Database.GetMigrations().ToList();
            }

            var problem = await Task.Run(() => _backup.ValidateBackup(path, known));
            if (problem is not null)
            {
                ErrorMessage = problem;
                return;
            }

            var confirm = MessageBox.Show(
                $"Restore from \"{Path.GetFileName(path)}\"?\n\n" +
                "Everything entered AFTER this backup was made (sales, stock, changes) will be replaced by the older data.\n\n" +
                "A safety copy of your current data is made first, and EV Pharmacy will restart when it is done.",
                "Restore backup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            SetBusy(true);

            var who = _session.CurrentUser?.Username;
            await _audit.LogEventAsync("Restore", $"Restore started from {Path.GetFileName(path)}");

            var safety = await Task.Run(() => _backup.Restore(path));

            // The signed-in user may not exist in the restored data, so no user id is recorded here.
            await _audit.LogEventAsync(
                "Restore",
                $"Database restored from {Path.GetFileName(path)}. Safety copy: {Path.GetFileName(safety)}",
                userId: null,
                username: who);

            MessageBox.Show(
                "The backup was restored.\n\nEV Pharmacy will now restart.",
                "Restore complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Restart();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log(ex, "Restore failed");
            ErrorMessage = "The restore did not complete and your current data was not changed. A safety copy is in the backup folder.";
            SetBusy(false);
        }
    }

    private static void Restart()
    {
        var exe = Environment.ProcessPath;

        if (!string.IsNullOrEmpty(exe) &&
            !Path.GetFileName(exe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }

        Application.Current.Shutdown();
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        CommandManager.InvalidateRequerySuggested();
    }
}
