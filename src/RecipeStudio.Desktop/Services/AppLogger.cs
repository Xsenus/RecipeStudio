using System;
using System.IO;
using System.Text;

namespace RecipeStudio.Desktop.Services;

public sealed class AppLogger
{
    private static readonly object Sync = new();

    public string LogFilePath { get; }

    public AppLogger(string appRoot)
    {
        var logsDir = Path.Combine(appRoot, "logs");
        Directory.CreateDirectory(logsDir);
        LogFilePath = Path.Combine(logsDir, "recipe-studio.log");
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
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
}
