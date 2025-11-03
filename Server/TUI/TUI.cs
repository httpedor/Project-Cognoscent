using ConsoleRenderer;
using Rpg;
using Server.Game;

namespace Server.TUI;

public static class TUI
{
    // UI configuration constants
    private static class Ui
    {
        public const int MaxSuggestionRows = 5;
        public const int InputBoxMaxHeight = 3;
        public const int BorderPaddingX = 2;
        public const int SuggestionHeaderHeight = 1;
        public const int BorderChromeHeight = 2; // top title row + bottom border
    }

    // State
    private static readonly ConsoleCanvas canvas = new(false, true);
    private static bool typingCommand;
    private static string currentCommand = string.Empty;
    private static string lastTypedCommand = string.Empty;

    // Suggestions state
    private static readonly object suggestionsLock = new();
    private static List<string> currentSuggestions = new();
    private static int currentSuggestionIndex = -1; // -1 = none selected

    // Logs state
    private static int logScroll; // 0 = bottom (newest). Positive = scrolled up (older logs)
    private static readonly object scrollLock = new();
    // Which log screen is active: 0 = Console Logs, 1 = Web Logs
    private static int currentLogScreen = 0;
    private static readonly object screenLock = new();

    // Suggestions lifecycle ----------------------------------------------------
    private static void UpdateSuggestions()
    {
        List<string> nextSuggestions;
        if (typingCommand)
        {
            var suggestions = Command.GetSuggestions(currentCommand);
            nextSuggestions = new List<string>(suggestions);
        }
        else
        {
            nextSuggestions = new List<string>();
        }

        lock (suggestionsLock)
        {
            currentSuggestions = nextSuggestions;
            // Adjust selection index based on availability and input text
            if (currentSuggestions.Count == 0 || string.IsNullOrEmpty(currentCommand))
            {
                currentSuggestionIndex = -1;
            }
            else
            {
                if (currentSuggestionIndex < 0)
                    currentSuggestionIndex = 0;
                if (currentSuggestionIndex >= currentSuggestions.Count)
                    currentSuggestionIndex = currentSuggestions.Count - 1;
            }
        }
    }

    private static void SetCommand(string cmd)
    {
        currentCommand = cmd ?? string.Empty;
        UpdateSuggestions();
    }

    private static void ApplySuggestion()
    {
        List<string> snapshot;
        int selectedIndex;
        lock (suggestionsLock)
        {
            snapshot = new List<string>(currentSuggestions);
            selectedIndex = currentSuggestionIndex;
        }

        if (snapshot.Count == 0)
            return;

        if (selectedIndex < 0 || selectedIndex >= snapshot.Count)
            selectedIndex = 0;

        string suggestion = snapshot[selectedIndex];
        if (string.IsNullOrEmpty(suggestion))
            return;

        int lastSpace = currentCommand.LastIndexOf(' ');
        string next = currentCommand;
        if (lastSpace < 0)
        {
            next = suggestion;
            if (!next.EndsWith(' '))
                next += ' ';
        }
        else if (currentCommand.EndsWith(' '))
        {
            next = currentCommand + suggestion;
        }
        else
        {
            next = currentCommand[..(lastSpace + 1)] + suggestion;
        }

        SetCommand(next);
    }

    private static void NavigateSuggestions(int delta)
    {
        lock (suggestionsLock)
        {
            if (currentSuggestions.Count == 0)
            {
                currentSuggestionIndex = -1;
                return;
            }

            if (currentSuggestionIndex < 0)
                currentSuggestionIndex = 0;
            else
                currentSuggestionIndex = Math.Clamp(currentSuggestionIndex + delta, 0, currentSuggestions.Count - 1);
        }
    }

