using Godot;

namespace TTRpgClient.scripts.ui;

public static class ToastParty
{
    public static class Direction
    {
        public const string LEFT = "left";
        public const string CENTER = "center";
        public const string RIGHT = "right";
    }
    public static class Gravity
    {
        public const string TOP = "top";
        public const string BOTTOM = "bottom";
    }
    public class Config
    {
        public string? Text;
        public string? Direction;
        public int? TextSize;
        public Color? BgColor;
        public Color? Color;
        public string? Gravity;
        public bool? UseFont;
        public float? Duration;
    }
    private static Node _node;
    public static Node ToastNode
    {
        get
        {
            if (_node == null)
                _node = GameManager.Instance.GetTree().Root.GetNode("ToastParty");
            return _node;
        }
    }

    public static GodotObject Show(Config config)
    {
        return (GodotObject)ToastNode.Call("show", new Godot.Collections.Dictionary
        {
            {"text", config.Text ?? ""},
            {"direction", config.Direction ?? Direction.CENTER},
            {"text_size", config.TextSize ?? 20},
            {"bgcolor", config.BgColor ?? new Color(0, 0, 0, 0.5f)},
            {"color", config.Color ?? new Color(1, 1, 1, 1)},
            {"gravity", config.Gravity ?? Gravity.BOTTOM},
            {"use_font", config.UseFont ?? false},
            {"duration", config.Duration ?? 2}
        });
    }
}
