using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;

namespace Rpg;

public static class BodyTags
{
    public static readonly Tag<BodyPart> Hard = new("hard");
    public static readonly Tag<BodyPart> Limb = new("limb");
    public static readonly Tag<BodyPart> Internal = new("internal");

    public static readonly Tag<Body> Humanoid = new("humanoid");
    public static readonly Tag<Body> Quadruped = new("quadruped");
    public static readonly Tag<Body> Flying = new("flying");
    public static readonly Tag<Body> Biped = new("biped");
}