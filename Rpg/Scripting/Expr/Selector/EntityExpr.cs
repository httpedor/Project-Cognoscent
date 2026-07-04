using System.Text.Json;
using Rpg.Entities;

namespace Rpg.Scripting;

public class CallerEntityExpr : Expr<Entity?>
{
    public CallerEntityExpr(){}
    public CallerEntityExpr(Stream stream){}

    [ExprOp(ExprCategory.Entity, "caller", "self")]
    public static Expr<Entity?> CompileOp(JsonElement obj) => new CallerEntityExpr();

    public override Entity? Eval(EvalContext ctx)
    {
        return ctx.Caller;
    }
}
public class TargetEntityExpr : Expr<Entity?>
{
    public TargetEntityExpr(){}
    public TargetEntityExpr(Stream stream){}

    [ExprOp(ExprCategory.Entity, "target")]
    public static Expr<Entity?> CompileOp(JsonElement obj) => new TargetEntityExpr();

    public override Entity? Eval(EvalContext ctx)
    {
        return ctx.Target;
    }
}
public class TargetComponentEntityExpr : Expr<Entity?>
{
    public TargetComponentEntityExpr(){}
    public TargetComponentEntityExpr(Stream stream){}

    [ExprOp(ExprCategory.Entity, "target_component_entity")]
    public static Expr<Entity?> CompileOp(JsonElement obj) => new TargetComponentEntityExpr();

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

    [ExprOp(ExprCategory.Entity, "component_entity", "entity_from_component", "entity_of_component")]
    public static Expr<Entity?> CompileOp(JsonElement obj) => new ComponentEntityExpr(ExpressionCompiler.Compile<Component?>(obj));

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

    [ExprOp(ExprCategory.Entity, "all_entities", IsArray = true)]
    public static ArrayExpr<Entity> CompileOp(JsonElement obj) => new AllEntitiesExpr();

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

    [ExprOp(ExprCategory.Entity, "all_entities_with_component", IsArray = true)]
    [ExprParam("component_type", typeof(uint), Required = true, Description = "The full name of the component type that the entities must have to be included in the result")]
    public static ArrayExpr<Entity> CompileOp(JsonElement obj) =>
        new AllEntitiesWithComponentExpr(ExpressionCompiler.Compile<uint>(obj.GetProperty("component_type")));

    public override IEnumerable<Entity> Eval(EvalContext ctx)
    {
        uint componentType = ComponentType.Eval(ctx);
        return ctx.Board?.GetEntitiesWithComponent(componentType) ?? Array.Empty<Entity>();
    }
}