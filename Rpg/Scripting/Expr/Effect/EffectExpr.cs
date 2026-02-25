using Rpg.Entities;

namespace Rpg.Scripting;

public abstract class EffectExpr : BaseExpr
{
    public abstract void Eval(EvalContext ctx);
    public void Eval(Entity? entity, Entity? caller, Entity? targetPart = null)
    {
        Eval(new EvalContext { Target = entity, Caller = caller, TargetPart = targetPart, Board = entity?.Board });
    }
    public void Eval(Entity entity)
    {
        Eval(entity, entity, null);
    }
}
public sealed class NoEffectExpr : EffectExpr
{
    public NoEffectExpr() {}
    public NoEffectExpr(Stream stream) {}
    public override void Eval(EvalContext ctx)
    {
    }
}
public sealed class CompositeEffectExpr : EffectExpr
{
    public readonly EffectExpr[] Effects;
    public CompositeEffectExpr(EffectExpr[] effects) => Effects = effects;
    public CompositeEffectExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Effects = new EffectExpr[length];
        for (int i = 0; i < length; i++)
            Effects[i] = (EffectExpr)BaseExpr.Deserialize(stream);
    }
    public override void Eval(EvalContext ctx)
    {
        foreach (var effect in Effects)
        {
            effect.Eval(ctx);
        }
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Effects.Length);
        foreach (var effect in Effects)
            effect.ToBytes(stream);
    }
}
public sealed class ConditionalEffectExpr : EffectExpr
{
    public readonly Expr<bool> Condition;
    public readonly EffectExpr Effect;

    public ConditionalEffectExpr(Expr<bool> condition, EffectExpr effect)
    {
        Condition = condition;
        Effect = effect;
    }
    public ConditionalEffectExpr(Stream stream)
    {
        Condition = (Expr<bool>)BaseExpr.Deserialize(stream);
        Effect = (EffectExpr)BaseExpr.Deserialize(stream);
    }

    public override void Eval(EvalContext ctx)
    {
        if (Condition.Eval(ctx))
        {
            Effect.Eval(ctx);
        }
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Condition.ToBytes(stream);
        Effect.ToBytes(stream);
    }
}
public sealed class ForEachEffectExpr : EffectExpr
{
    public readonly Expr<Entity?>[] selectors;
    public readonly EffectExpr Effect;
    public ForEachEffectExpr(Expr<Entity?>[] selectors, EffectExpr effect)
    {
        this.selectors = selectors;
        Effect = effect;
    }
    public ForEachEffectExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        selectors = new Expr<Entity?>[length];
        for (int i = 0; i < length; i++)
            selectors[i] = (Expr<Entity?>)BaseExpr.Deserialize(stream);
        Effect = (EffectExpr)BaseExpr.Deserialize(stream);
    }

    public override void Eval(EvalContext ctx)
    {
        foreach (var selector in selectors)
        {
            var entity = selector.Eval(ctx);
            if (entity == null)
                continue;
            Effect.Eval(ctx.WithTarget(entity));
        }
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(selectors.Length);
        foreach (var selector in selectors)
            selector.ToBytes(stream);
        Effect.ToBytes(stream);
    }
}