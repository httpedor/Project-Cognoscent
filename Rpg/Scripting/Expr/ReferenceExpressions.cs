namespace Rpg.Scripting;

public class CompendiumEntryExpr<T> : Expr<T?> where T : class
{
    private T? cachedEntry = null;
    public readonly Expr<string> IdExpr;
    public CompendiumEntryExpr(Expr<string> idExpr)
    {
        IdExpr = idExpr;
        if (idExpr is StringLiteralExpr literal)
        {
            string id = literal.Value;
            cachedEntry = Compendium.GetEntry<T>(id);
        }
    }
    public CompendiumEntryExpr(Stream stream)
    {
        IdExpr = BaseExpr.Deserialize<Expr<string>>(stream);
    }
    public override T? Eval(EvalContext ctx)
    {
        if (cachedEntry != null)
            return cachedEntry;
        string id = IdExpr.Eval(ctx);
        return Compendium.GetEntry<T>(id);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        IdExpr.ToBytes(stream);
    }
}

public class EnumExpr<T> : Expr<T> where T : struct, Enum
{
    private T? cachedValue = null;
    public readonly Expr<string> ValueExpr;
    public EnumExpr(Expr<string> valueExpr)
    {
        ValueExpr = valueExpr;
        if (valueExpr is StringLiteralExpr literal)
        {
            string valueStr = literal.Value;
            if (Enum.TryParse<T>(valueStr, out var result))
            {
                cachedValue = result;
            }
            else
            {
                throw new ArgumentException($"Invalid enum value '{valueStr}' for enum type {typeof(T).Name}.");
            }
        }
    }
    public EnumExpr(Stream stream)
    {
        ValueExpr = BaseExpr.Deserialize<Expr<string>>(stream);
    }
    public override T Eval(EvalContext ctx)
    {
        if (cachedValue.HasValue)
            return cachedValue.Value;
        string valueStr = ValueExpr.Eval(ctx);
        if (Enum.TryParse<T>(valueStr, out var result))
        {
            return result;
        }
        throw new ArgumentException($"Invalid enum value '{valueStr}' for enum type {typeof(T).Name}.");
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        ValueExpr.ToBytes(stream);
    }
}