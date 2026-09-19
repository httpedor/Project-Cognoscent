using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

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

    [ExprOp("stat", "creature_stat", "entity_stat", "entitystat", "creaturestat",
            Description = "Reads a named stat from an entity.")]
    public static Expr<float> Op(
        [Doc("Name of the stat to read")] string stat,
        [Doc("Entity to read the stat from; defaults to the caller")] Expr<Entity?>? entity = null,
        [Doc("Value to use when the stat is not present")] Expr<float>? @default = null)
        => new StatExpr(stat, entity ?? new CallerEntityExpr(), @default ?? new ConstNumberExpr(0));
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
    /// <summary>
    /// The body group to read from. This is an expression rather than a constant because callers
    /// legitimately map over a set of groups (<c>parts.map(group_stat('strength', $0, caller))</c>);
    /// when it was read as a constant string those call sites silently looked up a group literally
    /// named "$0" and fell through to the default value.
    /// </summary>
    public readonly Expr<string> GroupName;
    public readonly Expr<float> DefaultValue;
    public readonly Expr<Entity?> Target;

    public GroupStatExpr(string statName, Expr<string> groupName, Expr<Entity?> target, Expr<float> defaultValue)
    {
        StatName = statName;
        GroupName = groupName;
        DefaultValue = defaultValue;
        Target = target;
    }
    public GroupStatExpr(Stream stream)
    {
        StatName = stream.ReadString();
        GroupName = BaseExpr.Deserialize<Expr<string>>(stream);
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
        DefaultValue = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    [ExprOp("group_stat", "stat_from_group", "local_stat", "stat_local",
            Description = "Reads a named stat scoped to a body group.")]
    public static Expr<float> Op(
        [Doc("Name of the stat to read")] string stat,
        [Doc("Body group to read the stat from")] Expr<string> group,
        [Doc("Entity to read the stat from; defaults to the caller")] Expr<Entity?>? entity = null,
        [Doc("Value to use when the stat is not present")] Expr<float>? @default = null)
        => new GroupStatExpr(stat, group, entity ?? new CallerEntityExpr(), @default ?? new ConstNumberExpr(0));

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(StatName);
        GroupName.ToBytes(stream);
        Target.ToBytes(stream);
        DefaultValue.ToBytes(stream);
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
        
        return body.GetLocalStat(GroupName.Eval(ctx), StatName, defaultValue);
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

public sealed class ArraySizeExpr : Expr<float>
{
    public readonly ArrayExpr<object> Array;

    public ArraySizeExpr(ArrayExpr<object> array)
    {
        Array = array;
    }
    public ArraySizeExpr(Stream stream)
    {
        Array = (ArrayExpr<object>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var array = Array.Eval(ctx);
        return array.Count();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Array.ToBytes(stream);
    }

    [ExprOp("array_size", "length_of_array", "array_length", "array_count", "count_array", "len", "count",
            Description = "Number of elements in an array.")]
    public static Expr<float> Op([Doc("The array to measure")] ArrayExpr<object> array)
        => new ArraySizeExpr(array);
}