namespace RemoteViewerApp.Helpers;

public enum LogLevel { Info, Warning, Error, Debug }

/// <summary>
/// Helper log đơn giản, output qua callback ra UI (thread-safe via BeginInvoke)
/// </summary>
public static class LoggingHelper
{
    public static Action<string, LogLevel>? OnLog { get; set; }

    public static void Info(string msg)    => Log(msg, LogLevel.Info);
    public static void Warning(string msg) => Log(msg, LogLevel.Warning);
    public static void Error(string msg)   => Log(msg, LogLevel.Error);
    public static void Debug(string msg)   => Log(msg, LogLevel.Debug);

    private static void Log(string msg, LogLevel level)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var prefix = level switch
        {
            LogLevel.Error   => "[ERR ]",
            LogLevel.Warning => "[WARN]",
            LogLevel.Debug   => "[DBG ]",
            _                => "[INFO]",
        };
        var line = $"{timestamp} {prefix} {msg}";
        Console.WriteLine(line);
        OnLog?.Invoke(line, level);
    }
}
