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

    [ExprOp(ExprCategory.String, "bodypart_group", "bp_group")]
    [ExprParam("target", typeof(BodyPart), Description = "Body part to get the group of (defaults to caller's body part)")]
    public static Expr<string> CompileOp(JsonElement obj)
    {
        var target = ExpressionCompiler.Compile<BodyPart>(obj.GetProperty("target"));
        return new GetBodypartGroupExpr(target);
    }
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

    [ExprOp(ExprCategory.String, "entity_name", "ent_name")]
    [ExprParam("target", typeof(Entity), Description = "Entity to get the name of (defaults to caller)")]
    public static Expr<string> CompileOp(JsonElement obj)
    {
        var target = ExpressionCompiler.Compile<Entity>(obj.GetProperty("target"));
        return new GetEntityNameExpr(target);
    }
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

    [ExprOp(ExprCategory.String, "resting_posture", "rest_posture")]
    [ExprParam("target", typeof(Body), Description = "Body to get the resting posture of (defaults to caller's body)")]
    public static Expr<string> CompileOp(JsonElement obj)
    {
        var target = ExpressionCompiler.Compile<Body>(obj.GetProperty("target"));
        return new BodyRestingPostureNameExpr(target);
    }
}