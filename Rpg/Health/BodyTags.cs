using Rpg.Entities.Components.Health;

namespace Rpg;

public static class BodyTags
{
    public static readonly Tag<BodyPart> Hard = new("Hard");
    public static readonly Tag<BodyPart> Limb = new("Limb");
    public static readonly Tag<BodyPart> Internal = new("Internal");
}