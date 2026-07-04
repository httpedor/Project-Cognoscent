using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

namespace Rpg.Scripting;

public abstract class BodyPartCondition : Expr<bool>
{
    public readonly Expr<BodyPart?> BodyPart;
    public BodyPartCondition(Expr<BodyPart?> bodyPart) => BodyPart = bodyPart;
    public BodyPartCondition(Stream stream)
    {
        BodyPart = BaseExpr.Deserialize<Expr<BodyPart?>>(stream);
    }
    
    protected abstract bool Eval(BodyPart part);

    public override bool Eval(EvalContext ctx)
    {
        var part = BodyPart.Eval(ctx);
        if (part == null)
            return false;
        return Eval(part);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        BodyPart.ToBytes(stream);
    }
}

public class BodyPartAliveCondition : BodyPartCondition
{
    public BodyPartAliveCondition(Expr<BodyPart?> bodyPart) : base(bodyPart) { }
    public BodyPartAliveCondition(Stream stream) : base(stream) { }

    protected override bool Eval(BodyPart part) => part.IsAlive;

    [ExprOp(ExprCategory.Condition, "body_part_alive", "is_body_part_alive", "bp_alive")]
    [ExprParam("part", typeof(BodyPart), Required = true, Description = "The body part to check")]
    public static BodyPartAliveCondition CompileAlive(JsonElement json)
    {
        var bodyPart = ExpressionCompiler.Compile<BodyPart>(json.GetProperty("part"));
        return new BodyPartAliveCondition(bodyPart);
    }

    [ExprOp(ExprCategory.Condition, "body_part_dead", "is_body_part_dead", "bp_dead")]
    [ExprParam("part", typeof(BodyPart), Required = true, Description = "The body part to check")]
    public static Expr<bool> CompileDead(JsonElement json)
    {
        var bodyPart = ExpressionCompiler.Compile<BodyPart>(json.GetProperty("part"));
        return new NotConditionExpr(new BodyPartAliveCondition(bodyPart));
    }
    
}

public class BodyGroupAliveCondition : Expr<bool>
{
    public readonly Expr<string> GroupId;
    public readonly Expr<Body?> Body;
    public BodyGroupAliveCondition(Expr<string> groupId, Expr<Body?> body)
    {
        GroupId = groupId;
        Body = body;
    }
    public BodyGroupAliveCondition(Stream stream)
    {
        GroupId = BaseExpr.Deserialize<Expr<string>>(stream);
        Body = BaseExpr.Deserialize<Expr<Body?>>(stream);
    }

    [ExprOp(ExprCategory.Condition, "body_group_alive", "is_body_group_alive")]
    [ExprParam("group", typeof(string), Required = true, Description = "The ID of the body group to check")]
    [ExprParam("body", typeof(Body), Required = true, Description = "The body to check the group on. If not provided, uses the current body in context.")]
    public static BodyGroupAliveCondition Compile(JsonElement json)
    {
        var groupId = ExpressionCompiler.Compile<string>(json.GetProperty("group"));
        var bodyExpr = ExpressionCompiler.Compile<Body?>(json.GetProperty("body"));
        return new BodyGroupAliveCondition(groupId, bodyExpr);
    }

    public override bool Eval(EvalContext ctx)
    {
        return Body.Eval(ctx)?.IsGroupAlive(GroupId.Eval(ctx)) ?? false;
    }
}