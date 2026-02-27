using System.Text.Json;
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

    [ExprOp(ExprCategory.Selector, "part_by_tag", "bp_by_tag", "bodypart_by_tag", "body_part_by_tag")]
    [ExprParam("target", "selectorExpr", Required = true)]
    [ExprParam("tag", "stringExpr", Required = true)]
    public static Expr<Entity?> CompileOp(JsonElement obj)
        => new BodyPartSelectorByTagExpr(
            ExpressionCompiler.CompileSelector(obj.GetProperty("target")),
            ExpressionCompiler.CompileString(obj.GetProperty("tag")));
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

    [ExprOp(ExprCategory.Selector, "part_by_name", "bp_by_name", "bodypart_by_name", "body_part_by_name")]
    [ExprParam("target", "selectorExpr", Required = true)]
    [ExprParam("name", "stringExpr", Required = true)]
    public static Expr<Entity?> CompileOp(JsonElement obj)
        => new BodyPartSelectorByNameExpr(
            ExpressionCompiler.CompileSelector(obj.GetProperty("target")),
            ExpressionCompiler.CompileString(obj.GetProperty("name")));
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

    [ExprOp(ExprCategory.Selector, "part_by_path", "bp_by_path", "bodypart_by_path", "body_part_by_path")]
    [ExprParam("target", "selectorExpr", Required = true)]
    [ExprParam("path", "stringExpr", Required = true)]
    public static Expr<Entity?> CompileOp(JsonElement obj)
        => new BodyPartSelectorByPathExpr(
            ExpressionCompiler.CompileSelector(obj.GetProperty("target")),
            ExpressionCompiler.CompileString(obj.GetProperty("path")));
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