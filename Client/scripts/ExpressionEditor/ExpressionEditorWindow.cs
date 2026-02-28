using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

/// <summary>
/// A window that hosts an ExprNodeEditor for visual expression editing.
/// Can be opened via Modal.OpenExpressionEditor or instantiated directly.
/// </summary>
public partial class ExpressionEditorWindow : Window
{
    private ExprNodeEditor _editor;
    private Label _jsonPreview;
    private string _outputType;
    private Action<JsonElement?>? _onSave;

    /// <summary>
    /// Info about a single expression field to edit.
    /// </summary>
    public class ExprFieldInfo
    {
        public required string JsonKey;
        public required string DisplayName;
        public required string ExprType; // "number", "bool", "effect", "selector", "string"
    }

    public ExpressionEditorWindow()
    {
        _editor = new ExprNodeEditor();
        _jsonPreview = new Label();
        _outputType = "number";
    }

    /// <summary>
    /// Opens a single-expression editor.
    /// </summary>
    public static ExpressionEditorWindow Open(string title, string exprType, JsonElement? existingJson, Action<JsonElement?> onSave)
    {
        var window = new ExpressionEditorWindow();
        window.Title = $"Expression Editor - {title}";
        window._outputType = exprType;
        window._onSave = onSave;
        window.PopupWindow = true;
        window.Size = new Vector2I(1000, 600);

        window.Ready += () =>
        {
            window.SetupUI();
            window._editor.CreateOutputNode(exprType);

            // Load existing expression
            if (existingJson.HasValue)
            {
                ExprNodeSerializer.DeserializeToGraph(
                    window._editor, existingJson.Value,
                    new Vector2(300, 200), exprType);
                
                // Try auto-connecting the deserialized root to output
                window.AutoConnectToOutput();
            }

            window.UpdatePreview();
        };

        GameManager.Instance.AddChild(window);
        window.PopupCentered();
        return window;
    }

    /// <summary>
    /// Opens a multi-field expression editor with tabs for each expression field.
    /// </summary>
    public static void OpenMultiField(string title, ExprFieldInfo[] fields, JsonElement sourceJson, Action<JsonObject> onSave)
    {
        var window = new Window
        {
            Title = $"Expression Editor - {title}",
            PopupWindow = true,
            Size = new Vector2I(1100, 700)
        };

        var tabContainer = new TabContainer
        {
            AnchorsPreset = (int)Control.LayoutPreset.FullRect
        };

        var editors = new (ExprNodeEditor editor, ExprFieldInfo field)[fields.Length];

        window.Ready += () =>
        {
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var container = new VBoxContainer { Name = field.DisplayName };

                var editor = new ExprNodeEditor
                {
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(0, 500)
                };

                container.AddChild(editor);
                tabContainer.AddChild(container);
                tabContainer.SetTabTitle(i, field.DisplayName);

                editors[i] = (editor, field);

                // Deferred setup: create output node and load data
                editor.Ready += () =>
                {
                    editor.CreateOutputNode(field.ExprType);

                    if (sourceJson.TryGetProperty(field.JsonKey, out var existing))
                    {
                        ExprNodeSerializer.DeserializeToGraph(editor, existing, new Vector2(300, 200), field.ExprType);
                        // Auto-connect root node to output
                        AutoConnectRootToOutput(editor);
                    }
                };
            }

            window.AddChild(tabContainer);

            // Toolbar at bottom
            var toolbar = new HBoxContainer();
            toolbar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); // spacer

            var saveBtn = new Button { Text = "Save & Close" };
            saveBtn.Pressed += () =>
            {
                var result = sourceJson.ToNode()!.AsObject();
                foreach (var (editor, field) in editors)
                {
                    var json = ExprNodeSerializer.SerializeGraph(editor);
                    if (json.HasValue)
                        result[field.JsonKey] = JsonNode.Parse(json.Value.GetRawText());
                    else
                        result.Remove(field.JsonKey);
                }
                onSave(result);
                window.Hide();
                window.QueueFree();
            };
            toolbar.AddChild(saveBtn);

            var cancelBtn = new Button { Text = "Cancel" };
            cancelBtn.Pressed += () =>
            {
                window.Hide();
                window.QueueFree();
            };
            toolbar.AddChild(cancelBtn);

