using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

namespace Rpg.Scripting;

public sealed class BodyPartHealthExpr : Expr<float>
{
    public readonly Expr<BodyPart?> BodyPart;
    public readonly Expr<bool>? Standalone;
    public BodyPartHealthExpr(Expr<BodyPart?> bodyPart, Expr<bool>? standalone = null)
    {
        BodyPart = bodyPart;
        Standalone = standalone;
    }

    [ExprOp(ExprCategory.Number, "body_part_health", "bp_health")]
    [ExprParam("part", typeof(BodyPart), Required = true, Description = "The body part to get the health of")]
    [ExprParam("standalone", typeof(bool), Required = false, Description = "Whether to get the health of the body part alone, without counting its parents. If false or not provided, the health of the body part will be 0 if the parent is dead.")]
    public static BodyPartHealthExpr Compile(JsonElement json)
    {
        var bodyPart = ExpressionCompiler.Compile<BodyPart?>(json.GetProperty("part"));
        var standalone = json.TryGetProperty("standalone", out var standaloneEl) ? ExpressionCompiler.Compile<bool>(standaloneEl) : null;
        return new BodyPartHealthExpr(bodyPart, standalone);
    }

    public override float Eval(EvalContext ctx)
    {
        var part = BodyPart.Eval(ctx);
        if (part == null)
            return 0;
        if (Standalone != null && Standalone.Eval(ctx))
            return (float)part.HealthStandalone;
        return (float)part.Health;
    }
}
public sealed class BodyPartMaxHealthExpr : Expr<float>
{
    public readonly Expr<BodyPart?> BodyPart;
    public BodyPartMaxHealthExpr(Expr<BodyPart?> bodyPart)
    {
        BodyPart = bodyPart;
    }

    [ExprOp(ExprCategory.Number, "bp_max_health", "body_part_max_health")]
    [ExprParam("part", typeof(BodyPart), Required = true, Description = "The body part to get the max health of")]
    public static BodyPartMaxHealthExpr Compile(JsonElement json)
    {
        var bodyPart = ExpressionCompiler.Compile<BodyPart?>(json.GetProperty("part"));
        return new BodyPartMaxHealthExpr(bodyPart);
    }

    public override float Eval(EvalContext ctx)
    {
        var part = BodyPart.Eval(ctx);
        if (part == null)
            return 0;
        return (float)part.MaxHealth;
    }
}
public sealed class BodyPartHealthPercentExpr : Expr<float>
{
    public readonly Expr<BodyPart?> BodyPart;
    public BodyPartHealthPercentExpr(Expr<BodyPart?> bodyPart)
    {
        BodyPart = bodyPart;
    }

    [ExprOp(ExprCategory.Number, "bp_health_percent", "body_part_health_percent", "health_percent_bp")]
    [ExprParam("part", typeof(BodyPart), Required = true, Description = "The body part to get the health percent of")]
    public static BodyPartHealthPercentExpr Compile(JsonElement json)
    {
        var bodyPart = ExpressionCompiler.Compile<BodyPart?>(json.GetProperty("part"));
        return new BodyPartHealthPercentExpr(bodyPart);
    }

    public override float Eval(EvalContext ctx)
    {
        var part = BodyPart.Eval(ctx);
        if (part == null || part.MaxHealth <= 0)
            return 0;
        return (float)(part.Health / part.MaxHealth);
    }
}

public sealed class LayerIndexExpr : Expr<float>
{
    public Expr<BodyPart?> Target;
    public Expr<string> LayerName;

    public LayerIndexExpr(Expr<BodyPart?> target, Expr<string> layerName)
    {
        Target = target;
        LayerName = layerName;
    }
    public LayerIndexExpr(Stream stream)
    {
        Target = BaseExpr.Deserialize<Expr<BodyPart?>>(stream);
        LayerName = BaseExpr.Deserialize<Expr<string>>(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var target = Target.Eval(ctx);
        if (target == null)
            return -1;
        return target.FindLayerIndex(LayerName.Eval(ctx));
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Target.ToBytes(stream);
        LayerName.ToBytes(stream);
    }

    [ExprOp(ExprCategory.Number, "layer_index", "find_layer_index")]
    [ExprParam("target", typeof(BodyPart), Required = true, Description = "The layer's bodypart")]
    [ExprParam("name", typeof(string), Required = true, Description = "The layer's name")]
    public static Expr<float> CompileOp(JsonElement json)
        => new LayerIndexExpr(ExpressionCompiler.Compile<BodyPart?>(json.GetProperty("target")), ExpressionCompiler.Compile<string>(json.GetProperty("name")));
}

public sealed class LayerHealthExpr : Expr<float>
{
    public Expr<BodyPart?> Target;
    public Expr<string> LayerName;

    public LayerHealthExpr(Expr<BodyPart?> target, Expr<string> layerName)
    {
        Target = target;
        LayerName = layerName;
    }
    public LayerHealthExpr(Stream stream)
    {
        Target = BaseExpr.Deserialize<Expr<BodyPart?>>(stream);
        LayerName = BaseExpr.Deserialize<Expr<string>>(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var bp = Target.Eval(ctx);
        if (bp == null)
            return -1;
        var layer = bp.FindLayer(LayerName.Eval(ctx));
        if (layer == null)
            return -1;
        return (float)layer.Health;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Target.ToBytes(stream);
        LayerName.ToBytes(stream);
    }

    [ExprOp(ExprCategory.Number, "layer_health")]
    [ExprParam("target", typeof(BodyPart), Required = true, Description = "The layer's bodypart")]
    [ExprParam("name", typeof(string), Required = true, Description = "The layer's name")]
    public static Expr<float> CompileOp(JsonElement json)
        => new LayerHealthExpr(ExpressionCompiler.Compile<BodyPart?>(json.GetProperty("target")), ExpressionCompiler.Compile<string>(json.GetProperty("name")));
}
public sealed class BodyStabilityExpr : Expr<float>
{
    public Expr<Body?> Target;
    public BodyStabilityExpr(Expr<Body?> target)
    {
        Target = target;
    }
    public BodyStabilityExpr(Stream stream)
    {
        Target = BaseExpr.Deserialize<Expr<Body?>>(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var body = Target.Eval(ctx);
        if (body == null)
            return 0;
        return body.Stability;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Target.ToBytes(stream);
    }

    [ExprOp(ExprCategory.Number, "body_stability", "stability_of_body", "get_body_stability")]
    [ExprParam("target", typeof(Body), Required = true, Description = "The body to get the stability of")]
    public static Expr<float> CompileOp(JsonElement json)
        => new BodyStabilityExpr(ExpressionCompiler.Compile<Body?>(json.GetProperty("target")));
}

public sealed class BodyMovementIntensityExpr : Expr<float>
{
    public Expr<Body?> Target;
    public BodyMovementIntensityExpr(Expr<Body?> target)
    {
        Target = target;
    }
    public BodyMovementIntensityExpr(Stream stream)
    {
        Target = BaseExpr.Deserialize<Expr<Body?>>(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var body = Target.Eval(ctx);
        if (body == null)
            return 0;
        return body.MovementIntensity;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Target.ToBytes(stream);
    }

    [ExprOp(ExprCategory.Number, "body_movement_intensity", "movement_intensity_of_body", "get_body_movement_intensity")]
    [ExprParam("target", typeof(Body), Required = true, Description = "The body to get the movement intensity of")]
    public static Expr<float> CompileOp(JsonElement json)
        => new BodyMovementIntensityExpr(ExpressionCompiler.Compile<Body?>(json.GetProperty("target")));
}