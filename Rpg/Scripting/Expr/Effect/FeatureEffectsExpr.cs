using Rpg.Entities;
using Rpg.Features;

namespace Rpg.Scripting;

public class AddFeatureExpr : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly string FeatureName;
    public AddFeatureExpr(string featureName, Expr<Entity?> target)
    {
        FeatureName = featureName;
        Target = target;
    }
    public AddFeatureExpr(Stream stream)
    {
        FeatureName = stream.ReadString();
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
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
    public readonly Expr<Entity?> Target;
    public readonly string FeatureName;
    public RemoveFeatureExpr(string featureName, Expr<Entity?> target)
    {
        FeatureName = featureName;
        Target = target;
    }
    public RemoveFeatureExpr(Stream stream)
    {
        FeatureName = stream.ReadString();
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
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
    public readonly Expr<Entity?> Target;
    public readonly Expr<float> Ticks;
    public readonly string ConditionName;
    public AddConditionFeatureExpr(string conditionName, Expr<Entity?> target, Expr<float> ticks)
    {
        ConditionName = conditionName;
        Target = target;
        Ticks = ticks;
    }
    public AddConditionFeatureExpr(Stream stream)
    {
        ConditionName = stream.ReadString();
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
        Ticks = (Expr<float>)BaseExpr.Deserialize(stream);
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