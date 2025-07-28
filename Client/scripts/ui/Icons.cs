using System.Collections.Generic;
using Godot;

public static class Icons
{
    private static Dictionary<string, Texture2D> cache = new();
    public static readonly Texture2D Ping = GetIcon("ping");
    public static readonly Texture2D Ping2 = GetIcon("ping2");
    public static readonly Texture2D Trash = GetIcon("trash");
    public static readonly Texture2D Lighting = GetIcon("lightning");
    public static readonly Texture2D Move = GetIcon("move");
    public static readonly Texture2D LookAt = GetIcon("look-at");
    public static readonly Texture2D Box = GetIcon("box");
    public static readonly Texture2D Stop = GetIcon("stop");
    public static readonly Texture2D Punch = GetIcon("punch");
    public static readonly Texture2D Kick = GetIcon("kick");
    public static readonly Texture2D Humanoid = GetIcon("humanoid");
    public static readonly Texture2D CloseCircle = GetIcon("close-circle");
    public static readonly Texture2D Light = GetIcon("light");
    public static readonly Texture2D Chat = GetIcon("chat");
    public static readonly Texture2D Book = GetIcon("book");


    public static Texture2D GetIcon(string name)
    {
        if (cache.ContainsKey(name))
            return cache[name];
        
        var icon = GD.Load<Texture2D>($"res://assets/svg/{name}.svg");
        if (icon == null)
            icon = GD.Load<Texture2D>($"res://assets/svg/unknown.svg");
        cache[name] = icon;
        return icon;
    }
}