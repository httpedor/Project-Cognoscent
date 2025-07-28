using Godot;
using Rpg;
using TTRpgClient.scripts.RpgImpl;

namespace TTRpgClient.scripts;

public partial class LightNode : EntityNode
{
    public static bool ShowLightIcons = true;
    private static Texture2D tex = GD.Load<Texture2D>("res://assets/light.webp");
    public readonly LightEntity Light;
    public readonly PointLight2D pointLight;
    public LightNode(LightEntity light, ClientBoard board) : base(light, board)
    {
        Light = light;
        byte alpha = (byte)((light.Color >> 24) & 0xFF);
        byte red = (byte)((light.Color >> 16) & 0xFF);
        byte green = (byte)((light.Color >> 8) & 0xFF);
        byte blue = (byte)(light.Color & 0xFF);
        pointLight = new PointLight2D
        {
            Name = "Light" + light.Id,
            Texture = tex,
            Color = Color.Color8(red, green, blue, alpha),
        };
        AddChild(pointLight);

        if (GameManager.IsGm)
        {
            Display.SetImage(Icons.Light);
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        var TileSize = Light.Floor.TileSize;

        pointLight.Energy = Light.Intensity;
        pointLight.ShadowEnabled = Light.Shadows;
        pointLight.Scale = new Vector2(TileSize.X / tex.GetWidth() * Light.Range, TileSize.Y / tex.GetHeight() * Light.Range);
    
        if (GameManager.Instance.CurrentBoard != null && GameManager.Instance.CurrentBoard.SelectedEntity is Creature)
            Display.Visible = false;
        else 
            if (ShowLightIcons)
                Display.Visible = true;
            else
                Display.Visible = false;
    }

    protected override void MouseEntered()
    {
        if (GameManager.IsGm || (GameManager.Instance.CurrentBoard != null && GameManager.Instance.CurrentBoard.SelectedEntity is Creature))
            base.MouseEntered();
    }

    protected override void MouseExited()
    {
        if (GameManager.IsGm || (GameManager.Instance.CurrentBoard != null && GameManager.Instance.CurrentBoard.SelectedEntity is Creature))
            base.MouseExited();
    }
}
