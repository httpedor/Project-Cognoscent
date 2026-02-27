using Rpg.Entities;

namespace Rpg.Scripting;

public class BodyPartSelectorByTagExpr : Expr<Entity?>
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<string> Tag;
    public BodyPartSelectorByTagExpr(Expr<Entity?> target, Expr<string> tag)
    {
        Target = target;
        Tag = tag;
    }
    public BodyPartSelectorByTagExpr(Stream stream)
    {
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
        Tag = (Expr<string>)BaseExpr.Deserialize(stream);
    }
    public override Entity? Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return null;
        var body = entity.Body;
        if (body == null)
            return null;
        var tagValue = Tag.Eval(ctx);
        if (tagValue == null)
            return null;
        var parts = body.GetPartsWithTag(tagValue);
        if (parts.Count() == 0)
            return null;
        return parts.First().Entity;
    }
}

public class BodyPartSelectorByNameExpr : Expr<Entity?>
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<string> Name;
    public BodyPartSelectorByNameExpr(Expr<Entity?> target, Expr<string> name)
    {
        Target = target;
        Name = name;
    }
    public BodyPartSelectorByNameExpr(Stream stream)
    {
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
        Name = (Expr<string>)BaseExpr.Deserialize(stream);
    }
    public override Entity? Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return null;
        var body = entity.Body;
        if (body == null)
            return null;
        var nameValue = Name.Eval(ctx);
        if (nameValue == null)
            return null;
        var parts = body.GetPartsWithName(nameValue);
        if (parts.Count() == 0)
            return null;
        return parts.First().Entity;
    }
}

public class BodyPartSelectorByPathExpr : Expr<Entity?>
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<string> Path;
    public BodyPartSelectorByPathExpr(Expr<Entity?> target, Expr<string> path)
    {
        Target = target;
        Path = path;
    }
    public BodyPartSelectorByPathExpr(Stream stream)
    {
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
        Path = (Expr<string>)BaseExpr.Deserialize(stream);
    }
    public override Entity? Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return null;
        var body = entity.Body;
        if (body == null)
            return null;
        var pathValue = Path.Eval(ctx);
        if (pathValue == null)
            return null;
        var part = body.GetPartByPath(pathValue);
        return part?.Entity;
    }
}