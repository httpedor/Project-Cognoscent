using Rpg.Entities;
using Rpg.Features;

namespace Rpg.Scripting;

public class AddFeatureEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly CompendiumEntryExpr<Feature> FeatureName;
    public AddFeatureEffect(CompendiumEntryExpr<Feature> feature, Expr<Entity?> target)
    {
        FeatureName = feature;
        Target = target;
    }
    public AddFeatureEffect(Stream stream)
    {
        FeatureName = (CompendiumEntryExpr<Feature>)BaseExpr.Deserialize(stream);
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
    }
    public override void Eval(EvalContext ctx)
    {
        var feat = FeatureName.Eval(ctx);
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
        FeatureName.ToBytes(stream);
        Target.ToBytes(stream);
    }
}
public class RemoveFeatureEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly CompendiumEntryExpr<Feature> FeatureName;
    public RemoveFeatureEffect(CompendiumEntryExpr<Feature> featureName, Expr<Entity?> target)
    {
        FeatureName = featureName;
        Target = target;
    }
    public RemoveFeatureEffect(Stream stream)
    {
        FeatureName = (CompendiumEntryExpr<Feature>)BaseExpr.Deserialize(stream);
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
    }
    public override void Eval(EvalContext ctx)
    {
        var feat = FeatureName.Eval(ctx);
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
        FeatureName.ToBytes(stream);
        Target.ToBytes(stream);
    }
}

public class AddConditionEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<float> Ticks;
    public readonly string ConditionName;
    public AddConditionEffect(string conditionName, Expr<Entity?> target, Expr<float> ticks)
    {
        ConditionName = conditionName;
        Target = target;
        Ticks = ticks;
    }
    public AddConditionEffect(Stream stream)
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
