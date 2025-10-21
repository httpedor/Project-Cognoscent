using Microsoft.Extensions.Logging;
using Server.TUI;

namespace Server.Web;

internal sealed class TuiLogger : ILogger
{
    private readonly string _category;

    public TuiLogger(string category)
    {
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            string msg = formatter(state, exception);
            if (exception != null)
                msg += "\n" + exception;

            switch (logLevel)
            {
                case Microsoft.Extensions.Logging.LogLevel.Critical:
                case Microsoft.Extensions.Logging.LogLevel.Error:
                    Loggers.Web.Log(msg, Rpg.LogLevel.Error);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Warning:
                    Loggers.Web.Log(msg, Rpg.LogLevel.Warning);
                    break;
                default:
                    Loggers.Web.Log(msg, Rpg.LogLevel.Info);
                    break;
            }
        }
        catch
        {
            // Never throw from logger
        }
    }
}

internal sealed class TuiLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TuiLogger(categoryName);

    public void Dispose() { }
}
