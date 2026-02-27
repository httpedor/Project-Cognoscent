using Terminal.Gui;
using Rpg;
using GameCommand = Server.Game.Command;

namespace Server.TUI;

/// <summary>
/// Terminal UI built on Terminal.Gui.  Provides tabbed log views (Console / Web),
/// an inline command input with autocomplete suggestions, and command history.
/// <para><c>Init()</c> is non-blocking — the UI runs on a dedicated background thread.</para>
/// </summary>
public static class TUI
{
    // ── Screens ──────────────────────────────────────────────────────────────
    private static readonly (string Name, Func<Logger> GetLogger)[] Screens =
    [
        ("Console", () => Loggers.Console),
        ("Web",     () => Loggers.Web),
    ];

    // ── Command history ──────────────────────────────────────────────────────
    private static readonly List<string> _history = [];
    private static int _historyIdx = -1;

    // ── Layout constants ─────────────────────────────────────────────────────
    private const int InputFrameHeight = 3;
    private const int SuggestionsFrameHeight = 7;

    // ── Widgets ──────────────────────────────────────────────────────────────
    private static TabView   _tabs             = null!;
    private static LogDataSource[] _logSources = null!;
    private static ListView[] _logListViews    = null!;
    private static FrameView _suggestionsFrame = null!;
    private static ListView  _suggestionsList  = null!;
    private static TextField _input            = null!;
    private static FrameView _inputFrame       = null!;

