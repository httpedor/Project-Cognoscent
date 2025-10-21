namespace Rpg;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public class Logger
{
    public struct LogMessage
    {
        public string Message;
        public ConsoleColor ForegroundColor;
        public ConsoleColor BackgroundColor;
        public LogLevel Level;
        public DateTime Timestamp;

        public LogMessage(string message, LogLevel level, ConsoleColor foregroundColor = ConsoleColor.White)
        {
            Message = message;
            Level = level;
            ForegroundColor = foregroundColor;
            BackgroundColor = ConsoleColor.Black;
            Timestamp = DateTime.Now;
        }
    }
    public static Logger? Default;
    public event Action<LogMessage>? OnLogAdded;

    public readonly int maxLogs = 100;
    public readonly string Name;
    public List<LogMessage> Logs { get; private set; } = new();

    public Logger(string name = "Cognos", int maxLogs = 100)
    {
        this.maxLogs = maxLogs;
        Name = name;
    }

    public void Log(string message, LogLevel level = LogLevel.Info, ConsoleColor? color = null)
    {
        var logColor = color ?? level switch
        {
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.White
        };
        switch (level)
        {
            case LogLevel.Warning:
                message = "[WARNING] " + message;
                break;
            case LogLevel.Error:
                message = "[ERROR] " + message;
                break;
        }
        var logMessage = new LogMessage(message, level, logColor);
        Logs.Add(logMessage);
        if (Logs.Count > maxLogs)
        {
            Logs.RemoveAt(0);
        }
        OnLogAdded?.Invoke(logMessage);
    }

    public void Clear()
    {
        Logs.Clear();
    }

    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (Default != null)
            Default.Log(message, level);
        else
        {
            Console.WriteLine("[No Default Logger Set]");
            Console.WriteLine(message);
        }
    }

    public static void LogError(string message)
    {
        Log(message, LogLevel.Error);
    }
    public static void LogWarning(string message)
    {
        Log(message, LogLevel.Warning);
    }
}
