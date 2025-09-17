
using System;
using System.Collections.Generic;
using Godot;
using TTRpgClient.scripts;

public static class ContextMenu
{
    private static PopupMenu popupMenu;
    private static int addedSinceLastSep = 0;
    private static Dictionary<string, (Action<Vector2> action, bool autoHide)> callbacks = new();
    private static Vector2 lastPos;

    public static bool IsOpen => popupMenu.Visible;

    static ContextMenu()
    {
        popupMenu = new PopupMenu
        {
            Visible = false,
            HideOnItemSelection = false,
            HideOnCheckableItemSelection = false,
            HideOnStateItemSelection = false,
        };
        popupMenu.IndexPressed += (index) =>
        {
            var cb = callbacks[popupMenu.GetItemText((Int32)index)];
            cb.action(lastPos);
            if (cb.autoHide)
                Hide();
        };
        popupMenu.PopupHide += () => {
            callbacks.Clear();
            popupMenu.Clear(true);
        };
    }

    public static void AddOption(string title, Action<Vector2> action, bool autoHide = true)
    {
        popupMenu.AddItem(title);
        addedSinceLastSep++;
        callbacks[title] = (action, autoHide);

    }
    public static void AddSeparator()
    {
        if (addedSinceLastSep == 0)
            return;
        popupMenu.AddSeparator();
        addedSinceLastSep = 0;
    }

    public static void Show()
    {
        if (popupMenu.ItemCount == 0)
            return;
        lastPos = InputManager.Instance.MousePosition;
        var screenP = InputManager.Instance.GetGlobalMousePosition();
        popupMenu.Position = new Vector2I((Int32)screenP.X, (Int32)screenP.Y);
        popupMenu.ResetSize();
        if (popupMenu.GetParent() != null)
            popupMenu.Popup();
        else
            popupMenu.PopupExclusive(GameManager.Instance);
        addedSinceLastSep = 0;
    }

    public static void Hide()
    {
        popupMenu.Hide();
        addedSinceLastSep = 0;
    }
}