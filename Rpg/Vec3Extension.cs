using System.Numerics;

namespace Rpg;

public static class Vec3Extension
{
    public static Vector2 XY(this Vector3 vector)
    {
        return new Vector2(vector.X, vector.Y);
    }
}
