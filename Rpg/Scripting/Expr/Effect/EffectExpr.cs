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
    public override void Eval(EvalContext ctx)
    {
    }
}
public sealed class CompositeEffectExpr : EffectExpr
{
    public readonly EffectExpr[] Effects;
    public CompositeEffectExpr(EffectExpr[] effects) => Effects = effects;

    public override void Eval(EvalContext ctx)
    {
        foreach (var effect in Effects)
        {
            effect.Eval(ctx);
        }
    }
}
public sealed class ConditionalEffectExpr : EffectExpr
{
    public readonly ConditionExpr Condition;
    public readonly EffectExpr Effect;

    public ConditionalEffectExpr(ConditionExpr condition, EffectExpr effect)
    {
        Condition = condition;
        Effect = effect;
    }

    public override void Eval(EvalContext ctx)
    {
        if (Condition.Eval(ctx))
        {
            Effect.Eval(ctx);
        }
    }
}
public sealed class ForEachEffectExpr : EffectExpr
{
    public readonly SelectorExpr[] selectors;
    public readonly EffectExpr Effect;
    public ForEachEffectExpr(SelectorExpr[] selectors, EffectExpr effect)
    {
        this.selectors = selectors;
        Effect = effect;
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
}