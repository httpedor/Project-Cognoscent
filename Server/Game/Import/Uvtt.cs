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
        var entities = new List<Entity>();
        Floor? f = LoadFloorFromUvttJson(json, entities);
        if (f == null)
            return null;
        var b = new ServerBoard(name);
        b.AddFloor(f);
        foreach (var ent in entities)
            b.AddEntity(ent);
        return b;
    }

    public static Floor? LoadFloorFromUvttJson(string json, List<Entity>? entities = null)
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
        var los = uvtt.objects_line_of_sight.Select((points) => {
                return new Polygon()
                {
                    points = points.Select((point) => new Vector2(point.x, point.y)).ToArray()
                };
        }).ToArray();

        f.LineOfSight = new Polygon[f.Walls.Length + los.Length];
        for (int i = 0; i < f.Walls.Length; i++)
        {
            f.LineOfSight[i] = f.Walls[i];
        }
        for (int i = 0; i < los.Length; i++)
        {
            f.LineOfSight[i + f.Walls.Length] = los[i];
        }
        f.LineOfSight = (Polygon[])f.Walls.Clone();

        if (entities != null)
        {
            for (int i = 0; i < uvtt.portals.Length; i++)
            {
                var portal = uvtt.portals[i];
                var door = new Door()
                {
                    Position = new Vector3(portal.position.x, portal.position.y, 0),
                    Bounds = portal.bounds.Select((b) => new Vector2(b.x, b.y)).ToArray(),
                    Closed = portal.closed,
                    Rotation = portal.rotation
                };
                entities.Add(door);
            }
            for (int i = 0; i < uvtt.lights.Length; i++)
            {
                var uvttLight = uvtt.lights[i];
                if (!uvttLight.color.StartsWith("0x"))
                    uvttLight.color = "0x" + uvttLight.color.ToUpper();
                var light = new LightEntity()
                {
                    Position = new Vector3(uvttLight.position.x, uvttLight.position.y, 0),
                    Range = uvttLight.range,
                    Intensity = uvttLight.intensity,
                    Color = Convert.ToUInt32(uvttLight.color, 16),
                    Shadows = uvttLight.shadows
                };
                entities.Add(light);
            }
        }

        f.SetImage(Convert.FromBase64String(uvtt.image));
        f.UpdateTilesFromImage();
        f.UpdateCollisionGrid();
        return f;
    }

}
