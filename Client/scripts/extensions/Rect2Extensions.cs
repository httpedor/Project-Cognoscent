using Godot;

namespace TTRpgClient.scripts.extensions;

public static class Rect2Extensions
{
    public static Vector2 ClosestPoint(this Rect2 rect, Vector2 point)
    {
        float x = Mathf.Clamp(point.X, rect.Position.X, rect.Position.X + rect.Size.X);
        float y = Mathf.Clamp(point.Y, rect.Position.Y, rect.Position.Y + rect.Size.Y);
        return new Vector2(x, y);
    }
}