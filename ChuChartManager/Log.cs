using System.Diagnostics;

namespace ChuChartManager;

public static class Log
{
    public enum Level { Debug, Info, Warning, Error }

    public static Level MinLevel { get; set; } = Level.Info;

    public static void Debug(string msg) => Write(Level.Debug, msg);
    public static void Info(string msg) => Write(Level.Info, msg);
    public static void Warn(string msg) => Write(Level.Warning, msg);
    public static void Error(string msg) => Write(Level.Error, msg);
    public static void Error(string msg, Exception ex) => Write(Level.Error, $"{msg}: {ex.Message}");

    private static void Write(Level level, string msg)
    {
        if (level < MinLevel) return;
        var prefix = level switch
        {
            Level.Debug => "DBG",
            Level.Info => "INF",
            Level.Warning => "WRN",
            Level.Error => "ERR",
            _ => "???"
        };
        var line = $"[{DateTime.Now:HH:mm:ss}] [{prefix}] {msg}";
        Trace.WriteLine(line);
    }

    /// <summary>debug 模式下把 Trace 输出重定向到控制台</summary>
    public static void EnableConsoleOutput()
    {
        if (!Trace.Listeners.OfType<ConsoleTraceListener>().Any())
            Trace.Listeners.Add(new ConsoleTraceListener());
        MinLevel = Level.Debug;
    }
}
