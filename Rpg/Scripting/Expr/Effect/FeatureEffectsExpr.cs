using Rpg.Features;

namespace Rpg.Scripting;

public class AddFeatureExpr : EffectExpr
{
    public readonly SelectorExpr Target;
    public readonly string FeatureName;
    public AddFeatureExpr(string featureName, SelectorExpr target)
    {
        FeatureName = featureName;
        Target = target;
    }
    public AddFeatureExpr(Stream stream)
    {
        FeatureName = stream.ReadString();
        Target = (SelectorExpr)BaseExpr.Deserialize(stream);
    }
    public override void Eval(EvalContext ctx)
    {
        var feat = Compendium.GetEntry<Feature>(FeatureName);
        if (feat == null)
            throw new Exception($"Feature '{FeatureName}' not found in Compendium.");
        var entity = Target.Eval(ctx);
        if (entity == null)
            return;
        var feats = entity.Features;
        if (feats == null)
            return;
        feats.AddFeature(feat);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(FeatureName);
        Target.ToBytes(stream);
    }
}
public class RemoveFeatureExpr : EffectExpr
{
    public readonly SelectorExpr Target;
    public readonly string FeatureName;
    public RemoveFeatureExpr(string featureName, SelectorExpr target)
    {
        FeatureName = featureName;
        Target = target;
    }
    public RemoveFeatureExpr(Stream stream)
    {
        FeatureName = stream.ReadString();
        Target = (SelectorExpr)BaseExpr.Deserialize(stream);
    }
    public override void Eval(EvalContext ctx)
    {
        var feat = Compendium.GetEntry<Feature>(FeatureName);
        if (feat == null)
            throw new Exception($"Feature '{FeatureName}' not found in Compendium.");
        var entity = Target.Eval(ctx);
        if (entity == null)
            return;
        var feats = entity.Features;
        if (feats == null)
            return;
        feats.RemoveFeature(feat);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(FeatureName);
        Target.ToBytes(stream);
    }
}

public class AddConditionFeatureExpr : EffectExpr
{
    public readonly SelectorExpr Target;
    public readonly NumberExpr Ticks;
    public readonly string ConditionName;
    public AddConditionFeatureExpr(string conditionName, SelectorExpr target, NumberExpr ticks)
    {
        ConditionName = conditionName;
        Target = target;
        Ticks = ticks;
    }
    public AddConditionFeatureExpr(Stream stream)
    {
        ConditionName = stream.ReadString();
        Target = (SelectorExpr)BaseExpr.Deserialize(stream);
        Ticks = (NumberExpr)BaseExpr.Deserialize(stream);
    }
    public override void Eval(EvalContext ctx)
    {
        var feat = Compendium.GetEntry<Feature>(ConditionName);
        if (feat == null)
            throw new Exception($"Feature '{ConditionName}' not found in Compendium.");
        if (!(feat is ConditionFeature condition))
            throw new Exception($"Feature '{ConditionName}' is not a ConditionFeature.");
        var ticks = (uint)Ticks.Eval(ctx);
        var entity = Target.Eval(ctx);
        if (entity == null)
            return;
        var feats = entity.Features;
        if (feats == null)
            return;
        feats.AddFeature(condition.WithDuration(ticks));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(ConditionName);
        Target.ToBytes(stream);
        Ticks.ToBytes(stream);
    }
}