using System.Text.Json;
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

    [ExprOp(ExprCategory.Number, "stat", "creature_stat", "entity_stat", "entitystat", "creaturestat")]
    [ExprParam("stat", "string", Required = true, Description = "Name of the stat to read")]
    [ExprParam("entity", "selectorExpr", Description = "Entity to read stat from (defaults to caller)")]
    [ExprParam("default", "numberExpr", Description = "Default value if stat not found (defaults to 0)")]
    public static Expr<float> CompileOp(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        Expr<Entity?> entityExpr = obj.TryGetProperty("entity", out var entityElement)
            ? ExpressionCompiler.CompileSelector(entityElement)
            : new CallerSelectorExpr();
        Expr<float> defaultValue = obj.TryGetProperty("default", out var defaultElement)
            ? ExpressionCompiler.CompileNumber(defaultElement)
            : new ConstNumberExpr(0);
        return new StatExpr(statName, entityExpr, defaultValue);
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
        
        return body.GetLocalStat(GroupName, StatName, defaultValue);
    }
}

public sealed class StatMinExpr : Expr<float>
{
    public readonly string StatName;
    public readonly Expr<Entity?> Target;

    public StatMinExpr(string statName, Expr<Entity?> target)
    {
        StatName = statName;
        Target = target;
    }
    public StatMinExpr(Stream stream)
    {
        StatName = stream.ReadString();
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return 0;
        var stats = entity.Stats;
        if (stats == null)
            return 0;
        return stats.GetStat(StatName)?.MinValue ?? 0;
    }
}

public sealed class StatMaxExpr : Expr<float>
{
    public readonly string StatName;
    public readonly Expr<Entity?> Target;

    public StatMaxExpr(string statName, Expr<Entity?> target)
    {
        StatName = statName;
        Target = target;
    }
    public StatMaxExpr(Stream stream)
    {
        StatName = stream.ReadString();
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return 0;
        var stats = entity.Stats;
        if (stats == null)
            return 0;
        return stats.GetStat(StatName)?.MaxValue ?? 0;
    }
}