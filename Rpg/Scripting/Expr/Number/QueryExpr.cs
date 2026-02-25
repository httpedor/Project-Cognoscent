using Rpg.Entities;

namespace Rpg.Scripting;

public sealed class StatExpr : Expr<float>
{
    public readonly string StatName;
    public readonly Expr<float> DefaultValue;
    public readonly Expr<Entity?> Target;

    public StatExpr(string statName, Expr<Entity?> target, Expr<float> defaultValue)
    {
        StatName = statName;
        DefaultValue = defaultValue;
        Target = target;
    }
    public StatExpr(Stream stream)
    {
        StatName = stream.ReadString();
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
        DefaultValue = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);

        var defaultValue = DefaultValue.Eval(ctx);
        if (entity == null)
            return defaultValue;
        var stats = entity.Stats;
        if (stats == null)
            return defaultValue;
        return stats.GetStatValue(StatName, defaultValue);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(StatName);
        Target.ToBytes(stream);
        DefaultValue.ToBytes(stream);
    }
}
public sealed class GroupStatExpr : Expr<float>
{
    public readonly string StatName;
    public readonly string GroupName;
    public readonly Expr<float> DefaultValue;
    public readonly Expr<Entity?> Target;

    public GroupStatExpr(string statName, string groupName, Expr<Entity?> target, Expr<float> defaultValue)
    {
        StatName = statName;
        GroupName = groupName;
        DefaultValue = defaultValue;
        Target = target;
    }
    public GroupStatExpr(Stream stream)
    {
        StatName = stream.ReadString();
        GroupName = stream.ReadString();
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
        DefaultValue = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);

        var defaultValue = DefaultValue.Eval(ctx);
        if (entity == null)
            return defaultValue;
        var body = entity.Body;
        if (body == null)
        {
            body = entity.BodyPart?.Body;
            if (body == null)
                return defaultValue;
        }
        
        return body.GetStatByGroup(GroupName, StatName, defaultValue);
    }
}