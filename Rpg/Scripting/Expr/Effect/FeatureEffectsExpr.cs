using System.Text.Json;
using Rpg.Entities;
using Rpg.Features;

namespace Rpg.Scripting;

public class AddFeatureEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<Feature?> Feature;
    public AddFeatureEffect(Expr<Feature?> feature, Expr<Entity?> target)
    {
        Feature = feature;
        Target = target;
    }

    /// <summary>Giving a duration produces a timed condition rather than a permanent feature.</summary>
    [ExprOp("add_feature", "addfeature", "add_feat", "add_condition", "addcondition",
            Description = "Adds a feature to an entity, optionally for a limited time.")]
    public static EffectExpr Op(
        [Doc("Feature compendium id")] Expr<Feature?> feature,
        [Doc("Entity to add it to")] Expr<Entity?> target,
        [Doc("Duration in ticks; omit for a permanent feature")] Expr<float>? ticks = null)
        => ticks == null
            ? new AddFeatureEffect(feature, target)
            : new AddConditionEffect(feature, target, ticks);
    public AddFeatureEffect(Stream stream)
    {
        Feature = (Expr<Feature?>)BaseExpr.Deserialize(stream);
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
    }
    public override void EvalEffect(EvalContext ctx)
    {
        var feat = Feature.Eval(ctx);
        if (feat == null)
            throw new Exception($"Feature '{Feature}' not found in Compendium.");
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
        Feature.ToBytes(stream);
        Target.ToBytes(stream);
    }
}
public class RemoveFeatureEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<Feature> Feature;
    public RemoveFeatureEffect(Expr<Feature> feature, Expr<Entity?> target)
    {
        Feature = feature;
        Target = target;
    }

    [ExprOp("remove_feature", "removefeature", "remove_feat",
            Description = "Removes a feature from an entity.")]
    public static EffectExpr Op(
        [Doc("Feature compendium id to remove")] Expr<Feature> feature,
        [Doc("Entity to remove it from")] Expr<Entity?> target)
        => new RemoveFeatureEffect(feature, target);
    public RemoveFeatureEffect(Stream stream)
    {
        Feature = (Expr<Feature>)BaseExpr.Deserialize(stream);
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
    }
    public override void EvalEffect(EvalContext ctx)
    {
        var feat = Feature.Eval(ctx);
        if (feat == null)
            throw new Exception($"Feature '{Feature}' not found in Compendium.");
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
        Feature.ToBytes(stream);
        Target.ToBytes(stream);
    }
}

public class AddConditionEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<float> Seconds;
    public readonly Expr<Feature> Condition;
    public AddConditionEffect(Expr<Feature> condition, Expr<Entity?> target, Expr<float> seconds)
    {
        Condition = condition;
        Target = target;
        Seconds = seconds;
    }
    public AddConditionEffect(Stream stream)
    {
        Condition = (Expr<Feature>)BaseExpr.Deserialize(stream);
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
        Seconds = (Expr<float>)BaseExpr.Deserialize(stream);
    }
    public override void EvalEffect(EvalContext ctx)
    {
        var feat = Condition.Eval(ctx);
        if (feat == null)
            throw new Exception($"Feature '{Condition}' not found in Compendium.");
        if (!(feat is ConditionFeature condition))
            throw new Exception($"Feature '{Condition}' is not a ConditionFeature.");
        var seconds = Seconds.Eval(ctx);
        var entity = Target.Eval(ctx);
        if (entity == null)
            return;
        var feats = entity.Features;
        if (feats == null)
            return;
        feats.AddCondition(condition, seconds);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Condition.ToBytes(stream);
        Target.ToBytes(stream);
        Seconds.ToBytes(stream);
    }
}
