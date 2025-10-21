using Rpg;

namespace Server.TUI;

public static class Loggers
{
    public static readonly Logger Console = new Logger();
    public static readonly Logger Web = new Logger("Web");
    static Loggers()
    {
        Logger.Default = Console;
    }
}
