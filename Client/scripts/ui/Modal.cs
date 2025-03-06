using System;
using Godot;
using TTRpgClient.scripts;

public static class Modal
{
    public static void OpenConfirmationDialog(string title, string message, Action<bool> onSelect, string okText = "OK", string cancelText = "Cancelar")
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
            DialogText = message,
            DialogCloseOnEscape = true,
            DialogHideOnOk = true,
            OkButtonText = okText,
            CancelButtonText = cancelText
        };

        dialog.Confirmed += () => {
            onSelect(true);
            dialog.QueueFree();
        };
        dialog.Canceled += () => {
            onSelect(false);
            dialog.QueueFree();
        };

        GameManager.Instance.AddChild(dialog);
        dialog.PopupCentered();
    }
    public static void OpenAcceptDialog(string title, string message, Action? onAccept = null, string okText = "OK")
    {
        var dialog = new AcceptDialog
        {
            Title = title,
            DialogText = message,
            DialogCloseOnEscape = true,
            DialogHideOnOk = true,
            OkButtonText = okText
        };

        dialog.Confirmed += () => {
            if (onAccept != null)
                onAccept();
            dialog.QueueFree();
        };
        dialog.Canceled += () => {
            if (onAccept != null)
                onAccept();
            dialog.QueueFree();
        };

        GameManager.Instance.AddChild(dialog);
        dialog.PopupCentered();
    }

    public static void OpenOptionsDialog(string title, string message, string[] options, Action<string?> onSelect, bool cancelable = true)
    {
        var dialog = new AcceptDialog
        {
            Title = title,
            DialogText = message,
            DialogCloseOnEscape = cancelable,
            DialogHideOnOk = true,
        };

        dialog.GetOkButton().QueueFree();
        foreach (var option in options)
            dialog.AddButton(option, true, option);

        dialog.CustomAction += (name) => {
            onSelect(name);
            dialog.QueueFree();
        };
        dialog.Canceled += () => {
            onSelect(null);
            dialog.QueueFree();
        };

        GameManager.Instance.AddChild(dialog);
        dialog.PopupCentered();
    }

    public static void OpenStringDialog(string title, Action<string?> onSelect, bool cancelable = false)
    {
        var dialog = new AcceptDialog
        {
            Title = title,
            DialogCloseOnEscape = cancelable,
            DialogHideOnOk = true
        };
        var input = new TextEdit();
        dialog.AddChild(input);
        dialog.Canceled += () => {
            onSelect(null);
            dialog.QueueFree();
        };
        dialog.Confirmed += () => {
            onSelect(input.Text);
            input.QueueFree();
            dialog.QueueFree();
        };

        GameManager.Instance.AddChild(dialog);
        dialog.PopupCentered();
    }
}
