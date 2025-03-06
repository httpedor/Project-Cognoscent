using System.Drawing;
using System.Numerics;
using Rpg.Entities;

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
            stream.Write(BitConverter.GetBytes((UInt16)points.Length));
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
    public UInt32 Color;
    public bool Shadows;
    public Light(Vector2 position, float range, float intensity, UInt32 color, bool shadows){
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
    public enum TileFlag{
        AIR = 0x0,
        FLOOR = 0x1,
    }

    public Vector2 Size { get; protected set; }
    public Vector2 TileSize { get; protected set; }
    public UInt32 AmbientLight {get; set;}
    public Polygon[] Walls = Array.Empty<Polygon>();
    public Polygon[] LineOfSight = Array.Empty<Polygon>();
    public Light[] Lights = Array.Empty<Light>();
    public UInt32[] TileFlags = Array.Empty<UInt32>();
    public byte[] Image { get; protected set;}

    private void GenerateTiles(){
        TileFlags = new UInt32[(int)(Size.X * Size.Y)];
        for (int i = 0; i < TileFlags.Length; i++)
        {
            TileFlags[i] = (UInt32)TileFlag.FLOOR;
        }
    }

    public Floor(){
        Size = new Vector2(10, 10);
        TileSize = new Vector2(32, 32);
        AmbientLight = 0xFFFFFFFF;
        Image = new byte[0];
        GenerateTiles();
    }

    public Floor(Vector2 size, Vector2 tileSize, UInt32 ambientLight){
        Size = size;
        TileSize = tileSize;
        AmbientLight = ambientLight;
        Image = new byte[0];
        GenerateTiles();
    }

    public void UpdateTilesFromImage()
    {
        TileFlags = new UInt32[(int)(Size.X * Size.Y)];
        using (Image img = System.Drawing.Image.FromStream(new MemoryStream(Image)))
        {
            var bitmap = new Bitmap(img);
            for (int i = 0; i < TileFlags.Length; i++)
            {
                int x = (Int32)(i % Size.X);
                int y = (Int32)(i / Size.X);
                int imgX = (Int32)(x * TileSize.X + TileSize.X / 2);
                int imgY = (Int32)(y * TileSize.Y + TileSize.Y / 2);
                Color c = bitmap.GetPixel(imgX, imgY);

                TileFlags[i] = c.A == 0 ? (UInt32)TileFlag.AIR : (UInt32)TileFlag.FLOOR;
            }
        }
    }

    public bool TileHasFlag(Vector2 position, TileFlag flag){
        return (TileFlags[(int)(MathF.Floor(position.Y) * Size.X + MathF.Floor(position.X))] & (UInt32)flag) != 0;
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

    public byte[] GetImage(){
        return Image;
    }

    public virtual void SetImage(byte[] image){
        Image = image;
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
