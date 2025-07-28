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

    public uint GetStartTick(IFeatureSource entity)
    {
        if (!entity.HasFeature(this))
            return uint.MaxValue;
        byte[]? startTickData = entity.GetCustomData(StartTickKey);
        if (startTickData == null)
            return uint.MaxValue;
        
        return BitConverter.ToUInt32(startTickData);
    }
    public uint GetRemainingTicks(IFeatureSource entity)
    {
        return ticks - GetTicksSinceStart(entity);
    }
    public uint GetTicksSinceStart(IFeatureSource entity)
    {
        return entity.Board.CurrentTick - GetStartTick(entity);
    }

    public override void OnTick(IFeatureSource entity)
    {
        base.OnTick(entity);
        if (GetTicksSinceStart(entity) >= ticks)
            entity.Board.RunTaskLater(() => entity.RemoveFeature(this), 0);
    }

    public override void Enable(IFeatureSource source)
    {
        base.Enable(source);
        source.SetCustomData(StartTickKey, BitConverter.GetBytes(source.Board.CurrentTick));
    }
    public override void Disable(IFeatureSource source)
    {
        base.Disable(source);
        source.RemoveCustomData(StartTickKey);
    }
}