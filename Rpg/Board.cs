using System.Numerics;
using Rpg;
using Rpg.Inventory;

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
    public Board? Board => SidedLogic.Instance.GetBoard(BoardName);

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
    protected Floor[] floors = Array.Empty<Floor>();
    protected List<Entity> entities = new();
    private readonly Dictionary<int, Entity> entityCache = new();
    private readonly Dictionary<EntityType, List<Entity>> entityCacheByType = new();
    private readonly Dictionary<int, Item> itemCache = new();
    protected List<string> chatHistory = new();
    private readonly Dictionary<int, (uint Tick, Action Action)> queuedActions = new();

    protected uint pauseTick = uint.MaxValue;
    public bool TurnMode = false;
    public uint CurrentTick = 0;

    protected Board(){
        foreach (EntityType type in Enum.GetValues<EntityType>())
            entityCacheByType[type] = new List<Entity>();


        foreach (Entity ent in GetEntitiesByType(EntityType.Creature))
        {
            RemoveEntity(ent);
        }
    }

    public virtual void AddEntity(Entity entity){
        if (entity.Board != null)
            entity.Board.RemoveEntity(entity);
        entities.Add(entity);
        entityCache[entity.Id] = entity;
        entityCacheByType[entity.GetEntityType()].Add(entity);
        entity.Board = this;

        if (entity is IItemHolder ih)
        {
            CacheItems(ih);
        }
        return;

        void CacheItems(IItemHolder holder)
        {
            foreach (Item item in holder.Items)
            {
                itemCache[item.Id] = item;
                var ihp = item.GetProperty<ItemHolderProperty>();
                if (ihp != null)
                    CacheItems(ihp);
            }
        }
    }
    
    public uint GetWhenToPause()
    {
        return pauseTick;
    }
    public virtual void PauseAt(uint tick)
    {
        pauseTick = tick;
    }
    public void PauseIn(uint tick)
    {
        PauseAt(CurrentTick + tick);
    }
    public void CancelPause()
    {
        PauseAt(uint.MaxValue);
    }

    public List<Entity> GetEntities(){
        return entities;
    }

    public IEnumerable<Entity> GetEntitiesByType(EntityType type)
    {
        return entityCacheByType[type];
    }
    public List<T> GetEntities<T>() where T : Entity
    {
        List<T> ret = new();
        foreach (Entity ent in entities)
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

    public T? GetEntityById<T>(int id) where T : Entity
    {
        return GetEntityById(id) as T;
    }
    public Item? GetItemById(int id)
    {
        return itemCache.TryGetValue(id, out Item? item) ? item : null;
    }
    public virtual List<Creature> GetCreaturesByOwner(string owner)
    {
        var creatures = new List<Creature>();
        foreach (Entity entity in entities)
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
        if (entity is IItemHolder ih)
            UncacheItems(ih);
        return;

        void UncacheItems(IItemHolder holder)
        {
            foreach (Item item in holder.Items)
            {
                itemCache.Remove(item.Id);
                var ihp = item.GetProperty<ItemHolderProperty>();
                if (ihp != null)
                    UncacheItems(ihp);
            }
        }
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
    
    public void Log(string message)
    {
        if (SidedLogic.Instance.IsClient())
            AddChatMessage(message);
        else
            BroadcastMessage(message);
    }

    // Technically this is the end of the current tick, or start of the next tick(depending on if the code is before or after the CurrentTick++)
    public virtual void Tick()
    {
        CurrentTick++;
        
        if (CurrentTick == pauseTick)
        {
            StartTurnMode();
        }

        foreach (var pair in queuedActions.ToArray())
        {
            if (pair.Value.Tick <= CurrentTick)
                pair.Value.Action();
            queuedActions.Remove(pair.Key);
        }
    }
    public virtual void StartTurnMode()
    {
        TurnMode = true;
    }
    public virtual void EndTurnMode()
    {
        TurnMode = false;
    }

    public int RunTaskLater(Action task, uint delay)
    {
        return RunTask(task, CurrentTick + delay);
    }

    public int RunTask(Action task, uint targetTick)
    {
        int id = new Random().Next();
        var ret = (targetTick, task);
        queuedActions[id] = ret;
        return id;
    }
    public void CancelTask(int id)
    {
        queuedActions.Remove(id);
    }

    public float? GetVerticalIntersection(Vector3 position, float zChange)
    {
        int floor = (int)MathF.Floor(position.Z);

        if (floor < 0 || floor >= floors.Length)
            return null;

        Floor currentFloor = floors[floor];
        while (zChange != 0)
        {
            float diff = zChange;
            if (diff > 1)
                diff = 1;
            if (diff < -1)
                diff = -1;

            zChange -= diff;

            float nextZ = position.Z + diff;

            if ((int)nextZ >= floors.Length)
                return null;

            if (MathF.Floor(nextZ) < 0)
            {
                if (currentFloor.IsTileAtBlocked(new Vector2(position.X, position.Y)))
                    return 0;
                
                return null;
            }

            Vector3 nextPosition = position with { Z = position.Z + diff };
            Floor nextFloor = floors[(int)MathF.Floor(nextZ)];

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

        int startFloor = (int)start.Z;
        int endFloor = (int)end.Z;
        if (startFloor == endFloor)
        {
            var floorIntersect = floors[startFloor].GetIntersection(new Vector2(start.X, start.Y), new Vector2(end.X, end.Y), out var normal);
            if (floorIntersect != null)
            {
                return new BoardIntersectionInfo { IsCeiling = false, Position = new Vector3((Vector2)floorIntersect, startFloor), Normal = new Vector3(normal!.Value, 0) };
            }

            return null;
        }
        
        Vector3 vector = end - start;
        var projection = new Vector2(vector.X, vector.Y);

        int currentFloor = startFloor;
        Vector3 currentStart = start;
        while (currentFloor <= endFloor){

            float heightLeft = (currentFloor + 1) - currentStart.Z;
            Vector2 projectionInCurrentFloor = (projection * heightLeft) / vector.Z;
            var currentEnd = new Vector2(currentStart.X + projectionInCurrentFloor.X, currentStart.Y + projectionInCurrentFloor.Y);
            var intersection = floors[currentFloor].GetIntersection(new Vector2(currentStart.X, currentStart.Y), currentEnd, out var normal);
            if (intersection != null)
                return new BoardIntersectionInfo { IsCeiling = false, Position = new Vector3((Vector2)intersection, currentFloor+1), Normal = new Vector3(normal!.Value, 0) };

            currentStart += new Vector3(projectionInCurrentFloor.X, projectionInCurrentFloor.Y, heightLeft);
            currentFloor++;
            
            if (floors[currentFloor].IsTileAtBlocked(currentEnd))
                return new BoardIntersectionInfo { IsCeiling = true, Position = new Vector3(currentEnd, currentFloor), Normal = new Vector3(0, 0, -1) };
            // this code has NOT been tested
        }

        return null;
    }
}
