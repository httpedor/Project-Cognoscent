using System.Drawing;
using System.Numerics;
using Rpg;

namespace Rpg;

public struct Polygon : ISerializable {
    public Vector2[] points;

    public Polygon(Vector2[] points){
        this.points = points;
    }
    public Polygon(Stream stream){
        points = new Vector2[stream.ReadUInt16()];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = stream.ReadVec2();
        }
    }

    public void ToBytes(Stream stream){
            stream.Write(BitConverter.GetBytes((ushort)points.Length));
            foreach (Vector2 point in points)
            {
                stream.WriteVec2(point);
            }

    }
}


public struct Light : ISerializable {
    public Vector2 Position;
    public float Range;
    public float Intensity;
    public uint Color;
    public bool Shadows;
    public Light(Vector2 position, float range, float intensity, uint color, bool shadows){
        Position = position;
        Range = range;
        Intensity = intensity;
        Color = color;
        Shadows = shadows;
    }
    public Light(Stream stream){
        Position = stream.ReadVec2();
        Range = stream.ReadFloat();
        Intensity = stream.ReadFloat();
        Color = stream.ReadUInt32();
        Shadows = stream.ReadByte() == 1;
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteVec2(Position);
        stream.WriteFloat(Range);
        stream.WriteFloat(Intensity);
        stream.WriteUInt32(Color);
        stream.WriteByte((byte)(Shadows ? 1 : 0));
    }

}

public abstract class Floor
{
    public event Action<Midia>? OnMidiaChanged;
    public enum TileFlag{
        AIR = 0x0,
        FLOOR = 0x1,
        IS_STAIR = 0x2,
        STAIR_HORIZONTAL = 0x4,
        STAIR_INVERSE = 0x8
    }

    public Vector2 Size { get; protected set; }
    public Vector2 TileSize { get; protected set; }
    public uint AmbientLight {get; set;}
    public Polygon[] Walls = Array.Empty<Polygon>();
    public Polygon[] LineOfSight = Array.Empty<Polygon>();
    public uint[] TileFlags = Array.Empty<uint>();
    public float DefaultEntitySight;
    public Midia Display { get; protected set;}

    private void GenerateTiles(){
        TileFlags = new uint[(int)(Size.X * Size.Y)];
        for (int i = 0; i < TileFlags.Length; i++)
        {
            TileFlags[i] = (uint)TileFlag.FLOOR;
        }
    }

    public Floor(){
        Size = new Vector2(10, 10);
        TileSize = new Vector2(32, 32);
        AmbientLight = 0xFFFFFFFF;
        Display = new Midia();
        DefaultEntitySight = MathF.Max(Size.X, Size.Y);
        GenerateTiles();
    }

    public Floor(Vector2 size, Vector2 tileSize, uint ambientLight){
        Size = size;
        TileSize = tileSize;
        AmbientLight = ambientLight;
        Display = new Midia();
        DefaultEntitySight = MathF.Max(Size.X, Size.Y);
        GenerateTiles();
    }

    public void UpdateTilesFromImage()
    {
        TileFlags = new uint[(int)(Size.X * Size.Y)];
        if (Display.Type != MidiaType.Image)
            return;
        
        using (Image img = Image.FromStream(new MemoryStream(Display.Bytes)))
        {
            var bitmap = new Bitmap(img);
            for (int i = 0; i < TileFlags.Length; i++)
            {
                int x = (int)(i % Size.X);
                int y = (int)(i / Size.X);
                int imgX = (int)(x * TileSize.X + TileSize.X / 2);
                int imgY = (int)(y * TileSize.Y + TileSize.Y / 2);
                Color c = bitmap.GetPixel(imgX, imgY);

                TileFlags[i] = c.A == 0 ? (uint)TileFlag.AIR : (uint)TileFlag.FLOOR;
            }
        }
    }

    public bool TileHasFlag(Vector2 position, TileFlag flag){
        return (TileFlags[(int)(MathF.Floor(position.Y) * Size.X + MathF.Floor(position.X))] & (uint)flag) != 0;
    }

    public Vector2 WorldToPixel(Vector2 world){
        return world * TileSize;
    }
    
    public Vector2 PixelToWorld(Vector2 pixel){
        return pixel / TileSize;
    }
    public Vector2 WorldToTileCenter(Vector2 world){
        return new Vector2((int)world.X + 0.5f, (int)world.Y + 0.5f);
    }

    public bool IsTileAtBlocked(Vector2 position){
        return TileHasFlag(position, TileFlag.FLOOR);
    }

    public Vector2? GetTileStairs(Vector2 position)
    {
        if (!TileHasFlag(position, TileFlag.IS_STAIR))
            return null;
        Vector2 dir;
        if (TileHasFlag(position, TileFlag.STAIR_HORIZONTAL))
            dir = new Vector2(1, 0);
        else
            dir = new Vector2(0, 1);
        if (TileHasFlag(position, TileFlag.STAIR_INVERSE))
            dir *= -1;

        return dir;
    }

    public Midia GetMidia(){
        return Display;
    }

    public virtual void SetMidia(Midia midia)
    {
        OnMidiaChanged?.Invoke(midia);
        Display = midia;
    }

    public Vector2? GetIntersection(Vector2 start, Vector2 end){
        return GetIntersection(start, end, out _);
    }
    public abstract Vector2? GetIntersection(Vector2 start, Vector2 end, out Vector2? normal);
    public abstract IEnumerable<Line> PossibleOBBIntersections(OBB obb);
    public IEnumerable<Line> BroadPhaseOBB(OBB obb){
        return PossibleOBBIntersections(obb);
    }
}
