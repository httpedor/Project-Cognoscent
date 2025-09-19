using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
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

    private static Control? GetControlFor<T>(T value, Action<object> callback)
    {
        switch (value)
        {
            case string s:
            {
                var te = new TextEdit
                {
                    Text = s
                };
                te.TextChanged += () => callback(te.Text);
                return te;
            }
            case string[] strings:
            {
                var ob = new OptionButton
                {
                    AllowReselect = true
                };
                for (int j = 0; j < strings.Length; j++)
                {
                    ob.AddItem(strings[j], j);
                }
                ob.ItemSelected += id => {
                    callback(strings[id]);
                };
                callback(strings[0]);
                return ob;
            }
            case float f:
            {
                var sb = new SpinBox
                {
                    Value = f
                };
                sb.ValueChanged += val =>
                {
                    callback((float)val);
                };
                return sb;
            }
            case int n:
            {
                var sb = new SpinBox
                {
                    Value = n,
                    Rounded = true
                };
                sb.ValueChanged += val =>
                {
                    callback(val);
                };
                return sb;
            }
            case Entity ent:
            {
                var ob = new OptionButton
                {
                    AllowReselect = true,
                };
                var ids = new List<Entity>();
                foreach (var board in GameManager.Instance.GetBoards())
                {
                    foreach (var entity in board.GetEntities())
                    {
                        ob.AddIconItem(board.GetEntityNode(entity).Display.Texture, $"{(entity is Creature c ? c.Name + " " : "")}{entity.Id} - {entity.GetEntityType()} at {entity.Position}, board {board.Name}, {entity.Id}");
                        ids.Add(entity);
                    }
                }
                ob.ItemSelected += id => {
                    callback(ids[(int)id]);
                };
                return ob;
            }
            case Enum e:
            {
                var ob = new OptionButton
                {
                    AllowReselect = true
                };
                var values = Enum.GetValues(e.GetType());
                ob.ItemSelected += id => {
                    callback(values.GetValue(id));
                };
                for (int j = 0; j < values.Length; j++)
                {
                    var val = values.GetValue(j);
                    ob.AddItem(val.ToString(), j);
                    if (Equals(val, e))
                        ob.Select(j);
                }
                return ob;
            }
            case Midia m:
            {
                var button = new Button
                {
                    Text = "Procurar Arquivo"
                };
                button.Pressed += () =>
                {
                    var fileD = new FileDialog
                    {
                        Access = FileDialog.AccessEnum.Filesystem,
                        FileMode = FileDialog.FileModeEnum.OpenFile,
                        UseNativeDialog = true
                    };
                    fileD.FileSelected += f =>
                    {
                        callback(new Midia(File.ReadAllBytes(f), f));
                        button.Text = f[(f.LastIndexOf('/') + 1)..];
                        fileD.QueueFree();
                    };
                    button.GetTree().Root.AddChild(fileD);
                    fileD.Popup();
                };
                return button;
            }
            case Vector2 vec:
            {
                // Container for two SpinBoxes side-by-side
                var hbox = new HBoxContainer();

                var sbX = new SpinBox
                {
                    Value = vec.X
                };
                var sbY = new SpinBox
                {
                    Value = vec.Y
                };

                // Update results whenever X changes
                sbX.ValueChanged += val =>
                {
                    callback(new Vector2((float)val, (float)sbY.Value));
                };

                // Update results whenever Y changes
                sbY.ValueChanged += val =>
                {
                    callback(new Vector2((float)sbX.Value, (float)val));
                };

                hbox.AddChild(sbX);
                hbox.AddChild(sbY);

                callback(vec);
                return hbox;
            }
            case Vector3 vec3:
            {
                // Container for three SpinBoxes side-by-side
                var hbox = new HBoxContainer();

                var sbX = new SpinBox
                {
                    Value = vec3.X
                };
                var sbY = new SpinBox
                {
                    Value = vec3.Y
                };
                var sbZ = new SpinBox
                {
                    Value = vec3.Z
                };

                // Update whenever X changes
                sbX.ValueChanged += val =>
                {
                    callback(new Vector3((float)val, (float)sbY.Value, (float)sbZ.Value));
                };

                // Update whenever Y changes
                sbY.ValueChanged += val =>
                {
                    callback(new Vector3((float)sbX.Value, (float)val, (float)sbZ.Value));
                };

                // Update whenever Z changes
                sbZ.ValueChanged += val =>
                {
                    callback(new Vector3((float)sbX.Value, (float)sbY.Value, (float)val));
                };

                hbox.AddChild(sbX);
                hbox.AddChild(sbY);
                hbox.AddChild(sbZ);

                callback(vec3);
                return hbox;
            }
            case DamageType dt:
            {
                var ob = new OptionButton
                {
                    AllowReselect = true
                };
                ob.ItemSelected += id => callback(DamageType.FromId((byte)id));
                
                var values = DamageType.All;
                int j = 0;
                foreach (var dtVal in values)
                {
                    ob.AddItem(dtVal.Name, dtVal.Id);
                    if (dtVal == dt)
                        ob.Select(j);
                    j++;
                }
                return ob;
            }
            default:
            {
                return null;
            }
        }
    }

    public static void OpenFormDialog<T>(string title, Action<T> callback, T startingValue)
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
        if (startingValue == null)
            return;

        int i = 1;
        const int height = 40;
        const int gap = 8;
        foreach (var field in startingValue.GetType().GetFields())
        {
            string key = field.Name;
            var attr = field.GetCustomAttribute<TupleElementNamesAttribute>();
            if (attr != null && attr.TransformNames.Count == i)
                key = attr.TransformNames[i-1]!;
            var container = new HBoxContainer
            {
                AnchorLeft = 0,
                AnchorRight = 1,
                AnchorBottom = 0,
                AnchorTop = 0,
                OffsetTop = (height * (i-1)) + (gap * (i-1)),
                OffsetBottom = (height * i) + (gap * i),
                Name = key,
            };
            var label = new Label
            {
                Text = key + ":"
            };

            object value = field.GetValue(startingValue)!;
            var input = GetControlFor(value, (result) => field.SetValue(startingValue, result));
            if (input == null)
                continue;
            
            if (value.GetType() != typeof(Midia))
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
            callback(startingValue);
            dialog.QueueFree();
        };
        dialog.AddChild(okBtn);

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
            var input = GetControlFor(pair.Value, (obj) => results[pair.Key] = obj);
            if (input == null)
                continue;
            
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
        switch (midia.Type)
        {
            case MidiaType.Video:
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
                break;
            }
            case MidiaType.Image:
            {
                var img = new Image();
                img.LoadPngFromBuffer(midia.Bytes);
                control = new TextureRect
                {
                    Texture = ImageTexture.CreateFromImage(img)
                };
                break;
            }
            default:
            {
                control = new Control()
                {
                    Visible = false
                };
                break;
            }
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

    public static void OpenFileDialog(Action<string[]> onSelect, FileDialog.FileModeEnum fileMode = FileDialog.FileModeEnum.OpenFile)
    {
        var fileD = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFiles,
            UseNativeDialog = true
        };
        GameManager.Instance.GetTree().Root.AddChild(fileD);
        fileD.DirSelected += (dir) =>
        {
            onSelect([dir]);
        };
        fileD.FileSelected += (file) =>
        {
            onSelect([file]);
        };
        fileD.FilesSelected += (files) =>
        {
            onSelect(files);
        };
        fileD.Popup();
    }

    public static async Task<string[]> OpenFileDialogAsync(
        FileDialog.FileModeEnum fileMode = FileDialog.FileModeEnum.OpenFile)
    {
        var fileD = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFiles,
            UseNativeDialog = true
        };
        GameManager.Instance.GetTree().Root.AddChild(fileD);
        fileD.Popup();
        string[] ret = [];
        switch (fileMode)
        {
            case FileDialog.FileModeEnum.OpenFile:
                ret = [(await GameManager.Instance.ToSignal(fileD, FileDialog.SignalName.FileSelected))[0].AsString()];
                break;
            case FileDialog.FileModeEnum.OpenDir:
                ret = [(await GameManager.Instance.ToSignal(fileD, FileDialog.SignalName.DirSelected))[0].AsString()];
                break;
            case FileDialog.FileModeEnum.OpenFiles:
                ret = (await GameManager.Instance.ToSignal(fileD, FileDialog.SignalName.FilesSelected))[0].AsStringArray();
                break;
        }

        return ret;
    }
    public static void OpenMultiline(string title, Action<string> onSave, string? startingText = null)
    {
        var dialog = new Window
        {
            Title = title,
            PopupWindow = true,
            Size = GameManager.Instance.GetWindow().Size/2
        };

        var input = new TextEdit
        {
            Text = startingText ?? ""
        };
        dialog.AddChild(input);

        dialog.CloseRequested += () =>
        {
            dialog.Hide();
            dialog.QueueFree();
            onSave(input.Text);
        };
        dialog.Ready += () =>
        {
            input.Size = dialog.Size;
        };
        dialog.SizeChanged += () =>
        {
            input.Size = dialog.Size;
        };

        GameManager.Instance.AddChild(dialog);
        dialog.PopupCentered();
    }

    public static void OpenTabs(string title, (string title, Control control)[] tabs, Action onClose)
    {
        var dialog = new Window
        {
            Title = title,
            PopupWindow = true,
            Size = GameManager.Instance.GetWindow().Size/2
        };
        dialog.CloseRequested += () => {
            dialog.Hide();
            dialog.QueueFree();
        };

        var tabContainer = new TabContainer();
        int i = 0;
        foreach (var tab in tabs)
        {
            tabContainer.AddChild(tab.control);
            tabContainer.SetTabTitle(i, tab.title);
            i++;
        }
        dialog.AddChild(tabContainer);

        dialog.CloseRequested += () =>
        {
            dialog.Hide();
            dialog.QueueFree();

            onClose();
        };
        dialog.Ready += () =>
        {
            tabContainer.Size = dialog.Size;
        };
        dialog.SizeChanged += () =>
        {
            tabContainer.Size = dialog.Size;
        };

        GameManager.Instance.AddChild(dialog);
        dialog.PopupCentered();
    }

    /*public static void OpenCode(string title, CodeEdit codeEdit, (string name, string? code)[] tabs, Action<(string name, string? code)[]>? onSave)
    {
        var dialog = new Window
        {
            Title = title,
            PopupWindow = true,
            Size = GameManager.Instance.GetWindow().Size/2
        };
        dialog.CloseRequested += () => {
            dialog.Hide();
            dialog.QueueFree();
        };

        var tabContainer = new TabContainer();
        int i = 0;
        foreach (var tab in tabs)
        {
            codeEdit.Name = tab.name;
            codeEdit.Text = tab.code;
            tabContainer.AddChild(codeEdit);
            tabContainer.SetTabTitle(i, tab.name.Capitalize());
            i++;
        }
        dialog.AddChild(tabContainer);

        dialog.CloseRequested += () =>
        {
            dialog.Hide();
            dialog.QueueFree();

            if (onSave == null)
                return;
            
            (string name, string? code)[] tabResults = new (string, string?)[tabContainer.GetChildCount()];
            int i = 0;
            foreach (var tab in tabContainer.GetChildren())
            {
                CodeEdit code = (tab as CodeEdit)!;
                if (code.Text == "")
                    tabResults[i] = (tab.Name.ToString(), null);
                else
                    tabResults[i] = (tab.Name.ToString(), code.Text);
                i++;
            }

            onSave(tabResults);
        };
        dialog.Ready += () =>
        {
            tabContainer.Size = dialog.Size;
        };
        dialog.SizeChanged += () =>
        {
            tabContainer.Size = dialog.Size;
        };

        GameManager.Instance.AddChild(dialog);
        dialog.PopupCentered();
    }*/
}
