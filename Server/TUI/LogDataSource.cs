using System.Collections;
using Terminal.Gui;
using Rpg;

namespace Server.TUI;

/// <summary>
/// Custom <see cref="IListDataSource"/> that renders log messages with per-line
/// foreground and background colours, mapping <see cref="ConsoleColor"/> values
/// to Terminal.Gui <see cref="Color"/> values.
/// </summary>
internal sealed class LogDataSource : IListDataSource
{
    private readonly List<(string Text, ConsoleColor Fg, ConsoleColor Bg)> _lines = [];

    public int Count => _lines.Count;

    public int Length => _lines.Count > 0 ? _lines.Max(l => l.Text.Length) : 0;

    /// <summary>Append a log message (potentially multi-line) to the data source.</summary>
    public void AddMessage(Logger.LogMessage msg)
    {
        string text = msg.Message ?? "";
        if (string.IsNullOrEmpty(text))
        {
            _lines.Add(("", msg.ForegroundColor, msg.BackgroundColor));
            return;
        }

        foreach (string segment in text.Split(["\r\n", "\n"], StringSplitOptions.None))
            _lines.Add((segment, msg.ForegroundColor, msg.BackgroundColor));
    }

    public void Render(
        ListView container,
        ConsoleDriver driver,
        bool selected,
        int item,
        int col,
        int line,
        int width,
        int start = 0)
    {
        if (item < 0 || item >= _lines.Count) return;

        var (text, fg, bg) = _lines[item];

        driver.SetAttribute(Terminal.Gui.Attribute.Make(MapColor(fg), MapColor(bg)));

        if (start > 0 && start < text.Length)
            text = text[start..];
        else if (start >= text.Length)
            text = "";

        text = text.Length > width ? text[..width] : text.PadRight(width);
        driver.AddStr(text);
    }

    public bool IsMarked(int item) => false;
    public void SetMark(int item, bool value) { }
    public IList ToList() => _lines.Select(l => (object)l.Text).ToList();

    private static Color MapColor(ConsoleColor cc) => cc switch
    {
        ConsoleColor.Black       => Color.Black,
        ConsoleColor.DarkBlue    => Color.Blue,
        ConsoleColor.DarkGreen   => Color.Green,
        ConsoleColor.DarkCyan    => Color.Cyan,
        ConsoleColor.DarkRed     => Color.Red,
        ConsoleColor.DarkMagenta => Color.Magenta,
        ConsoleColor.DarkYellow  => Color.Brown,
        ConsoleColor.Gray        => Color.Gray,
        ConsoleColor.DarkGray    => Color.DarkGray,
        ConsoleColor.Blue        => Color.BrightBlue,
        ConsoleColor.Green       => Color.BrightGreen,
        ConsoleColor.Cyan        => Color.BrightCyan,
        ConsoleColor.Red         => Color.BrightRed,
        ConsoleColor.Magenta     => Color.BrightMagenta,
        ConsoleColor.Yellow      => Color.BrightYellow,
        ConsoleColor.White       => Color.White,
        _                        => Color.White,
    };
}
