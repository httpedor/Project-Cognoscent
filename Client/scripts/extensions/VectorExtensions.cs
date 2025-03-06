
using System.Numerics;

public static class VectorExtensions{
    public static Godot.Vector2 ToGodot(this Vector2 vec){
        return new Godot.Vector2(vec.X, vec.Y);
    }

    public static Vector2 ToNumerics(this Godot.Vector2 vec){
        return new Vector2(vec.X, vec.Y);
    }

    public static Godot.Vector3 ToGodot(this Vector3 vec){
        return new Godot.Vector3(vec.X, vec.Y, vec.Z);
    }

    public static Vector3 ToNumerics(this Godot.Vector3 vec){
        return new Vector3(vec.X, vec.Y, vec.Z);
    }

    public static Godot.Vector2 ToV2(this Godot.Vector3 vec)
    {
        return new Godot.Vector2(vec.X, vec.Y);
    }

    public static Vector2 ToV2(this Vector3 vec)
    {
        return new Vector2(vec.X, vec.Y);
    }
}