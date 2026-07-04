using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;

namespace Rpg;

public static class BodyTags
{
    public static readonly Tag<BodyPart> Limb = new("limb"); // Body parts that connect to it's parent with a joint.
    public static readonly Tag<BodyPart> Internal = new("internal"); // Body parts that are inside it's parent.
    public static readonly Tag<BodyPart> Overlaps = new("overlaps"); // When targeting the parent body part, this body part can be targeted accidentally. For example, you may be aiming for the head, but end up hitting the nose instead.
    public static readonly Tag<BodyPart> Head = new("head"); // The head
    public static readonly Tag<BodyPart> Hand = new("hand"); // A hand, or something that functions as a hand, like a claw or tentacle
    public static readonly Tag<BodyPart> Foot = new("foot"); // A foot, or something that functions as a foot, like a hoof or tentacle
    public static readonly Tag<BodyPart> Arm = new("arm"); // An arm, or something that functions as an arm, like a tentacle
    public static readonly Tag<BodyPart> Leg = new("leg"); // A leg, or something that functions as a leg, like a tentacle
    public static readonly Tag<BodyPart> Wing = new("wing");
    public static readonly Tag<BodyPart> Tail = new("tail");
    public static readonly Tag<BodyPart> Horn = new("horn");

    public static readonly Tag<Body> Humanoid = new("humanoid");
    public static readonly Tag<Body> Quadruped = new("quadruped");
    public static readonly Tag<Body> Flying = new("flying");
    public static readonly Tag<Body> Biped = new("biped");
    public static readonly Tag<Body> Female = new("female");
    public static readonly Tag<Body> Male = new("male");
}