            // Add toolbar into a top-level VBox
            var mainLayout = new VBoxContainer { AnchorsPreset = (int)Control.LayoutPreset.FullRect };
            window.RemoveChild(tabContainer);
            mainLayout.AddChild(tabContainer);
            tabContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainLayout.AddChild(toolbar);
            window.AddChild(mainLayout);
        };

        window.CloseRequested += () =>
        {
            window.Hide();
            window.QueueFree();
        };

        GameManager.Instance.AddChild(window);
        window.PopupCentered();
    }

    private void SetupUI()
    {
        var mainLayout = new VBoxContainer
        {
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
        };

        // Top toolbar
        var toolbar = new HBoxContainer();
        var clearBtn = new Button { Text = "Clear" };
        clearBtn.Pressed += () =>
        {
            _editor.ClearGraph();
            _editor.CreateOutputNode(_outputType);
            UpdatePreview();
        };
        toolbar.AddChild(clearBtn);

        toolbar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); // spacer

        var saveBtn = new Button { Text = "Save & Close" };
        saveBtn.Pressed += () =>
        {
            var json = ExprNodeSerializer.SerializeGraph(_editor);
            _onSave?.Invoke(json);
            Hide();
            QueueFree();
        };
        toolbar.AddChild(saveBtn);

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.Pressed += () =>
        {
            _onSave?.Invoke(null);
            Hide();
            QueueFree();
        };
        toolbar.AddChild(cancelBtn);

        mainLayout.AddChild(toolbar);

        // Graph editor
        _editor.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _editor.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _editor.CustomMinimumSize = new Vector2(0, 400);
        _editor.OnGraphChanged += UpdatePreview;
        mainLayout.AddChild(_editor);

        // JSON preview
        var previewBar = new HBoxContainer();
        previewBar.AddChild(new Label { Text = "JSON: " });
        _jsonPreview = new Label
        {
            Text = "(no output connected)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 30)
        };
        var previewScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 60),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        previewScroll.AddChild(_jsonPreview);
        previewBar.AddChild(previewScroll);
        mainLayout.AddChild(previewBar);

        AddChild(mainLayout);

        CloseRequested += () =>
        {
            Hide();
            QueueFree();
        };
        SizeChanged += () =>
        {
            mainLayout.Size = Size;
        };
        mainLayout.Size = Size;
    }

    private void UpdatePreview()
    {
        try
        {
            var json = ExprNodeSerializer.SerializeGraph(_editor);
            if (json.HasValue)
            {
                string text = JsonSerializer.Serialize(json.Value, new JsonSerializerOptions { WriteIndented = true });
                _jsonPreview.Text = text;
                _jsonPreview.AddThemeColorOverride("font_color", new Color(0.8f, 1f, 0.8f));
            }
            else
            {
                _jsonPreview.Text = "(no output connected)";
                _jsonPreview.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            }
        }
        catch (Exception e)
        {
            _jsonPreview.Text = $"Error: {e.Message}";
            _jsonPreview.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
        }
    }

    private void AutoConnectToOutput()
    {
        AutoConnectRootToOutput(_editor);
    }

    /// <summary>
    /// Auto-connects the last expression node (not the output) to the output node.
    /// This is used after deserialization to link the root of the deserialized tree.
    /// </summary>
    private static void AutoConnectRootToOutput(ExprNodeEditor editor)
    {
        var outputNode = editor.GetOutputNode();
        if (outputNode == null) return;

        // Find all nodes that have no downstream connections (i.e. their output isn't connected to anything)
        var exprNodes = editor.GetExpressionNodes();
        var connectedSources = new System.Collections.Generic.HashSet<string>();
        var connections = editor.GetConnectionList();
        foreach (var conn in connections)
        {
            connectedSources.Add(conn["from_node"].AsStringName().ToString());
        }

        GraphNode? rootCandidate = null;
        foreach (var node in exprNodes)
        {
            if (!connectedSources.Contains(node.Name.ToString()))
            {
                rootCandidate = node;
                // Take the last unconnected node as root (deepest in creation order)
            }
        }

        if (rootCandidate != null)
        {
            editor.ConnectNode(rootCandidate.Name, 0, outputNode.Name, 0);
        }
    }
}
