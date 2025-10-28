using System.Dynamic;

namespace Rpg;

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

    public uint GetStartTick(IFeatureContainer entity)
    {
        if (!entity.HasFeature(this))
            return uint.MaxValue;
        byte[]? startTickData = entity.GetCustomData(StartTickKey);
        if (startTickData == null)
            return uint.MaxValue;
        
        return BitConverter.ToUInt32(startTickData);
    }
    public uint GetRemainingTicks(IFeatureContainer entity)
    {
        return ticks - GetTicksSinceStart(entity);
    }
    public uint GetTicksSinceStart(IFeatureContainer entity)
    {
        return entity.Board.CurrentTick - GetStartTick(entity);
    }

    public override void OnTick(IFeatureContainer entity)
    {
        base.OnTick(entity);
        if (GetTicksSinceStart(entity) >= ticks)
            entity.Board.RunTaskLater(() => entity.RemoveFeature(this), 0);
    }

    public override void OnEnable(IFeatureContainer source)
    {
        base.OnEnable(source);
        source.SetCustomData(StartTickKey, source.Board.CurrentTick);
    }
    public override void OnDisable(IFeatureContainer source)
    {
        base.OnDisable(source);
        source.RemoveCustomData(StartTickKey);
    }
}