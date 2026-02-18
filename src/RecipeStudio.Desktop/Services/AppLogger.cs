using System;
using System.IO;
using System.Text;

namespace RecipeStudio.Desktop.Services;

public sealed class AppLogger
{
    private static readonly object Sync = new();

    private readonly string _logsDir;
    private bool _enabled = true;
    private int _retentionDays = 14;

    public string LogFilePath { get; }

    public AppLogger(string appRoot)
    {
        _logsDir = Path.Combine(appRoot, "logs");
        Directory.CreateDirectory(_logsDir);
        LogFilePath = Path.Combine(_logsDir, "recipe-studio.log");
    }

    public void Configure(bool enabled, int retentionDays)
    {
        _enabled = enabled;
        _retentionDays = Math.Clamp(retentionDays, 1, 3650);
        CleanupOldLogs();
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        if (!_enabled)
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
                File.AppendAllText(LogFilePath, sb.AppendLine().ToString());
            }
        }
        catch
        {
            // never crash because of logger
        }
    }

    private void CleanupOldLogs()
    {
        try
        {
            if (!Directory.Exists(_logsDir))
                return;

            var threshold = DateTime.UtcNow.AddDays(-_retentionDays);
            foreach (var file in Directory.EnumerateFiles(_logsDir, "*.log", SearchOption.TopDirectoryOnly))
            {
                if (File.GetLastWriteTimeUtc(file) < threshold)
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
}
