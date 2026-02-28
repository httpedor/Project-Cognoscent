using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;

namespace Rpg;

public static class BodyTags
{
    public static readonly Tag<BodyPart> Hard = new("Hard");
    public static readonly Tag<BodyPart> Limb = new("Limb");
    public static readonly Tag<BodyPart> Internal = new("Internal");

    public static readonly Tag<Body> Humanoid = new("Humanoid");
    public static readonly Tag<Body> Quadruped = new("Quadruped");
    public static readonly Tag<Body> Flying = new("Flying");
    public static readonly Tag<Body> Biped = new("Biped");
}