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

    /// <summary>
    /// Compiles add_feature / add_condition effect from JSON.
    /// If "ticks" is present, creates an AddConditionEffect instead.
    /// </summary>
    [ExprOp(ExprCategory.Effect, "add_feature", "addfeature", "add_feat", "add_condition", "addcondition")]
    [ExprParam("feature", typeof(string), Required = true, Description = "Compendium feature ID")]
    [ExprParam("target", typeof(Entity), Required = true)]
    [ExprParam("ticks", typeof(float), Description = "Duration in ticks (creates a timed condition)")]
    public static EffectExpr CompileOp(JsonElement obj)
    {
        var feature = ExpressionCompiler.Compile<Feature?>(obj.GetProperty("feature"));
        var selector = ExpressionCompiler.Compile<Entity?>(obj.GetProperty("target"));
        if (obj.TryGetProperty("ticks", out var ticksElement))
        {
            var ticks = ExpressionCompiler.Compile<float>(ticksElement);
            return new AddConditionEffect(feature, selector, ticks);
        }
        return new AddFeatureEffect(feature, selector);
    }
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

    [ExprOp(ExprCategory.Effect, "remove_feature", "removefeature", "remove_feat")]
    [ExprParam("feature", typeof(string), Required = true, Description = "Compendium feature ID to remove")]
    [ExprParam("target", typeof(Entity), Required = true)]
    public static EffectExpr CompileOp(JsonElement obj)
        => new RemoveFeatureEffect(
            ExpressionCompiler.Compile<Feature>(obj.GetProperty("feature")),
            ExpressionCompiler.Compile<Entity>(obj.GetProperty("target")));
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
