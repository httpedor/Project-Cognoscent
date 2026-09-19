using System.Linq;
using System.Text.Json;
using Rpg.Entities;

namespace Rpg.Scripting;

public sealed class NoComponentExpr : Expr<Component?>
{
    public NoComponentExpr() { }
    public NoComponentExpr(Stream stream) { }

    public override Component? Eval(EvalContext ctx)
    {
        return null;
    }
}

public abstract class ComponentExpr<T> : Expr<Component?> where T : Component
{
    public abstract T? EvalComponent(EvalContext ctx);
    public override Component? Eval(EvalContext ctx)
    {
        return EvalComponent(ctx);
    }
}

public class TargetComponentExpr : Expr<Component?>
{
    public TargetComponentExpr()
    {
    }

    [ExprOp("target", "component_target", "target_component",
            Description = "The component the script is acting on.")]
    public static Expr<Component?> Op() => new TargetComponentExpr();
    public TargetComponentExpr(Stream stream)
    {
    }
    public override Component? Eval(EvalContext ctx)
    {
        return ctx.TargetComponent;
    }
}

public sealed class EntityToComponentExpr<T> : ComponentExpr<T> where T : Component
{
    private readonly Expr<Entity?> inner;

    public EntityToComponentExpr(Expr<Entity?> inner)
    {
        this.inner = inner;
    }

    public EntityToComponentExpr(Stream stream)
    {
        inner = (Expr<Entity?>)BaseExpr.Deserialize(stream);
    }

    public override T? EvalComponent(EvalContext ctx)
    {
        var entity = inner.Eval(ctx);
        if (entity == null)
            return null;

        return entity.GetComponent<T>();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        inner.ToBytes(stream);
    }
}