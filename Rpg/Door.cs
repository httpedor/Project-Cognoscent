using System.Numerics;

namespace Rpg;

public class Door
{
    public Vector2 position {get; set; }
    public Vector2[] bounds {get; set;}
    public float rotation {get; set;}
    public bool closed {get; set;}
    public bool freestanding {get; set;}
}
