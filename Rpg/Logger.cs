namespace Rpg;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public class Logger : ISerializable
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
    public Logger(Stream stream)
    {
        Name = stream.ReadString();
        maxLogs = stream.ReadInt32();
        int logCount = stream.ReadInt32();
        Logs = new List<LogMessage>();
        for (int i = 0; i < logCount; i++)
        {
            string message = stream.ReadString();
            ConsoleColor fg = (ConsoleColor)stream.ReadByte();
            ConsoleColor bg = (ConsoleColor)stream.ReadByte();
            LogLevel level = (LogLevel)stream.ReadByte();
            DateTime timestamp = DateTime.FromBinary(stream.ReadInt64());
            Logs.Add(new LogMessage(message, level, fg)
            {
                BackgroundColor = bg,
                Timestamp = timestamp
            });
        }
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

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        stream.WriteInt32(Logs.Count);
        foreach (var log in Logs)
        {
            stream.WriteString(log.Message);
            stream.WriteByte((byte)log.ForegroundColor);
            stream.WriteByte((byte)log.BackgroundColor);
            stream.WriteByte((byte)log.Level);
            stream.WriteInt64(log.Timestamp.ToBinary());
        }
    }
}
