using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO.Compression;
using System.Numerics;
using Rpg.Entities;

namespace Rpg;

public struct BoardIntersectionInfo
{
    public bool IsCeiling;
    public Vector3 Position;
    public Vector3 Normal;
}

public class BoardRef : ISerializable
{
    public string BoardName;
    public Board? Board
    {
        get {
            return SidedLogic.Instance.GetBoard(BoardName);
        }
    }

    public BoardRef(Board board)
    {
        BoardName = board.Name;
    }
    public BoardRef(Stream stream)
    {
        BoardName = stream.ReadString();
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(BoardName);
    }

}

public abstract class Board
{
    public string Name = "";
    protected Floor[] floors = new Floor[0];
    protected List<Entity> entities = new List<Entity>();
    private Dictionary<int, Entity> entityCache = new Dictionary<int, Entity>();
    private Dictionary<EntityType, List<Entity>> entityCacheByType = new Dictionary<EntityType, List<Entity>>();
    protected List<string> chatHistory = new List<string>();

    public bool CombatMode = false;
    public int CombatTick = -1;

    public Board(){
        floors = new Floor[0];
        foreach (var type in Enum.GetValues<EntityType>())
            entityCacheByType[type] = new List<Entity>();
    }

    public virtual void AddEntity(Entity entity){
        if (entity.Board != null)
            entity.Board.RemoveEntity(entity);
        entities.Add(entity);
        entityCache[entity.Id] = entity;
        entityCacheByType[entity.GetEntityType()].Add(entity);
        entity.Board = this;
    }
    public List<Entity> GetEntities(){
        return entities;
    }
    public List<T> GetEntities<T>() where T : Entity
    {
        List<T> ret = new();
        foreach (var ent in entities)
        {
            if (ent is T sub)
            {
                ret.Add(sub);
            }
        }
        return ret;
    }
    public Entity? GetEntityById(int id){
        return entityCache.TryGetValue(id, out Entity? entity) ? entity : null;
    }
    public virtual List<Creature> GetCreaturesByOwner(string owner)
    {
        List<Creature> creatures = new List<Creature>();
        foreach (var entity in entities)
        {
            if (entity is Creature creature && creature.Owner.Equals(owner))
                creatures.Add(creature);
        }
        return creatures;
    }
    public virtual void RemoveEntity(Entity? entity){
        if (entity == null)
            return;
        entities.Remove(entity);
        entity.Board = null;
    }
    public void RemoveEntity(int id){
        RemoveEntity(entities.Find(e => e.Id == id));
    }

    public void AddFloor(Floor toAdd){
        Array.Resize(ref floors, floors.Length + 1);
        SetFloor(floors.Length - 1, toAdd);
    }

    public virtual void SetFloor(int index, Floor? toSet){
        if (index < 0 || index >= floors.Length)
            throw new Exception("Invalid floor index");

        if (toSet == null){
            floors = floors.Where((source, i) => i != index).ToArray();
            return;
        }

        floors[index] = toSet;
    }

    public void RemoveFloor(int index){
        SetFloor(index, null);
    }

    public Floor GetFloor(int index){
        if (index < 0 || index >= floors.Length)
            throw new Exception("Invalid floor index");

        return floors[index];
    }

    public byte GetFloorCount(){
        return (byte)floors.Length;
    }

    public List<string> GetChatHistory(){
        return chatHistory;
    }
    public virtual void AddChatMessage(string message){
        chatHistory.Add(message);
    }
    public abstract void BroadcastMessage(string message);

    public float? GetVerticalIntersection(Vector3 position, float zChange)
    {
        var floor = (int)MathF.Floor(position.Z);

        if (floor < 0 || floor >= floors.Length)
            return null;

        var currentFloor = floors[floor];
        while (zChange != 0)
        {
            var diff = zChange;
            if (diff > 1)
                diff = 1;
            if (diff < -1)
                diff = -1;

            zChange -= diff;

            var nextZ = position.Z + diff;

            if ((int)nextZ >= floors.Length)
                return null;

            if (MathF.Floor(nextZ) < 0)
            {
                if (currentFloor.IsTileAtBlocked(new Vector2(position.X, position.Y)))
                    return 0;
                
                return null;
            }

            var nextPosition = new Vector3(position.X, position.Y, position.Z + diff);
            var nextFloor = floors[(Int32)MathF.Floor(nextZ)];

            if (currentFloor == nextFloor)
                return null;
            
            if (nextZ < position.Z)
            {
                if (currentFloor.IsTileAtBlocked(new Vector2(position.X, position.Y)))
                    return position.Z;
            }
            else
            {
                if (nextFloor.IsTileAtBlocked(new Vector2(position.X, position.Y)))
                    return (int)(position.Z + zChange);
            }

            position = nextPosition;
            currentFloor = nextFloor;
        }

        return null;
    }

    [Obsolete("Please don't use this, it's untested, probably broken, and pretty much useless too.")]
    public BoardIntersectionInfo? GetIntersection(Vector3 start, Vector3 end){
        if (start.Z < 0 || start.Z >= floors.Length || end.Z < 0 || end.Z >= floors.Length)
            return null;

        var startFloor = (int)start.Z;
        var endFloor = (int)end.Z;
        if (startFloor == endFloor)
        {
            Vector2? normal;
            var floorIntersect = floors[startFloor].GetIntersection(new Vector2(start.X, start.Y), new Vector2(end.X, end.Y), out normal);
            if (floorIntersect != null)
            {
                return new BoardIntersectionInfo { IsCeiling = false, Position = new Vector3((Vector2)floorIntersect, startFloor), Normal = new Vector3((Vector2)normal, 0) };
            }

            return null;
        }
        
        var vector = end - start;
        var projection = new Vector2(vector.X, vector.Y);

        var currentFloor = startFloor;
        var currentStart = start;
        while (currentFloor <= endFloor){

            var heightLeft = (currentFloor + 1) - currentStart.Z;
            var projectionInCurrentFloor = (projection * heightLeft) / vector.Z;
            var currentEnd = new Vector2(currentStart.X + projectionInCurrentFloor.X, currentStart.Y + projectionInCurrentFloor.Y);
            Vector2? normal;
            var intersection = floors[currentFloor].GetIntersection(new Vector2(currentStart.X, currentStart.Y), currentEnd, out normal);
            if (intersection != null)
                return new BoardIntersectionInfo { IsCeiling = false, Position = new Vector3((Vector2)intersection, currentFloor+1), Normal = new Vector3((Vector2)normal, 0) };

            currentStart += new Vector3(projectionInCurrentFloor.X, projectionInCurrentFloor.Y, heightLeft);
            currentFloor++;
            
            if (floors[currentFloor].IsTileAtBlocked(currentEnd))
                return new BoardIntersectionInfo { IsCeiling = true, Position = new Vector3(currentEnd, currentFloor), Normal = new Vector3(0, 0, -1) };
            // this code has NOT been tested
        }

        return null;
    }
}
