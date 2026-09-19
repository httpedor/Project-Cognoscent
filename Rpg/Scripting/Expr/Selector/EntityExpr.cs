using System.Text.Json;
using Rpg.Entities;

namespace Rpg.Scripting;

public class CallerEntityExpr : Expr<Entity?>
{
    public CallerEntityExpr(){}
    public CallerEntityExpr(Stream stream){}

    [ExprOp("caller", "self", Description = "The entity running the script.")]
    public static Expr<Entity?> Op() => new CallerEntityExpr();

    public override Entity? Eval(EvalContext ctx)
    {
        return ctx.Caller;
    }
}
public class TargetEntityExpr : Expr<Entity?>
{
    public TargetEntityExpr(){}
    public TargetEntityExpr(Stream stream){}

    [ExprOp("target", Description = "The entity the script is acting on.")]
    public static Expr<Entity?> Op() => new TargetEntityExpr();

    public override Entity? Eval(EvalContext ctx)
    {
        return ctx.Target;
    }
}
public class TargetComponentEntityExpr : Expr<Entity?>
{
    public TargetComponentEntityExpr(){}
    public TargetComponentEntityExpr(Stream stream){}

    [ExprOp("target_component_entity", Description = "The entity owning the targeted component.")]
    public static Expr<Entity?> Op() => new TargetComponentEntityExpr();

    public override Entity? Eval(EvalContext ctx)
    {
        return ctx.TargetComponent?.Entity;
    }
}
public class ComponentEntityExpr : Expr<Entity?>
{
    public readonly Expr<Component?> Component;
    public ComponentEntityExpr(Expr<Component?> component)
    {
        Component = component;
    }
    public ComponentEntityExpr(Stream stream)
    {
        Component = BaseExpr.Deserialize<Expr<Component?>>(stream);
    }

    [ExprOp("component_entity", "entity_from_component", "entity_of_component",
            Description = "The entity that owns a component.")]
    public static Expr<Entity?> Op([Doc("Component to read the owner of")] Expr<Component?> component)
        => new ComponentEntityExpr(component);

    public override Entity? Eval(EvalContext ctx)
    {
        var component = Component.Eval(ctx);
        return component?.Entity;
    }
}
public class NoEntityExpr : Expr<Entity?>
{
    public NoEntityExpr(){}
    public NoEntityExpr(Stream stream){}
    public override Entity? Eval(EvalContext ctx)
    {
        return null;
    }
}
public sealed class AllEntitiesExpr : ArrayExpr<Entity>
{
    public AllEntitiesExpr() {}
    public AllEntitiesExpr(Stream stream) {}

    [ExprOp("all_entities", Description = "Every entity on the board.")]
    public static ArrayExpr<Entity> Op() => new AllEntitiesExpr();

    public override IEnumerable<Entity> Eval(EvalContext ctx)
    {
        return ctx.Board?.GetEntities() ?? Array.Empty<Entity>();
    }
}
public sealed class AllEntitiesWithComponentExpr : ArrayExpr<Entity>
{
    public readonly Expr<uint> ComponentType;
    public AllEntitiesWithComponentExpr(Expr<uint> componentType)
    {
        ComponentType = componentType;
    }
    public AllEntitiesWithComponentExpr(Stream stream)
    {
        ComponentType = BaseExpr.Deserialize<Expr<uint>>(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        ComponentType.ToBytes(stream);
    }

    [ExprOp("all_entities_with_component", Description = "Every board entity carrying a component type.")]
    public static ArrayExpr<Entity> Op(
        [Doc("Id of the component type entities must carry")] Expr<uint> component_type)
        => new AllEntitiesWithComponentExpr(component_type);

    public override IEnumerable<Entity> Eval(EvalContext ctx)
    {
        uint componentType = ComponentType.Eval(ctx);
        return ctx.Board?.GetEntitiesWithComponent(componentType) ?? Array.Empty<Entity>();
    }
}