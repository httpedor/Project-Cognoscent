using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Godot;
using Rpg;
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
        foreach (string option in options)
            dialog.AddButton(option, true, option);

        dialog.CustomAction += name => {
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

    public static void OpenFormDialog(string title, Action<Dictionary<string, object>> callback, params (string Key, object Value, Predicate<object>? Predicate)[] inputs)
    {
        var dialog = new Window
        {
            Title = title,
            Transient = true,
            Exclusive = true,
            AlwaysOnTop = true,
            PopupWindow = true,
            Size = GameManager.Instance.GetWindow().Size/2
        };
        dialog.CloseRequested += () => {
            dialog.Hide();
            dialog.QueueFree();
        };
        int i = 1;
        const int height = 40;
        const int gap = 8;
        Dictionary<string, object> results = new();
        foreach (var pair in inputs)
        {
            results[pair.Key] = pair.Value;
            var container = new HBoxContainer
            {
                AnchorLeft = 0,
                AnchorRight = 1,
                AnchorBottom = 0,
                AnchorTop = 0,
                OffsetTop = (height * (i-1)) + (gap * (i-1)),
                OffsetBottom = (height * i) + (gap * i),
                Name = pair.Key,
            };
            var label = new Label
            {
                Text = pair.Key + ":"
            };
            Control input;
            if (pair.Value is string s)
            {
                string oldText = s;
                input = new TextEdit
                {
                    Text = s
                };
                var te = input as TextEdit;
                te.TextChanged += () =>
                {
                    if (pair.Predicate != null && !pair.Predicate(te.Text))
                    {
                        te.Text = oldText;
                    }
                    else
                    {
                        oldText = te.Text;
                        results[pair.Key] = oldText;
                    }
                };
            }
            else if (pair.Value is string[] strings)
            {
                input = new OptionButton
                {
                    AllowReselect = true
                };
                var ob = (OptionButton)input;
                for (int j = 0; j < strings.Length; j++)
                {
                    ob.AddItem(strings[j], j);
                }
                ob.ItemSelected += id => {
                    results[pair.Key] = strings[id];
                };
                results[pair.Key] = strings[0];
            }
            else if (pair.Value is float f)
            {
                input = new SpinBox
                {
                    Value = f
                };
                var sb = input as SpinBox;
                sb.ValueChanged += val =>
                {
                    results[pair.Key] = val;
                };
            }
            else if (pair.Value is int n)
            {
                input = new SpinBox
                {
                    Value = n,
                    Rounded = true
                };
                var sb = input as SpinBox;
                sb.ValueChanged += val =>
                {
                    results[pair.Key] = val;
                };
            }
            else if (pair.Value is Entity)
            {
                input = new OptionButton
                {
                    AllowReselect = true,
                };
                var ob = input as OptionButton;
                foreach (var board in GameManager.Instance.GetBoards())
                {
                    foreach (var entity in board.GetEntities())
                    {
                        if (pair.Predicate == null || pair.Predicate(entity))
                            ob.AddIconItem(board.GetEntityNode(entity).Display.Texture, $"{(entity is Creature c ? c.Name + " " : "")}{entity.Id} - {entity.GetEntityType()} at {entity.Position}, board {board.Name}, {entity.Id}");
                    }
                }
                ob.ItemSelected += id => {
                    results[pair.Key] = id;
                };
            }
            else if (pair.Value is Enum e)
            {
                input = new OptionButton
                {
                    AllowReselect = true
                };
                var ob = input as OptionButton;
                var values = Enum.GetValues(e.GetType());
                for (int j = 0; j < values.Length; j++)
                {
                    ob.AddItem(values.GetValue(j).ToString(), j);
                }
                ob.ItemSelected += id => {
                    results[pair.Key] = values.GetValue(id);
                };
            }
            else if (pair.Value is Midia)
            {
                input = new Button
                {
                    Text = "Procurar Arquivo"
                };
                (input as Button).Pressed += () => 
                {
                    var fileD = new FileDialog
                    {
                        Access = FileDialog.AccessEnum.Filesystem,
                        FileMode = FileDialog.FileModeEnum.OpenFile,
                        UseNativeDialog = true
                    };
                    fileD.FileSelected += f => {
                        results[pair.Key] = new Midia(File.ReadAllBytes(f), f);
                        (input as Button)!.Text = f[(f.LastIndexOf('/')+1)..];
                        fileD.QueueFree();
                    };
                    input.GetTree().Root.AddChild(fileD);
                    fileD.Popup();
                };
            }
            else
            {
                continue;
            }
            if (pair.Value.GetType() != typeof(byte[]))
                input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            else
                input.Size = new Vector2(64, 64);

            container.AddChild(label);
            container.AddChild(input);
            dialog.AddChild(container);
            i++;
        }

        var okBtn = new Button
        {
            Text = "Ok",
            GrowVertical = Control.GrowDirection.Begin,
        };
        okBtn.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        okBtn.Pressed += () => {
            callback(results);
            dialog.QueueFree();
        };
        dialog.AddChild(okBtn);

        GameManager.Instance.AddChild(dialog);
        dialog.PopupCentered();
    }

    public static void OpenMedia(Midia midia, string? title = null, string? desc = null)
    {
        var dialog = new Window
        {
            Title = title == null ? "" : title,
            Transient = true,
            Exclusive = true,
            AlwaysOnTop = true,
            PopupWindow = true,
            Size = GameManager.Instance.GetWindow().Size/2
        };
            
        Control control;
        if (midia.IsVideo)
        {
            control = new VideoStreamPlayer();
            string filePath = Path.Combine(OS.GetCacheDir(), "Temp", "modal_midia");
            using (var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write))
            {
                file.StoreBuffer(midia.Bytes);
            }

            var videoStream = ResourceLoader.Load<VideoStream>("res://assets/ffmpeg.tres");
            videoStream.File = filePath;
            (control as VideoStreamPlayer).Stream = videoStream;
        }
        else
        {
            var img = new Image();
            img.LoadPngFromBuffer(midia.Bytes);
            control = new TextureRect
            {
                Texture = ImageTexture.CreateFromImage(img)
            };
        }
        control.AnchorLeft = 0.1f;
        control.AnchorRight = 0.9f;
        control.AnchorBottom = 0.9f;
        control.AnchorTop = 0.1f;
        
        //TODO: Title and description

        dialog.AddChild(control);
        GameManager.Instance.AddChild(dialog);
        dialog.PopupCentered();
    }
}