    public static void Init()
    {
        Console.CursorVisible = false;
        Loggers.Console.OnLogAdded += (_) => Render();
        Loggers.Web.OnLogAdded += (_) => Render();
        Task.Run(() =>
        {
            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                if (typingCommand)
                {
                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Escape:
                            typingCommand = false;
                            SetCommand("");
                            break;
                        case ConsoleKey.Enter:
                            typingCommand = false;
                            // Execute and remember last typed command
                            Command.ExecuteCommand(null, currentCommand);
                            lastTypedCommand = currentCommand;
                            SetCommand("");
                            break;
                        case ConsoleKey.Backspace:
                            if (currentCommand.Length > 0)
                                SetCommand(currentCommand.Substring(0, currentCommand.Length - 1));
                            
                            break;
                        case ConsoleKey.Tab:
                            ApplySuggestion();
                            break;
                        case ConsoleKey.UpArrow:
                            // When input is empty, recall last typed command.
                            // Otherwise, navigate suggestions if available.
                            if (string.IsNullOrEmpty(currentCommand))
                            {
                                if (!string.IsNullOrEmpty(lastTypedCommand))
                                    SetCommand(lastTypedCommand);
                            }
                            else
                            {
                                NavigateSuggestions(-1);
                            }
                            break;
                        case ConsoleKey.DownArrow:
                            // When input is empty, keep it empty; otherwise navigate suggestions.
                            if (string.IsNullOrEmpty(currentCommand))
                            {
                                SetCommand("");
                            }
                            else
                            {
                                NavigateSuggestions(+1);
                            }
                            break;
                        default:
                            if (!char.IsControl(keyInfo.KeyChar))
                            {
                                SetCommand(currentCommand + keyInfo.KeyChar);
                            }
                            break;
                    }
                }
                else
                {
                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        typingCommand = true;
                        SetCommand("");
                    }
                    else
                    {
                        // Scroll logs with Up/Down arrows or K/J (vim-like)
                        // Switch screens with Left/Right or H/L
                        switch (keyInfo.Key)
                        {
                            case ConsoleKey.UpArrow:
                            case ConsoleKey.K:
                                lock (scrollLock)
                                {
                                    logScroll++;
                                }
                                break;
                            case ConsoleKey.DownArrow:
                            case ConsoleKey.J:
                                lock (scrollLock)
                                {
                                    if (logScroll > 0) logScroll--;
                                }
                                break;
                            case ConsoleKey.LeftArrow:
                            case ConsoleKey.H:
                                lock (screenLock)
                                {
                                    currentLogScreen = Math.Max(0, currentLogScreen - 1);
                                }
                                break;
                            case ConsoleKey.RightArrow:
                            case ConsoleKey.L:
                                lock (screenLock)
                                {
                                    currentLogScreen = Math.Min(1, currentLogScreen + 1);
                                }
                                break;
                        }
                    }
                }
                Render();
            }
        });
    }


    // Rendering ---------------------------------------------------------------
    private static void Render()
    {
        Task.Run(() =>
        {
            Console.CursorVisible = typingCommand;
            lock (canvas)
            {
                canvas.Clear();
                canvas.Clear();

                // Text Input dimensions
                int inputHeight = Math.Min(Ui.InputBoxMaxHeight, canvas.Height);
                int inputTop = Math.Max(0, canvas.Height - inputHeight);

                List<string> suggestions;
                int selectedIdx;
                lock (suggestionsLock)
                {
                    suggestions = new List<string>(currentSuggestions);
                    selectedIdx = currentSuggestionIndex;
                }

                int suggestionRows = 0;
                int suggestionBoxHeight = 0;
                if (suggestions.Count > 0 && inputTop >= 3)
                {
                    suggestionRows = Math.Min(Ui.MaxSuggestionRows, suggestions.Count);
                    suggestionBoxHeight = suggestionRows + Ui.BorderChromeHeight;
                    if (suggestionBoxHeight > inputTop)
                    {
                        suggestionRows = Math.Max(0, inputTop - Ui.BorderChromeHeight);
                        suggestionBoxHeight = suggestionRows > 0 ? suggestionRows + Ui.BorderChromeHeight : 0;
                    }
                }

                int suggestionTop = inputTop - suggestionBoxHeight;
                if (suggestionTop < 0)
                {
                    suggestionBoxHeight = 0;
                    suggestionRows = 0;
                    suggestionTop = 0;
                }

                RenderSuggestions(suggestionTop, suggestionBoxHeight, suggestionRows, suggestions, selectedIdx);

                RenderInput(inputTop, inputHeight);
                
                // Render only the active log screen as a full-width area
                int active;
                lock (screenLock)
                {
                    active = currentLogScreen;
                }
                if (active == 0)
                    RenderLogsFull(suggestionTop);
                else
                    RenderWebLogsFull(suggestionTop);

                // The double render appears intentional (double buffering / flicker mitigation)
                canvas.Render();
                canvas.Render();
            }
        });
    }

    private static void RenderSuggestions(int suggestionTop, int suggestionBoxHeight, int suggestionRows, List<string> suggestions, int selectedIdx)
    {
        if (suggestionBoxHeight <= 0) return;

        canvas.CreateBorder(0, suggestionTop, canvas.Width, suggestionBoxHeight);
        canvas.Text(Ui.BorderPaddingX, suggestionTop, "Suggestions");
        for (int i = 0; i < suggestionRows; i++)
        {
            string prefix = (i == selectedIdx) ? "> " : "  ";
            canvas.Text(Ui.BorderPaddingX, suggestionTop + Ui.SuggestionHeaderHeight + i, prefix + suggestions[i]);
        }
    }

    private static void RenderInput(int inputTop, int inputHeight)
    {
        canvas.CreateBorder(0, inputTop, canvas.Width, inputHeight);
        canvas.Text(Ui.BorderPaddingX, inputTop, "Command Input");

        int commandLineY = canvas.Height - 2;
        if (typingCommand)
        {
            canvas.Text(Ui.BorderPaddingX, commandLineY, currentCommand);
            Console.SetCursorPosition(Ui.BorderPaddingX + currentCommand.Length, commandLineY);
        }
        else
        {
            canvas.Text(Ui.BorderPaddingX, commandLineY, "Press Enter to type a command...");
        }
    }

    private static void RenderLogs(int suggestionTop)
    {
        // Logs area occupies left 2/3 of the screen, above suggestions/input
        int logAreaHeight = Math.Max(1, suggestionTop);
        int logAreaWidth = (canvas.Width / 3) * 2;
        canvas.CreateBorder(0, 0, logAreaWidth, logAreaHeight);
        canvas.Text(Ui.BorderPaddingX, 0, "Console Logs");

        var consoleLogs = Loggers.Console.Logs;
        int displayRows = Math.Max(0, logAreaHeight - Ui.BorderChromeHeight);

        // Expand logs into individual display lines (preserve colors per log)
        var renderedLines = new List<(string text, ConsoleColor fg, ConsoleColor bg)>();
        foreach (var log in consoleLogs)
        {
            if (string.IsNullOrEmpty(log.Message))
            {
                renderedLines.Add((string.Empty, log.ForegroundColor, log.BackgroundColor));
                continue;
            }
            var lines = log.Message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var l in lines)
                renderedLines.Add((l, log.ForegroundColor, log.BackgroundColor));
        }

        // Apply scroll offset (logScroll) so user can view older/newer messages.
        int scrollSnapshot;
        lock (scrollLock)
        {
            // Clamp scroll to available range
            int maxScroll = Math.Max(0, renderedLines.Count - displayRows);
            if (logScroll > maxScroll) logScroll = maxScroll;
            scrollSnapshot = logScroll;
        }

        int lineStart = Math.Max(0, renderedLines.Count - displayRows - scrollSnapshot);
        for (int i = 0; i < displayRows; i++)
        {
            int idx = lineStart + i;
            if (idx < renderedLines.Count)
            {
                var entry = renderedLines[idx];
                canvas.Text(Ui.BorderPaddingX, 1 + i, entry.text, false, entry.fg, entry.bg);
            }
        }
    }

    private static void RenderLogsFull(int suggestionTop)
    {
        int logAreaHeight = Math.Max(1, suggestionTop);
        int logAreaWidth = canvas.Width;
        canvas.CreateBorder(0, 0, logAreaWidth, logAreaHeight);
        canvas.Text(Ui.BorderPaddingX, 0, "Console Logs (press H/Left and L/Right to switch)");

        var consoleLogs = Loggers.Console.Logs;
        int displayRows = Math.Max(0, logAreaHeight - Ui.BorderChromeHeight);

        var renderedLines = new List<(string text, ConsoleColor fg, ConsoleColor bg)>();
        foreach (var log in consoleLogs)
        {
            if (string.IsNullOrEmpty(log.Message))
            {
                renderedLines.Add((string.Empty, log.ForegroundColor, log.BackgroundColor));
                continue;
            }
            var lines = log.Message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var l in lines)
                renderedLines.Add((l, log.ForegroundColor, log.BackgroundColor));
        }

        int scrollSnapshot;
        lock (scrollLock)
        {
            int maxScroll = Math.Max(0, renderedLines.Count - displayRows);
            if (logScroll > maxScroll) logScroll = maxScroll;
            scrollSnapshot = logScroll;
        }

        int lineStart = Math.Max(0, renderedLines.Count - displayRows - scrollSnapshot);
        for (int i = 0; i < displayRows; i++)
        {
            int idx = lineStart + i;
            if (idx < renderedLines.Count)
            {
                var entry = renderedLines[idx];
                canvas.Text(Ui.BorderPaddingX, 1 + i, entry.text, false, entry.fg, entry.bg);
            }
        }
    }

    private static void RenderWebLogs(int suggestionTop)
    {
        // Right 1/3 area of the screen dedicated to web logs, same height as logs
        int logAreaHeight = Math.Max(1, suggestionTop);
        int fullWidth = canvas.Width;
        int leftWidth = (fullWidth / 3) * 2;
        int webAreaWidth = fullWidth - leftWidth;
        int webAreaLeft = leftWidth;

        if (webAreaWidth <= 0)
            return;

        canvas.CreateBorder(webAreaLeft, 0, webAreaWidth, logAreaHeight);
        canvas.Text(webAreaLeft + Ui.BorderPaddingX, 0, "Web Logs");

        var webLogs = Loggers.Web.Logs;
        int displayRows = Math.Max(0, logAreaHeight - Ui.BorderChromeHeight);

        var renderedLines = new List<(string text, ConsoleColor fg, ConsoleColor bg)>();
        foreach (var log in webLogs)
        {
            if (string.IsNullOrEmpty(log.Message))
            {
                renderedLines.Add((string.Empty, log.ForegroundColor, log.BackgroundColor));
                continue;
            }
            var lines = log.Message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var l in lines)
                renderedLines.Add((l, log.ForegroundColor, log.BackgroundColor));
        }

        // No independent scrolling for web logs yet - sync to same scroll offset as console logs
        int scrollSnapshot;
        lock (scrollLock)
        {
            int maxScroll = Math.Max(0, renderedLines.Count - displayRows);
            if (logScroll > maxScroll) logScroll = maxScroll;
            scrollSnapshot = logScroll;
        }

        int lineStart = Math.Max(0, renderedLines.Count - displayRows - scrollSnapshot);
        for (int i = 0; i < displayRows; i++)
        {
            int idx = lineStart + i;
            if (idx < renderedLines.Count)
            {
                var entry = renderedLines[idx];
                canvas.Text(webAreaLeft + Ui.BorderPaddingX, 1 + i, entry.text, false, entry.fg, entry.bg);
            }
        }
    }

    private static void RenderWebLogsFull(int suggestionTop)
    {
        int logAreaHeight = Math.Max(1, suggestionTop);
        int logAreaWidth = canvas.Width;
        canvas.CreateBorder(0, 0, logAreaWidth, logAreaHeight);
        canvas.Text(Ui.BorderPaddingX, 0, "Web Logs (press H/Left and L/Right to switch)");

        var webLogs = Loggers.Web.Logs;
        int displayRows = Math.Max(0, logAreaHeight - Ui.BorderChromeHeight);

        var renderedLines = new List<(string text, ConsoleColor fg, ConsoleColor bg)>();
        var cloned = new List<Logger.LogMessage>(webLogs);
        foreach (var log in cloned)
        {
            if (string.IsNullOrEmpty(log.Message))
            {
                renderedLines.Add((string.Empty, log.ForegroundColor, log.BackgroundColor));
                continue;
            }
            var lines = log.Message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var l in lines)
                renderedLines.Add((l, log.ForegroundColor, log.BackgroundColor));
        }

        int scrollSnapshot;
        lock (scrollLock)
        {
            int maxScroll = Math.Max(0, renderedLines.Count - displayRows);
            if (logScroll > maxScroll) logScroll = maxScroll;
            scrollSnapshot = logScroll;
        }

        int lineStart = Math.Max(0, renderedLines.Count - displayRows - scrollSnapshot);
        for (int i = 0; i < displayRows; i++)
        {
            int idx = lineStart + i;
            if (idx < renderedLines.Count)
            {
                var entry = renderedLines[idx];
                canvas.Text(Ui.BorderPaddingX, 1 + i, entry.text, false, entry.fg, entry.bg);
            }
        }
    }
}
