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

    [ExprOp("body_part_health", "bp_health", Description = "Current health of a body part.")]
    public static BodyPartHealthExpr Op(
        [Doc("Body part to read")] Expr<BodyPart?> part,
        [Doc("Read the part's own health, ignoring whether its parent is dead")] Expr<bool>? standalone = null)
        => new BodyPartHealthExpr(part, standalone);

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

    [ExprOp("bp_max_health", "body_part_max_health", Description = "Maximum health of a body part.")]
    public static BodyPartMaxHealthExpr Op([Doc("Body part to read")] Expr<BodyPart?> part)
        => new BodyPartMaxHealthExpr(part);

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

    [ExprOp("bp_health_percent", "body_part_health_percent", "health_percent_bp",
            Description = "Health of a body part as a fraction of its maximum.")]
    public static BodyPartHealthPercentExpr Op([Doc("Body part to read")] Expr<BodyPart?> part)
        => new BodyPartHealthPercentExpr(part);

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

    [ExprOp("layer_index", "find_layer_index", Description = "Index of a named layer on a body part.")]
    public static Expr<float> Op(
        [Doc("Body part the layer belongs to")] Expr<BodyPart?> target,
        [Doc("Layer name")] Expr<string> name)
        => new LayerIndexExpr(target, name);
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

    [ExprOp("layer_health", Description = "Health of a named layer on a body part.")]
    public static Expr<float> Op(
        [Doc("Body part the layer belongs to")] Expr<BodyPart?> target,
        [Doc("Layer name")] Expr<string> name)
        => new LayerHealthExpr(target, name);
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

    [ExprOp("body_stability", "stability_of_body", "get_body_stability",
            Description = "How stable a body currently is.")]
    public static Expr<float> Op([Doc("Body to read")] Expr<Body?> target)
        => new BodyStabilityExpr(target);
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

    [ExprOp("body_movement_intensity", "movement_intensity_of_body", "get_body_movement_intensity",
            Description = "How intensely a body is currently moving.")]
    public static Expr<float> Op([Doc("Body to read")] Expr<Body?> target)
        => new BodyMovementIntensityExpr(target);
}