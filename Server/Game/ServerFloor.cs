using System.Numerics;
using MoonSharp.Interpreter;
using Rpg;

namespace Server.Game;

public struct FloorCollisionInfo
{
    public Line[] Walls;
    public AreaTrigger[] Triggers;
    public Vector2[] Intersections;
    public Vector2[] Normals;

    public readonly uint TriggerIntersectionStartIndex => (uint)Walls.Length;
    public readonly uint WallIntersectionStartIndex => 0;
}

public class AreaTrigger : ISerializable
{
    public Polygon Area;
    public Rpg.Skill Action;

    public AreaTrigger(Polygon area, Rpg.Skill action){
        Area = area;
        Action = action;
    }

    public AreaTrigger(Stream stream){
        Area = new Polygon(stream);
        Action = Rpg.Skill.FromBytes(stream);
    }

    public void ToBytes(Stream stream){
        Area.ToBytes(stream);
        Action.ToBytes(stream);
    }
}

public class ServerFloor : Floor, ISerializable
{
    public AreaTrigger[] Triggers = Array.Empty<AreaTrigger>();
    private List<Line>[,] collisionGrid;
    private List<AreaTrigger>[,] triggerGrid;
    
    public void UpdateCollisionGrid()
    {
        collisionGrid = new List<Line>[(int)Size.X, (int)Size.Y];
        for (int x = 0; x < Size.X; x++)
        {
            for (int y = 0; y < Size.Y; y++)
            {
                collisionGrid[x, y] = new List<Line>();
            }
        }
        triggerGrid = new List<AreaTrigger>[(int)Size.X, (int)Size.Y];
        for (int x = 0; x < Size.X; x++)
        {
            for (int y = 0; y < Size.Y; y++)
            {
                triggerGrid[x, y] = new List<AreaTrigger>();
            }
        }

        foreach (var wall in Walls)
        {
            for (int i = 0; i < wall.points.Length-1; i++)
            {
                var A = wall.points[i];
                var B = wall.points[i + 1];
                var line = new Line(A, B);
                var min = new Vector2(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y));
                var max = new Vector2(Math.Max(A.X, B.X), Math.Max(A.Y, B.Y));
                for (int x = (int)min.X; x <= (int)max.X; x++)
                {
                    for (int y = (int)min.Y; y <= (int)max.Y; y++)
                    {
                        if (!collisionGrid[x, y].Contains(line))
                            collisionGrid[x, y].Add(line);
                    }
                }
            }
        }
        foreach (var trigger in Triggers)
        {
            for (int i = 0; i < trigger.Area.points.Length-1; i++)
            {
                var A = trigger.Area.points[i];
                var B = trigger.Area.points[i + 1];
                var min = new Vector2(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y));
                var max = new Vector2(Math.Max(A.X, B.X), Math.Max(A.Y, B.Y));
                for (int x = (int)min.X; x <= (int)max.X; x++)
                {
                    for (int y = (int)min.Y; y <= (int)max.Y; y++)
                    {
                        if (!triggerGrid[x, y].Contains(trigger))
                            triggerGrid[x, y].Add(trigger);
                    }
                }
            }
        }
    }

    public ServerFloor(Vector2 size, Vector2 tileSize, UInt32 ambinetLight) : base(size, tileSize, ambinetLight)
    {
    }

    public ServerFloor(Stream stream){
        Size = stream.ReadVec2();
        TileSize = stream.ReadVec2();
        AmbientLight = stream.ReadUInt32();

        TileFlags = new UInt32[stream.ReadUInt32()];
        for (int i = 0; i < TileFlags.Length; i++)
            TileFlags[i] = stream.ReadUInt32();

        Walls = new Polygon[stream.ReadUInt16()];
        for (int i = 0; i < Walls.Length; i++)
            Walls[i] = new Polygon(stream);
        
        LineOfSight = new Polygon[stream.ReadUInt16()];
        for (int i = 0; i < LineOfSight.Length; i++)
            LineOfSight[i] = new Polygon(stream);
        
        Triggers = new AreaTrigger[stream.ReadUInt16()];
        for (int i = 0; i < Triggers.Length; i++)
            Triggers[i] = new AreaTrigger(stream);

        Image = new byte[stream.ReadUInt32()];
        stream.ReadExactly(Image);
        UpdateCollisionGrid();
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteVec2(Size);
        stream.WriteVec2(TileSize);
        stream.Write(BitConverter.GetBytes(AmbientLight));
        stream.Write(BitConverter.GetBytes(TileFlags.Length));
        foreach (var tileFlag in TileFlags)
            stream.Write(BitConverter.GetBytes(tileFlag));
        stream.Write(BitConverter.GetBytes((UInt16)Walls.Length));
        foreach (var wall in Walls)
            wall.ToBytes(stream);
        
        stream.Write(BitConverter.GetBytes((UInt16)LineOfSight.Length));
        foreach (var vb in LineOfSight)
            vb.ToBytes(stream);
        
        stream.WriteUInt16((UInt16)Triggers.Length);
        foreach (var trigger in Triggers)
            trigger.ToBytes(stream);
        
        stream.Write(BitConverter.GetBytes((UInt32)Image.Length));
        stream.Write(Image);
    }

    public bool AddTrigger(AreaTrigger trigger)
    {
        /*if (trigger.Action.GetRequiredDataType() != null && !trigger.Action.GetRequiredDataType().IsAssignableTo(typeof(EntityTargetedActionData)))
            return false;*/
        Triggers = Triggers.Append(trigger).ToArray();
        return true;
    }

    public FloorCollisionInfo GetIntersection(Vector2 start, Vector2 end, int limit, bool countWalls=true, bool countTriggers=true, bool normals=true)
    {
        if (limit <= 0)
            return new FloorCollisionInfo { Intersections = Array.Empty<Vector2>(), Walls = Array.Empty<Line>(), Triggers = Array.Empty<AreaTrigger>()};

        List<Vector2> intersections = new List<Vector2>();
        List<Line> walls = new();
        List<AreaTrigger> triggers = new List<AreaTrigger>();
        List<Vector2> normalsList = new List<Vector2>();

        var min = new Vector2(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
        var max = new Vector2(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
        for (int x = (Int32)min.X; x <= max.X; x++)
        {
            for (int y = (Int32)min.Y; y <= max.Y; y++)
            {
                if (x < 0 || x >= Size.X || y < 0 || y >= Size.Y)
                    continue;

                var lines = collisionGrid[x, y];
                if (countWalls && lines.Count > 0)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        var p3 = line.Start;
                        var p4 = line.End;
                        var intersect = Geometry.LineLineIntersection(start, end, p3, p4);
                        if (intersect != null && !intersections.Contains((Vector2)intersect))
                        {
                            intersections.Add((Vector2)intersect);
                            walls.Add(line);
                            if (normals)
                            {
                                var normal = new Vector2(p4.Y - p3.Y, p3.X - p4.X);
                                var midPoint = (p3 + p4) / 2;
                                var midToStart = Vector2.Normalize(start - midPoint);
                                if (Vector2.Dot(midToStart, midPoint) < 0)
                                    normal = -normal;
                                normalsList.Add(Vector2.Normalize(normal));
                            }
                            if (intersections.Count >= limit)
                                goto LimitExceeded;
                        }
                    }
                }

                var triggersHere = triggerGrid[x, y];
                if (countTriggers && triggersHere.Count > 0)
                {
                    foreach (var trigger in triggersHere)
                    {
                        for (int i = 0; i < trigger.Area.points.Length; i++)
                        {
                            var p3 = trigger.Area.points[i];
                            var p4 = trigger.Area.points[i + 1];

                            var intersect = Geometry.LineLineIntersection(start, end, p3, p4);
                            if (intersect != null && !intersections.Contains((Vector2)intersect))
                            {
                                intersections.Add((Vector2)intersect);
                                triggers.Add(trigger);
                                if (intersections.Count >= limit)
                                    goto LimitExceeded;
                            }
                        }
                    }
                }
            }
        }

    LimitExceeded:

        return new FloorCollisionInfo { Intersections = intersections.ToArray(), Walls = walls.ToArray(), Triggers = triggers.ToArray(), Normals = normalsList.ToArray()};
    }

    public override Vector2? GetIntersection(Vector2 start, Vector2 end, out Vector2? normal)
    {
        var info = GetIntersection(start, end, Int32.MaxValue, countTriggers: false, normals: true);
        normal = null;
        if (info.Intersections.Length == 0)
        {
            return null;
        }
        Vector2 closest = Vector2.Zero;
        float closestDist = Single.MaxValue;
        for (int i = 0; i < info.Intersections.Length; i++)
        {
            var dist = Vector2.Distance(start, info.Intersections[i]);
            if (dist < closestDist)
            {
                closest = info.Intersections[i];
                closestDist = dist;
                normal = info.Normals[i];
            }
        }

        return closest;
    }

    public override IEnumerable<Line> PossibleOBBIntersections(OBB obb)
    {
        var corners = obb.Corners;
        var min = new Vector2(corners[0].X, corners[0].Y);
        var max = new Vector2(corners[0].X, corners[0].Y);
        for (int i = 1; i < corners.Length; i++)
        {
            min = new Vector2(Math.Min(min.X, corners[i].X), Math.Min(min.Y, corners[i].Y));
            max = new Vector2(Math.Max(max.X, corners[i].X), Math.Max(max.Y, corners[i].Y));
        }

        for (int x = (Int32)min.X; x <= max.X; x++)
        {
            for (int y = (Int32)min.Y; y <= max.Y; y++)
            {
                if (x < 0 || x >= Size.X || y < 0 || y >= Size.Y)
                    continue;

                var walls = collisionGrid[x, y];
                foreach (var wall in walls)
                {
                    yield return wall;
                }
            }
        }

        /*foreach (var door in Doors)
        {
            if (door.Closed)
                yield return new Line(door.Bounds[0], door.Bounds[1]);
            else
                yield return new Line(door.Bounds[0], door.OpenBound2);
        }*/
    }

    public IEnumerable<AreaTrigger> PossibleTriggersAt(Vector2 position)
    {
        var x = (Int32)position.X;
        var y = (Int32)position.Y;
        if (x < 0 || x >= Size.X || y < 0 || y >= Size.Y)
            yield break;

        foreach (var trigger in triggerGrid[x, y])
        {
            yield return trigger;
        }
    }
}
