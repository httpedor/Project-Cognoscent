using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

namespace Rpg.Scripting;


public class BodyPartSelectorByTagExpr : ArrayExpr<Component>
{
    public readonly Expr<Body?> Target;
    public readonly Expr<string> Tag;
    public BodyPartSelectorByTagExpr(Expr<Body?> target, Expr<string> tag)
    {
        Target = target;
        Tag = tag;
    }

    public BodyPartSelectorByTagExpr(Stream stream)
    {
        Target = (Expr<Body?>)BaseExpr.Deserialize(stream);
        Tag = (Expr<string>)BaseExpr.Deserialize(stream);
    }
    public override IEnumerable<BodyPart> Eval(EvalContext ctx)
    {
        var body = Target.Eval(ctx);
        if (body == null)
            return [];
        var tagValue = Tag.Eval(ctx);
        if (tagValue == null)
            return [];
        return body.GetPartsWithTag(tagValue);
    }
    
    [ExprOp(ExprCategory.Component, "part_by_tag", "bp_by_tag", "bodypart_by_tag", "body_part_by_tag", GenericType = "BodyPart", IsArray = true)]
    [ExprParam("body", typeof(Body), Required = true)]
    [ExprParam("tag", typeof(Tag<BodyPart>), Required = true)]
    public static ArrayExpr<Component> CompileOp(JsonElement obj)
        => new BodyPartSelectorByTagExpr(
            ExpressionCompiler.Compile<Body>(obj.GetProperty("body")),
            ExpressionCompiler.Compile<string>(obj.GetProperty("tag")));
}

public class BodyPartSelectorByNameExpr : ComponentExpr<BodyPart>
{
    public readonly Expr<Body?> Target;
    public readonly Expr<string> Name;
    public BodyPartSelectorByNameExpr(Expr<Body?> target, Expr<string> name)
    {
        Target = target;
        Name = name;
    }

    [ExprOp(ExprCategory.Component, "part_by_name", "bp_by_name", "bodypart_by_name", "body_part_by_name", GenericType = "BodyPart")]
    [ExprParam("target", typeof(Body), Required = true)]
    [ExprParam("name", typeof(string), Required = true)]
    public static Expr<Component?> CompileOp(JsonElement obj)
        => new BodyPartSelectorByNameExpr(
            ExpressionCompiler.Compile<Body>(obj.GetProperty("target")),
            ExpressionCompiler.Compile<string>(obj.GetProperty("name")));
    public BodyPartSelectorByNameExpr(Stream stream)
    {
        Target = (Expr<Body?>)BaseExpr.Deserialize(stream);
        Name = (Expr<string>)BaseExpr.Deserialize(stream);
    }
    public override BodyPart? EvalComponent(EvalContext ctx)
    {
        var body = Target.Eval(ctx);
        if (body == null)
            return null;
        var nameValue = Name.Eval(ctx);
        if (nameValue == null)
            return null;
        var parts = body.GetPartsWithName(nameValue);
        if (parts.Count() == 0)
            return null;
        return parts.First();
    }
}

public class BodyPartSelectorByPathExpr : ComponentExpr<BodyPart>
{
    public readonly Expr<Body?> Target;
    public readonly Expr<string> Path;
    public BodyPartSelectorByPathExpr(Expr<Body?> target, Expr<string> path)
    {
        Target = target;
        Path = path;
    }

    [ExprOp(ExprCategory.Component, "part_by_path", "bp_by_path", "bodypart_by_path", "body_part_by_path", GenericType = "BodyPart")]
    [ExprParam("target", typeof(Body), Required = true)]
    [ExprParam("path", typeof(string), Required = true)]
    public static Expr<Component?> CompileOp(JsonElement obj)
        => new BodyPartSelectorByPathExpr(
            ExpressionCompiler.Compile<Body>(obj.GetProperty("target")),
            ExpressionCompiler.Compile<string>(obj.GetProperty("path")));
    public BodyPartSelectorByPathExpr(Stream stream)
    {
        Target = (Expr<Body?>)BaseExpr.Deserialize(stream);
        Path = (Expr<string>)BaseExpr.Deserialize(stream);
    }
    public override BodyPart? EvalComponent(EvalContext ctx)
    {
        var body = Target.Eval(ctx);
        if (body == null)
            return null;
        var pathValue = Path.Eval(ctx);
        if (pathValue == null)
            return null;
        return body.GetPartByPath(pathValue);
    }
}
public class BodyFromPartSelectorExpr : ComponentExpr<Body>
{
    public readonly Expr<BodyPart?> Target;
    public BodyFromPartSelectorExpr(Expr<BodyPart?> target)
    {
        Target = target;
    }

    [ExprOp(ExprCategory.Component, "body_from_part", "body_from_bp", "body_from_bodypart", GenericType = "Body")]
    [ExprParam("target", typeof(BodyPart), Required = true, Description = "The body part to get the body from")]
    public static Expr<Component?> CompileOp(JsonElement obj)
        => new BodyFromPartSelectorExpr(
            ExpressionCompiler.Compile<BodyPart>(obj.GetProperty("target")));
    public BodyFromPartSelectorExpr(Stream stream)
    {
        Target = (Expr<BodyPart?>)BaseExpr.Deserialize(stream);
    }
    public override Body? EvalComponent(EvalContext ctx)
    {
        var part = Target.Eval(ctx);
        return part?.Body;
    }
}
public sealed class BodyPartsInGroupExpr : ArrayExpr<Component>
{
    public readonly Expr<Body?> Body;
    public readonly Expr<string> GroupId;

    public BodyPartsInGroupExpr(Expr<Body?> body, Expr<string> groupId)
    {
        Body = body;
        GroupId = groupId;
    }

    [ExprOp(ExprCategory.Component, "parts_in_group", "bps_in_group", "bodyparts_in_group", "body_parts_in_group", GenericType = "BodyPart", IsArray = true)]
    [ExprParam("body", typeof(Body), Required = true, Description = "The body to get the parts from")]
    [ExprParam("group_id", typeof(string), Required = true, Description = "The ID of the group to get the parts from")]
    public static ArrayExpr<Component> Compile(JsonElement json)
    {
        var body = ExpressionCompiler.Compile<Body>(json.GetProperty("body"));
        var groupId = ExpressionCompiler.Compile<string>(json.GetProperty("group_id"));
        return new BodyPartsInGroupExpr(body, groupId);
    }

    public BodyPartsInGroupExpr(Stream stream)
    {
        Body = BaseExpr.Deserialize<Expr<Body?>>(stream);
        GroupId = BaseExpr.Deserialize<Expr<string>>(stream);
    }

    public override IEnumerable<BodyPart> Eval(EvalContext ctx)
    {
        var bodyValue = Body.Eval(ctx);
        if (bodyValue == null)
            return [];
        var groupIdValue = GroupId.Eval(ctx);
        if (groupIdValue == null)
            return [];
        return bodyValue.GetPartsOnGroup(groupIdValue);
    }
}