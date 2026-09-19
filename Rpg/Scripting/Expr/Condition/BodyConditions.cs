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

    [ExprOp("body_part_alive", "is_body_part_alive", "bp_alive",
            Description = "True while a body part is alive.")]
    public static BodyPartAliveCondition Alive([Doc("Body part to check")] Expr<BodyPart?> part)
        => new BodyPartAliveCondition(part);

    [ExprOp("body_part_dead", "is_body_part_dead", "bp_dead",
            Description = "True once a body part is dead.")]
    public static Expr<bool> Dead([Doc("Body part to check")] Expr<BodyPart?> part)
        => new NotConditionExpr(new BodyPartAliveCondition(part));
    
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

    [ExprOp("body_group_alive", "is_body_group_alive",
            Description = "True while any part of a body group is alive.")]
    public static BodyGroupAliveCondition Op(
        [Doc("Id of the body group to check")] Expr<string> group,
        [Doc("Body the group belongs to")] Expr<Body?> body)
        => new BodyGroupAliveCondition(group, body);

    public override bool Eval(EvalContext ctx)
    {
        return Body.Eval(ctx)?.IsGroupAlive(GroupId.Eval(ctx)) ?? false;
    }
}