
using System;
using System.Collections.Generic;
using Godot;
using TTRpgClient.scripts;

public static class ContextMenu
{
    private static PopupMenu popupMenu;
    private static int addedSinceLastSep = 0;
    private static Dictionary<string, Action<Vector2>> callbacks = new();
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
            callbacks[popupMenu.GetItemText((Int32)index)](lastPos);
        };
        popupMenu.PopupHide += () => {
            callbacks.Clear();
            popupMenu.Clear(true);
        };
    }

    public static void AddOption(string title, Action<Vector2> action)
    {
        popupMenu.AddItem(title);
        addedSinceLastSep++;
        callbacks[title] = action;
    }
    public static void AddSeparator()
    {
        if (addedSinceLastSep == 0)
            return;
        popupMenu.AddSeparator();
    }

    public static void Show()
    {
        if (popupMenu.ItemCount == 0)
            return;
        lastPos = InputManager.Instance.MousePosition;
        var screenP = InputManager.Instance.GetGlobalMousePosition();
        popupMenu.Position = new Vector2I((Int32)screenP.X, (Int32)screenP.Y);
        if (popupMenu.GetParent() != null)
            popupMenu.Popup();
        else
            popupMenu.PopupExclusive(GameManager.Instance);
        addedSinceLastSep = 0;
    }

    public static void Hide()
    {
        popupMenu.Hide();
    }
}