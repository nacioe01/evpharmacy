
using EVPharmacy.Data.Database;
using System.IO;

namespace EVPharmacy.Services;

/// <summary>
/// Writes unexpected errors to a daily log file so technical details are
/// kept out of user-facing messages but remain available for support.
/// </summary>
public static class ErrorLogger
{
    private static readonly object Sync = new();

    public static string LogDirectory =>
        Path.Combine(AppDatabase.DataDirectory, "Logs");

    public static string? Log(Exception exception, string context)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            var path = Path.Combine(
                LogDirectory,
                $"error-{DateTime.Now:yyyyMMdd}.log");

            var entry =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}{Environment.NewLine}";

            lock (Sync)
            {
                File.AppendAllText(path, entry);
            }

            return path;
        }
        catch
        {
            // Logging must never crash the application.
            return null;
        }
    }
}
