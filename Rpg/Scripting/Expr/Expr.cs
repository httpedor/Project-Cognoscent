using Rpg.Entities;

namespace Rpg.Scripting;
public abstract class BaseExpr
{
}
public abstract class Expr<T> : BaseExpr
{
    public abstract T Eval(EvalContext ctx);

    public T Eval(Entity? target, Entity? source, Entity? bpTarget = null)
    {
        return Eval(new EvalContext()
        {
            Target = target,
            Caller = source,
            TargetPart = bpTarget,
            Board = target?.Board
        });
    }
    public T Eval(Entity target)
    {
        return Eval(target, target, null);
    }
}