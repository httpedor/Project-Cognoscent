using Rpg.Entities;

namespace Rpg.Scripting;
public abstract class BaseExpr : ISerializable
{
    public static BaseExpr Deserialize(Stream stream)
    {
        var typeName = stream.ReadString();
        var type = Type.GetType("Rpg.Scripting." + typeName);
        if (type == null)
            throw new Exception("Failed to get expression type: " + typeName);
        if (!type.IsSubclassOf(typeof(BaseExpr)))
            throw new Exception("Type is not an expression: " + typeName);
        if (type.GetConstructor(new[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get expression constructor: " + typeName);

        return (BaseExpr)Activator.CreateInstance(type, stream)!;
    }
    public static T Deserialize<T>(Stream stream) where T : BaseExpr
    {
        var expr = Deserialize(stream);
        if (expr is T tExpr)
            return tExpr;
        throw new Exception($"Failed to deserialize expression. Expected {typeof(T).Name}, got {expr.GetType().Name}");
    }

    public virtual void ToBytes(Stream stream)
    {
        //No need for full name since all expressions are in the same namespace
        stream.WriteString(GetType().Name);
    }
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