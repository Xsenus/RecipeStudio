using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace RecipeStudio.Desktop.Services;

public sealed class AppLogger
{
    private static readonly object Sync = new();

    private bool _enabled = true;
    private int _retentionDays = 14;
    private string _logsDir;
    private string _minimumLevel = LogSeverity.Info;

    public string CurrentLogFilePath => Path.Combine(_logsDir, $"{DateTime.Now:dd.MM.yyyy}.log");

    public AppLogger()
    {
        _logsDir = ResolveDefaultLogsDir();
        Directory.CreateDirectory(_logsDir);
    }

    public void Configure(bool enabled, int retentionDays, string? minimumLevel, string? logsFolder)
    {
        _enabled = enabled;
        _retentionDays = Math.Clamp(retentionDays, 1, 3650);
        _minimumLevel = NormalizeLevel(minimumLevel);

        if (!string.IsNullOrWhiteSpace(logsFolder))
        {
            _logsDir = logsFolder;
        }

        try
        {
            Directory.CreateDirectory(_logsDir);
        }
        catch
        {
            // keep old folder if failed
        }

        CleanupOldLogs();
    }

    public void Info(string message) => Write(LogSeverity.Info, message, null);
    public void Warn(string message) => Write(LogSeverity.Warning, message, null);
    public void Error(string message, Exception? exception = null) => Write(LogSeverity.Error, message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        if (!_enabled)
            return;

        if (!ShouldWrite(level))
            return;

        try
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ")
              .Append(level).Append(' ').Append(message);

            if (exception is not null)
            {
                sb.AppendLine()
                  .Append(exception.GetType().FullName)
                  .Append(": ")
                  .Append(exception.Message)
                  .AppendLine()
                  .Append(exception.StackTrace ?? string.Empty);
            }

            lock (Sync)
            {
                File.AppendAllText(CurrentLogFilePath, sb.AppendLine().ToString());
            }
        }
        catch
        {
            // never crash because of logger
        }
    }

    private bool ShouldWrite(string level)
    {
        var incoming = SeverityRank(level);
        var min = SeverityRank(_minimumLevel);
        return incoming <= min;
    }

    private static int SeverityRank(string level)
    {
        return level switch
        {
            LogSeverity.Error => 1,
            LogSeverity.Warning => 2,
            _ => 3
        };
    }

    private static string NormalizeLevel(string? level)
    {
        return level switch
        {
            LogSeverity.Error => LogSeverity.Error,
            LogSeverity.Warning => LogSeverity.Warning,
            _ => LogSeverity.Info
        };
    }

    private void CleanupOldLogs()
    {
        try
        {
            if (!Directory.Exists(_logsDir))
                return;

            var keepFrom = DateTime.Today.AddDays(-(_retentionDays - 1));

            foreach (var file in Directory.EnumerateFiles(_logsDir, "*.log", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (DateTime.TryParseExact(name, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                {
                    if (fileDate.Date < keepFrom)
                    {
                        File.Delete(file);
                    }

                    continue;
                }

                if (File.GetLastWriteTime(file).Date < keepFrom)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // ignore cleanup errors
        }
    }

    private static string ResolveDefaultLogsDir()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "RecipeStudio", "logs");
    }
}
