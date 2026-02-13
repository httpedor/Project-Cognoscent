using System.Dynamic;
using Rpg.Entities;
using Rpg.Entities.Components;

namespace Rpg.Features;

public abstract class ConditionFeature : Feature
{
    public string StartTickKey => GetId() + "_startTick";
    protected uint ticks;

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
            throw new InvalidOperationException($"Entity {entity.Id} does not have FeaturesContainer");
        if (!features.HasFeature(this))
            return uint.MaxValue;
        uint? startTick = features.CustomData.GetUInt(StartTickKey);
        if (startTick == null)
            return uint.MaxValue;
        
        return startTick.Value;
    }
    public uint GetRemainingTicks(Entity entity)
    {
        return ticks - GetTicksSinceStart(entity);
    }
    public uint GetTicksSinceStart(Entity entity)
    {
        return entity.Board.CurrentTick - GetStartTick(entity);
    }

    public override void OnTick(FeaturesContainer source)
    {
        base.OnTick(source);
        if (GetTicksSinceStart(source.Entity) >= ticks)
            source.Entity.Board.RunTaskLater(() => source.Entity.Features?.RemoveFeature(this), 0);
    }

    public override void OnEnable(FeaturesContainer source)
    {
        base.OnEnable(source);
        source.CustomData!.SetUInt(StartTickKey, source.Board.CurrentTick);
    }
    public override void OnDisable(FeaturesContainer source)
    {
        base.OnDisable(source);
        source.CustomData.Remove(StartTickKey);
    }

    public ConditionFeature WithDuration(uint newTicks)
    {
        var clone = (ConditionFeature)MemberwiseClone();
        clone.ticks = newTicks;
        return clone;
    }
}