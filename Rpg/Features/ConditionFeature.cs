using System.Dynamic;
using Rpg.Entities;

namespace Rpg.Features;

public abstract class ConditionFeature : Feature
{
    public string StartTickKey => GetId() + "_startTick";
    protected readonly uint ticks;

    protected ConditionFeature(uint ticks) : base()
    {
        this.ticks = ticks;
    }
    protected ConditionFeature(Stream stream) : base()
    {
        ticks = stream.ReadUInt32();
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteUInt32(ticks);
    }

    public uint GetStartTick(Entity entity)
    {
        var features = entity.Features;
        if (features == null)
            throw new InvalidOperationException($"Entity {entity.Id} does not have FeaturesComponent");
        if (!features.HasFeature(this))
            return uint.MaxValue;
        byte[]? startTickData = features.CustomData.Get(StartTickKey);
        if (startTickData == null)
            return uint.MaxValue;
        
        return BitConverter.ToUInt32(startTickData);
    }
    public uint GetRemainingTicks(Entity entity)
    {
        return ticks - GetTicksSinceStart(entity);
    }
    public uint GetTicksSinceStart(Entity entity)
    {
        return entity.Board.CurrentTick - GetStartTick(entity);
    }

    public override void OnTick(Entity entity)
    {
        base.OnTick(entity);
        if (GetTicksSinceStart(entity) >= ticks)
            entity.Board.RunTaskLater(() => entity.Features?.RemoveFeature(this), 0);
    }

    public override void OnEnable(Entity source)
    {
        base.OnEnable(source);
        source.CustomData!.SetUInt(StartTickKey, source.Board.CurrentTick);
    }
    public override void OnDisable(Entity source)
    {
        base.OnDisable(source);
        source.CustomData!.Remove(StartTickKey);
    }
}