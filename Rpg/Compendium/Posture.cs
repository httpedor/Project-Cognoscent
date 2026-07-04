using System.Text.Json;
using Rpg.Entities.Components.Health;
using Rpg.Scripting;

namespace Rpg;

/*
TODO: Implement Posture system
Posture would be a system to determine how the body is positioned, e.g lying down, crouching, combat postures, etc.
This would be determined by the position of the body parts, and would provide bonuses or penalties to certain stats or skills depending on the posture.
Maybe also add a "stability" stat that would determine how well the body can maintain current posture, and would be affected by injuries, weight carried, etc.
Certain postures may require certain ConditionExpr to be met to be maintained, for example, standing up may require at least one leg to be healthy.
Some examples:
 - Running is low stability, but provides bonuses to speed and evasion. Requires both legs to be healthy on humanoids.
 - Different combat postures have bonuses to different combat skills and target different body part groups.
 - Changing posture should be a skill probably.
*/
public class BodyPosture : ISerializable, ITaggable
{
    /// <summary>
    /// Id of the posture in the compendium.
    /// </summary>
    public string Id;
    /// <summary>
    /// The name of the posture, for example "standing", "crouching", "prone", "flying", etc.
    /// </summary>
    public string Name;
    /// <summary>
    /// How stable the body is while in this posture.
    /// Target = Body
    /// $0 = Movement Intensity
    /// </summary>
    public Expr<float> MaxStability;
    /// <summary>
    /// How much stability the body recovers per second while in this posture.
    /// Target = Body
    /// $0 = Movement Intensity
    /// </summary>
    public Expr<float>? StabilityRecovery;
    /// <summary>
    /// The ideal height something has to be relative to the body's lowest point to be interacted with successfully with this posture.
    /// [0, 1] relative to body height.
    /// Target = Body
    /// $0 = Height of the body.
    /// </summary>
    public Expr<float> IdealHeight;
    /// <summary>
    /// How much deviation from the ideal height is tolerated.
    /// In meters.
    /// Target = Body
    /// $0 = Height of the body.
    /// </summary>
    public Expr<float> HeightTolerance;
    /// <summary>
    /// How exposed each part of the body is while in this posture.
    /// TODO: Figure out how this affects combat and interactions. Maybe this will be used with the "Block" or "Parry" skills
    /// The key is the tag of the body part.
    /// </summary>
    public Dictionary<string, float> Defensiveness;
    /// <summary>
    /// How many meters per second the body can move while in this posture.
    /// Target = Body
    /// $1 = Movement Intensity
    /// </summary>
    public Expr<float> Movement;
    /// <summary>
    /// Ran every tick while the body is in this posture.
    /// Target = Body
    /// </summary>
    public EffectExpr? OnTick;
    /// <summary>
    /// Condition that determines whether the body can stay in this posture or not. Ran every 10 ticks.
    /// If it returns false, the body will be forced into the resting posture.
    /// Target = Body
    /// </summary>
    public Expr<bool>? CanStayInPosture;
    /// <summary>
    /// Condition that determines whether the body can enter this posture or not. Ran when trying to change into this posture.
    /// Target = Body
    /// </summary>
    public Expr<bool>? CanEnterPosture;
    public HashSet<string> Tags = new();
    HashSet<string> ITaggable.Tags {get => Tags; set => Tags = value;}

    public BodyPosture(string id, JsonElement element)
    {
        Id = id;
        Name = element.GetProperty("name").GetString() ?? throw new Exception("Posture must have a name");
        MaxStability = ExpressionCompiler.Compile<float>(element.GetProperty("maxStability"));
        IdealHeight = ExpressionCompiler.Compile<float>(element.GetProperty("idealHeight"));
        HeightTolerance = ExpressionCompiler.Compile<float>(element.GetProperty("heightTolerance"));
        if (element.TryGetProperty("defensiveness", out var defensivenessProp))
            Defensiveness = defensivenessProp.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetSingle());
        else
            Defensiveness = new();
        Movement = ExpressionCompiler.Compile<float>(element.GetProperty("movement"));
        if (element.TryGetProperty("stabilityRecovery", out var stabilityRecoveryProp))
            StabilityRecovery = ExpressionCompiler.Compile<float>(stabilityRecoveryProp);
        if (element.TryGetProperty("onTick", out var onTickProp))
            OnTick = ExpressionCompiler.CompileEffect(onTickProp);
        if (element.TryGetProperty("canStayInPosture", out var canStayInPostureProp))
            CanStayInPosture = ExpressionCompiler.Compile<bool>(canStayInPostureProp);
        if (element.TryGetProperty("canEnterPosture", out var canEnterPostureProp))
            CanEnterPosture = ExpressionCompiler.Compile<bool>(canEnterPostureProp);
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Id);
    }

    public static BodyPosture FromBytes(Stream stream)
    {
        var id = stream.ReadString();
        var ret = Compendium.GetEntry<BodyPosture>(id);
        if (ret == null)
            throw new InvalidOperationException("Failed to deserialize BodyPosture with id " + id);
        return ret;
    }
}