using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

namespace Rpg.Scripting;

public sealed class GetBodypartGroupExpr : Expr<string>
{
    public readonly Expr<BodyPart?> Target;
    public GetBodypartGroupExpr(Expr<BodyPart?> target)
    {
        Target = target;
    }
    public GetBodypartGroupExpr(Stream stream)
    {
        Target = (Expr<BodyPart?>)BaseExpr.Deserialize(stream);
    }

    public override string Eval(EvalContext ctx)
    {
        var bodyPart = Target.Eval(ctx);
        if (bodyPart == null)
            return "null";
        return bodyPart.Group;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Target.ToBytes(stream);
    }

    [ExprOp("bodypart_group", "bp_group", Description = "The group a body part belongs to.")]
    public static Expr<string> Op([Doc("Body part to read the group of")] Expr<BodyPart?> target)
        => new GetBodypartGroupExpr(target);
}
public sealed class GetEntityNameExpr : Expr<string>
{
    public readonly Expr<Entity> Target;
    public GetEntityNameExpr(Expr<Entity> target)
    {
        Target = target;
    }
    public GetEntityNameExpr(Stream stream)
    {
        Target = (Expr<Entity>)BaseExpr.Deserialize(stream);
    }

    public override string Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return "null";
        return entity.Name;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Target.ToBytes(stream);
    }

    [ExprOp("entity_name", "ent_name", Description = "The display name of an entity.")]
    public static Expr<string> Op([Doc("Entity to read the name of")] Expr<Entity> target)
        => new GetEntityNameExpr(target);
}
public sealed class BodyRestingPostureNameExpr : Expr<string>
{
    public readonly Expr<Body> Target;
    public BodyRestingPostureNameExpr(Expr<Body> target)
    {
        Target = target;
    }
    public BodyRestingPostureNameExpr(Stream stream)
    {
        Target = (Expr<Body>)BaseExpr.Deserialize(stream);
    }

    public override string Eval(EvalContext ctx)
    {
        var body = Target.Eval(ctx);
        if (body == null)
            return "null";
        var posture = body.Model.RestingPosture;
        return posture?.ToString() ?? "null";
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Target.ToBytes(stream);
    }

    [ExprOp("resting_posture", "rest_posture", Description = "The name of a body's resting posture.")]
    public static Expr<string> Op([Doc("Body to read the resting posture of")] Expr<Body> target)
        => new BodyRestingPostureNameExpr(target);
}