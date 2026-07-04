using System.Dynamic;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Scripting;

namespace Rpg.Features;

public class ConditionFeature : Feature
{
    public string StartTickKey => Id + "_startTick";
    public string EndTickKey => Id + "_endTick";

    public Expr<float> DefaultDuration { get; init; }

    public ConditionFeature(string id, string name, string description, string icon) : base(id, name, description, icon)
    {
        DefaultDuration = new ConstNumberExpr(1);
    }

    public uint GetStartTick(FeaturesContainer source)
    {
        if (!source.HasFeature(this))
            return uint.MaxValue;
        uint? startTick = source.CustomData.GetUInt(StartTickKey);
        if (startTick == null)
            return uint.MaxValue;
        
        return startTick.Value;
    }
    public uint GetEndTick(FeaturesContainer source)
    {
        if (!source.HasFeature(this))
            return uint.MaxValue;
        uint? endTick = source.CustomData.GetUInt(EndTickKey);
        if (endTick == null)
            return uint.MaxValue;
        
        return endTick.Value;
    }
    public long GetRemainingTicks(FeaturesContainer source)
    {
        return (long)GetEndTick(source) - (long)source.Board.CurrentTick;
    }
    public uint GetTicksSinceStart(FeaturesContainer source)
    {
        return source.Board.CurrentTick - GetStartTick(source);
    }

    public override void OnTick(FeaturesContainer source)
    {
        base.OnTick(source);
        if (GetRemainingTicks(source) <= 0)
            source.Entity.Board.RunTaskLater(() => source.Entity.Features?.RemoveFeature(this), 0);
    }
    public override void OnRemoved(FeaturesContainer source)
    {
        base.OnRemoved(source);
        source.CustomData.Remove(StartTickKey);
        source.CustomData.Remove(EndTickKey);
    }
    public override void OnAdded(FeaturesContainer source)
    {
        base.OnAdded(source);
        source.CustomData?.SetUInt(StartTickKey, source.Board.CurrentTick);
        source.CustomData?.SetUInt(EndTickKey, source.Board.CurrentTick + (uint)(DefaultDuration.Eval(source.Entity) * Physics.TicksPerSecond));
    }
}