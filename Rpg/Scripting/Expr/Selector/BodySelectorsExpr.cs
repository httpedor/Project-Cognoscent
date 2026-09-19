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
    
    [ExprOp("parts_by_tag", "bps_by_tag", "bodyparts_by_tag", "body_parts_by_tag",
            Description = "Every body part on a body carrying a tag.")]
    public static ArrayExpr<Component> Op(
        [Doc("Body to search")] Expr<Body?> body,
        [Doc("Tag the parts must carry")] Expr<string> tag)
        => new BodyPartSelectorByTagExpr(body, tag);
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

    [ExprOp("part_by_name", "bp_by_name", "bodypart_by_name", "body_part_by_name",
            Description = "The body part with a given name.")]
    public static Expr<Component?> Op(
        [Doc("Body to search")] Expr<Body?> target,
        [Doc("Part name")] Expr<string> name)
        => new BodyPartSelectorByNameExpr(target, name);
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

    [ExprOp("part_by_path", "bp_by_path", "bodypart_by_path", "body_part_by_path",
            Description = "The body part at a slash-separated path.")]
    public static Expr<Component?> Op(
        [Doc("Body to search")] Expr<Body?> target,
        [Doc("Path to the part")] Expr<string> path)
        => new BodyPartSelectorByPathExpr(target, path);
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

    [ExprOp("body_from_part", "body_from_bp", "body_from_bodypart",
            Description = "The body a part belongs to.")]
    public static Expr<Component?> Op([Doc("Body part to read the owner of")] Expr<BodyPart?> target)
        => new BodyFromPartSelectorExpr(target);
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

    [ExprOp("parts_in_group", "bps_in_group", "bodyparts_in_group", "body_parts_in_group",
            Description = "Every body part belonging to a group.")]
    public static ArrayExpr<Component> Op(
        [Doc("Body to search")] Expr<Body?> body,
        [Doc("Id of the group")] Expr<string> group_id)
        => new BodyPartsInGroupExpr(body, group_id);

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