    // ═════════════════════════════════════════════════════════════════════════
    //  Public entry-point (non-blocking)
    // ═════════════════════════════════════════════════════════════════════════
    public static void Init()
    {
        var thread = new Thread(RunApplication)
        {
            IsBackground = true,
            Name = "TUI",
        };
        thread.Start();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Application bootstrap (runs on the TUI thread)
    // ═════════════════════════════════════════════════════════════════════════
    private static void RunApplication()
    {
        Application.Init();

        var top = Application.Top;

        BuildInputFrame();
        BuildSuggestionsFrame();
        BuildLogTabs();
        WireInputEvents();

        top.Add(_tabs, _suggestionsFrame, _inputFrame);
        top.Loaded += () => _input.SetFocus();

        Application.Run();
        Application.Shutdown();
    }

    // ─── Build: command input ────────────────────────────────────────────────
    private static void BuildInputFrame()
    {
        _inputFrame = new FrameView("Command")
        {
            X      = 0,
            Y      = Pos.AnchorEnd(InputFrameHeight),
            Width  = Dim.Fill(),
            Height = InputFrameHeight,
        };

        _input = new TextField("")
        {
            X     = 0,
            Y     = 0,
            Width = Dim.Fill(),
        };

        _inputFrame.Add(_input);
    }

    // ─── Build: suggestions panel ────────────────────────────────────────────
    private static void BuildSuggestionsFrame()
    {
        _suggestionsFrame = new FrameView("Suggestions")
        {
            X       = 0,
            Y       = Pos.AnchorEnd(InputFrameHeight + SuggestionsFrameHeight),
            Width   = Dim.Fill(),
            Height  = SuggestionsFrameHeight,
            Visible = false,
        };

        _suggestionsList = new ListView
        {
            X        = 0,
            Y        = 0,
            Width    = Dim.Fill(),
            Height   = Dim.Fill(),
            CanFocus = false,
        };

        _suggestionsFrame.CanFocus = false;
        _suggestionsFrame.Add(_suggestionsList);
    }

    // ─── Build: tabbed log views ─────────────────────────────────────────────
    private static void BuildLogTabs()
    {
        _tabs = new TabView
        {
            X        = 0,
            Y        = 0,
            Width    = Dim.Fill(),
            Height   = Dim.Fill(InputFrameHeight), // initially no suggestions
            CanFocus = false,
        };

        _logSources   = new LogDataSource[Screens.Length];
        _logListViews = new ListView[Screens.Length];

        for (int i = 0; i < Screens.Length; i++)
        {
            var source = new LogDataSource();
            _logSources[i] = source;

            var lv = new ListView(source)
            {
                X        = 0,
                Y        = 0,
                Width    = Dim.Fill(),
                Height   = Dim.Fill(),
                CanFocus = false,
            };
            _logListViews[i] = lv;

            _tabs.AddTab(new TabView.Tab(Screens[i].Name, lv), i == 0);

            // Seed with any logs that already exist (snapshot to avoid concurrent modification)
            foreach (var log in Screens[i].GetLogger().Logs.ToList())
                source.AddMessage(log);

            // Subscribe to future logs (thread-safe via MainLoop.Invoke)
            int idx = i;
            Screens[i].GetLogger().OnLogAdded += msg =>
            {
                Application.MainLoop?.Invoke(() =>
                {
                    _logSources[idx].AddMessage(msg);
                    var view = _logListViews[idx];
                    view.SetNeedsDisplay();

                    // Auto-scroll to the bottom
                    int count = _logSources[idx].Count;
                    if (count > 0)
                    {
                        view.SelectedItem = count - 1;
                        try
                        {
                            int visibleRows = Math.Max(1, view.Frame.Height);
                            int top = Math.Max(0, count - visibleRows);
                            if (top < count) // TopItem must be < Count
                                view.TopItem = top;
                        }
                        catch { /* layout not ready yet – SelectedItem alone is enough */ }
                    }
                });
            };
        }
    }

    // ─── Wire keyboard events on the input field ─────────────────────────────
    private static void WireInputEvents()
    {
        _input.TextChanged += _ => RefreshSuggestions();

        _input.KeyPress += args =>
        {
            bool handled = true;
            switch (args.KeyEvent.Key)
            {
                case Key.Enter:
                    ExecuteCurrentCommand();
                    break;

                case Key.Tab:
                    ApplySelectedSuggestion();
                    break;

                case Key.Esc:
                    _input.Text = "";
                    _historyIdx = -1;
                    SetSuggestionsVisible(false);
                    break;

                case Key.CursorUp:
                    if (_suggestionsFrame.Visible && _suggestionsList.Source?.Count > 0)
                    {
                        if (_suggestionsList.SelectedItem > 0)
                            _suggestionsList.SelectedItem--;
                        EnsureSuggestionVisible();
                    }
                    else
                        HistoryUp();
                    break;

                case Key.CursorDown:
                    if (_suggestionsFrame.Visible && _suggestionsList.Source?.Count > 0)
                    {
                        if (_suggestionsList.SelectedItem < _suggestionsList.Source.Count - 1)
                            _suggestionsList.SelectedItem++;
                        EnsureSuggestionVisible();
                    }
                    else
                        HistoryDown();
                    break;

                // Log scrolling
                case Key.PageUp:
                    ScrollActiveLog(-10);
                    break;
                case Key.PageDown:
                    ScrollActiveLog(+10);
                    break;

                // Tab switching
                case Key.PageUp | Key.CtrlMask:
                    SwitchTab(-1);
                    break;
                case Key.PageDown | Key.CtrlMask:
                    SwitchTab(+1);
                    break;

                default:
                    handled = false;
                    break;
            }
            args.Handled = handled;
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Suggestions
    // ═════════════════════════════════════════════════════════════════════════
    // The raw suggestion values (for tab-completion) and the display strings
    // (with descriptions appended) are kept in parallel lists.
    private static List<string> _rawSuggestions = [];

    private static void RefreshSuggestions()
    {
        string cmd = _input.Text?.ToString() ?? "";

        _rawSuggestions = string.IsNullOrEmpty(cmd)
            ? []
            : new List<string>(GameCommand.GetSuggestions(cmd));

        if (_rawSuggestions.Count > 0)
        {
            // Are we completing a command name? (first token, no space typed yet)
            bool isCommandName = !cmd.Contains(' ');

            var display = new List<string>(_rawSuggestions.Count);
            foreach (string s in _rawSuggestions)
            {
                if (isCommandName)
                {
                    string? desc = GameCommand.GetDescription(s);
                    display.Add(string.IsNullOrEmpty(desc) ? s : $"{s}  \u2014 {desc}");
                }
                else
                    display.Add(s);
            }

            _suggestionsList.SetSource(display);
            _suggestionsList.SelectedItem = 0;
            SetSuggestionsVisible(true);
        }
        else
        {
            SetSuggestionsVisible(false);
        }
    }

    private static void SetSuggestionsVisible(bool show)
    {
        if (_suggestionsFrame.Visible == show) return;

        _suggestionsFrame.Visible = show;
        _tabs.Height = show
            ? Dim.Fill(InputFrameHeight + SuggestionsFrameHeight)
            : Dim.Fill(InputFrameHeight);

        Application.Top.LayoutSubviews();
        Application.Top.SetNeedsDisplay();
    }

    /// <summary>Scrolls the suggestions ListView so the selected item is visible.</summary>
    private static void EnsureSuggestionVisible()
    {
        int sel = _suggestionsList.SelectedItem;
        int visibleRows = Math.Max(1, _suggestionsList.Frame.Height);

        try
        {
            if (sel < _suggestionsList.TopItem)
                _suggestionsList.TopItem = sel;
            else if (sel >= _suggestionsList.TopItem + visibleRows)
                _suggestionsList.TopItem = sel - visibleRows + 1;
        }
        catch { /* layout not ready */ }

        _suggestionsList.SetNeedsDisplay();
    }

    private static void ApplySelectedSuggestion()
    {
        if (!_suggestionsFrame.Visible || _rawSuggestions.Count == 0) return;

        int sel = _suggestionsList.SelectedItem;
        if (sel < 0 || sel >= _rawSuggestions.Count) return;

        string suggestion = _rawSuggestions[sel];
        if (string.IsNullOrEmpty(suggestion)) return;

        // Entity suggestions are "id:name" – only insert the id part
        int colonIdx = suggestion.IndexOf(':');
        if (colonIdx > 0)
            suggestion = suggestion[..colonIdx];

        string current = _input.Text?.ToString() ?? "";
        int lastSpace = current.LastIndexOf(' ');

        string next;
        if (lastSpace < 0)
        {
            next = suggestion;
            if (!next.EndsWith(' ')) next += ' ';
        }
        else if (current.EndsWith(' '))
            next = current + suggestion;
        else
            next = current[..(lastSpace + 1)] + suggestion;

        _input.Text = next;
        _input.CursorPosition = _input.Text.RuneCount;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Command execution
    // ═════════════════════════════════════════════════════════════════════════
    private static void ExecuteCurrentCommand()
    {
        string cmd = _input.Text?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(cmd))
        {
            if (_history.Count == 0 || _history[^1] != cmd)
                _history.Add(cmd);
            GameCommand.ExecuteCommand(null, cmd);
        }

        _input.Text = "";
        _historyIdx = -1;
        SetSuggestionsVisible(false);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Command history
    // ═════════════════════════════════════════════════════════════════════════
    private static void HistoryUp()
    {
        if (_history.Count == 0) return;
        if (_historyIdx < 0) _historyIdx = _history.Count;
        if (_historyIdx > 0) _historyIdx--;

        _input.Text = _history[_historyIdx];
        _input.CursorPosition = _input.Text.RuneCount;
    }

    private static void HistoryDown()
    {
        if (_historyIdx < 0) return;
        _historyIdx++;

        if (_historyIdx >= _history.Count)
        {
            _historyIdx = -1;
            _input.Text = "";
        }
        else
        {
            _input.Text = _history[_historyIdx];
            _input.CursorPosition = _input.Text.RuneCount;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Log / tab navigation helpers
    // ═════════════════════════════════════════════════════════════════════════
    private static int ActiveTabIndex()
    {
        var selected = _tabs.SelectedTab;
        if (selected is null) return 0;
        int idx = 0;
        foreach (var tab in _tabs.Tabs)
        {
            if (tab == selected) return idx;
            idx++;
        }
        return 0;
    }

    private static void SwitchTab(int delta)
    {
        int current = ActiveTabIndex();
        int next = Math.Clamp(current + delta, 0, Screens.Length - 1);
        if (next != current)
        {
            _tabs.SelectedTab = _tabs.Tabs.ElementAt(next);
            _tabs.SetNeedsDisplay();
        }
    }

    private static void ScrollActiveLog(int delta)
    {
        int idx = ActiveTabIndex();
        var view = _logListViews[idx];
        int count = _logSources[idx].Count;
        if (count == 0) return;

        int visibleRows = Math.Max(1, view.Frame.Height);
        int maxTop = Math.Max(0, count - visibleRows);
        int newTop = Math.Clamp(view.TopItem + delta, 0, maxTop);
        try { view.TopItem = newTop; } catch { }
        view.SetNeedsDisplay();
    }
}
