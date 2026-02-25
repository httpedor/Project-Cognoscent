using Godot;
using Rpg.Entities.Components;

namespace TTRpgClient.scripts;

public partial class LightRenderer : ComponentRenderer<Light>
{
    public static bool ShowLightIcons = false;
    private static Texture2D tex = GD.Load<Texture2D>("res://assets/light.webp");
    private readonly PointLight2D pointLight;

    public LightRenderer(Light light, EntityRenderer entity) : base(light, entity)
    {
        byte alpha = (byte)((light.Color >> 24) & 0xFF);
        byte red = (byte)((light.Color >> 16) & 0xFF);
        byte green = (byte)((light.Color >> 8) & 0xFF);
        byte blue = (byte)(light.Color & 0xFF);
        pointLight = new PointLight2D
        {
            Name = "Light" + light.Entity.Id,
            Texture = tex,
            Color = Color.Color8(red, green, blue, alpha),
        };
        AddChild(pointLight);

    }

    public override void OnReady()
    {
        if (GameManager.IsGm)
        {
            EntityRenderer.Display?.SetImage(Icons.Light);
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        var token = Entity.Token!;
        if (token.Floor == null)
            return;
        var TileSize = token.Floor.TileSize;

        pointLight.Energy = Component.Intensity;
        pointLight.ShadowEnabled = Component.Shadows;
        pointLight.Scale = new Vector2(TileSize.X / tex.GetWidth() * Component.Range, TileSize.Y / tex.GetHeight() * Component.Range);
    
        if (GameManager.Instance.CurrentBoard?.SelectedEntity?.HasComponent(Token.ID) ?? false)
            EntityRenderer.Display?.Visible = false;
        else 
            if (ShowLightIcons)
                EntityRenderer.Display?.Visible = true;
            else
                EntityRenderer.Display?.Visible = false;
    }
}