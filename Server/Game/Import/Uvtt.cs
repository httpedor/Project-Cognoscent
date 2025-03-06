using System.Numerics;
using System.Text.Json;
using Rpg;
using Rpg.Entities;

namespace Server.Game.Import;

public static class Uvtt
{
public class UvttBoard
{
    public double format { get; set; }
    public Resolution resolution { get; set; }
    public Line_of_sight[][] line_of_sight { get; set; }
    public Line_of_sight[][] objects_line_of_sight { get; set; }
    public UvttPortal[] portals { get; set; }
    public Environment environment { get; set; }
    public UvttLight[] lights { get; set; }
    public string image { get; set; }
}
public class UvttLight
{
    public Line_of_sight position { get; set; }
    public float range { get; set; }
    public float intensity { get; set; }
    public string color { get; set; }
    public bool shadows { get; set; }
}

public class UvttPortal
{
    public Line_of_sight position {get; set; }
    public Line_of_sight[] bounds {get; set;}
    public float rotation {get; set;}
    public bool closed {get; set;}
    public bool freestanding {get; set;}
}

public class Resolution
{
    public Map_origin map_origin { get; set; }
    public Map_size map_size { get; set; }
    public int pixels_per_grid { get; set; }
}

public class Map_origin
{
    public int x { get; set; }
    public int y { get; set; }
}

public class Map_size
{
    public int x { get; set; }
    public int y { get; set; }
}

public class Line_of_sight
{
    public float x { get; set; }
    public float y { get; set; }
}

public class Environment
{
    public bool baked_lighting { get; set; }
    public string ambient_light { get; set; }
}

    public static ServerBoard? LoadBoardFromUvttJson(string json, string name)
    {
        Floor? f = LoadFloorFromUvttJson(json);
        if (f == null)
            return null;
        var b = new ServerBoard(name);
        b.AddFloor(f);
        return b;
    }

    public static Floor? LoadFloorFromUvttJson(string json, List<Entity[]>? portals = null )
    {
        UvttBoard? uvtt = JsonSerializer.Deserialize<UvttBoard>(json);
        if (uvtt == null || uvtt.image == null)
            return null;
        if (uvtt.environment.ambient_light == null)
            uvtt.environment.ambient_light = "0xFFFFFFFF";
        if (!uvtt.environment.ambient_light.StartsWith("0x"))
            uvtt.environment.ambient_light = "0x" + uvtt.environment.ambient_light.ToUpper();
        var f = new ServerFloor(
            new Vector2(uvtt.resolution.map_size.x, uvtt.resolution.map_size.y),
            new Vector2(uvtt.resolution.pixels_per_grid, uvtt.resolution.pixels_per_grid),
            Convert.ToUInt32(uvtt.environment.ambient_light, 16)
        );
        f.Walls = uvtt.line_of_sight.Select((points) =>
                new Polygon()
                {
                    points = points.Select((point) => new Vector2(point.x, point.y)).ToArray()
                }
            ).ToArray();
        f.LineOfSight = (Polygon[])f.Walls.Clone();

        f.Lights = uvtt.lights.Select((uvttLight) =>{
            if (!uvttLight.color.StartsWith("0x"))
                uvttLight.color = "0x" + uvttLight.color.ToUpper();

            return new Light(
                new Vector2(uvttLight.position.x, uvttLight.position.y),
                uvttLight.range,
                uvttLight.intensity,
                Convert.ToUInt32(uvttLight.color, 16),
                uvttLight.shadows
            );
        }
        ).ToArray();

        if (portals != null)
        {
            foreach (var portal in uvtt.portals)
            {
                var ent = new Door()
                {
                    //Position = new Vector3(portal.position.x, portal.position.y, 0),
                };
                //portals.Add(ent);
            }
        }

        f.SetImage(Convert.FromBase64String(uvtt.image));
        f.UpdateTilesFromImage();
        f.UpdateCollisionGrid();
        return f;
    }

}
