using System.ComponentModel;
using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components.Health;
using Rpg.Health;

namespace Rpg.Scripting;

public class AddInjuryEffect : EffectExpr
{
    public readonly Expr<BodyPart?> Target;
    public readonly Expr<float> Layer;
    public readonly InjuryModel InjuryModel;
    public AddInjuryEffect(InjuryModel model, Expr<BodyPart?> target, Expr<float>? layer = null)
    {
        InjuryModel = model;
        Target = target;
        if (layer == null)
            Layer = new ConstNumberExpr(0);
        else
            Layer = layer;
    }

    // The injury used to be declared as a raw JSON subtree the op parsed itself, which was the last
    // thing in the language that could only be written as an object. An injury model is a type and a
    // severity, so it is now spelled out as two ordinary arguments.
    [ExprOp("add_injury", "injury", "hurt", Description = "Applies an injury to a body part.")]
    public static EffectExpr Op(
        [Doc("Injury type compendium id")] Expr<InjuryType> injury,
        [Doc("How severe the injury is")] Expr<float> severity,
        [Doc("Body part to injure")] Expr<BodyPart?> target,
        [Doc("Layer to injure; defaults to the outermost")] Expr<float>? layer = null)
        => new AddInjuryEffect(new InjuryModel(injury!, severity), target, layer);
    public AddInjuryEffect(Stream stream)
    {
        InjuryModel = new InjuryModel(stream);
        Target = (Expr<BodyPart?>)BaseExpr.Deserialize(stream);
        Layer = BaseExpr.Deserialize<Expr<float>>(stream);
    }
    public override void EvalEffect(EvalContext ctx)
    {
        var part = Target.Eval(ctx);
        if (part == null)
            return;

        part.GetLayer((int)Layer.Eval(ctx)).AddInjury(InjuryModel.Eval(ctx));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        InjuryModel.ToBytes(stream);
        Target.ToBytes(stream);
        Layer.ToBytes(stream);
    }
}
public class HealInjuryTypeEffect : EffectExpr
{
    public readonly Expr<BodyPart?> Target;
    public readonly Expr<float>? Amount;
    public Expr<float>? Layer;
    //TODO: Maybe do a "maxLayer" property that defines how deep it'll search for an injury
    public readonly Expr<InjuryType> InjuryType;
    public HealInjuryTypeEffect(Expr<InjuryType> injuryType, Expr<float> amount, Expr<BodyPart?> target)
    {
        InjuryType = injuryType;
        Amount = amount;
        Target = target;
    }
    public HealInjuryTypeEffect(Expr<InjuryType> injuryType, Expr<BodyPart?> target)
    {
        InjuryType = injuryType;
        Amount = null;
        Target = target;
    }

    [ExprOp("heal_injury", Description = "Heals or removes an injury on a body part.")]
    public static EffectExpr Op(
        [Doc("Injury type compendium id")] Expr<InjuryType> injury,
        [Doc("Body part to heal")] Expr<BodyPart?> target,
        [Doc("Amount to heal; omit to remove the injury entirely")] Expr<float>? amount = null,
        [Doc("Layer to heal")] Expr<float>? layer = null)
        => amount == null
            ? new HealInjuryTypeEffect(injury, target) { Layer = layer }
            : new HealInjuryTypeEffect(injury, amount, target) { Layer = layer };
    public HealInjuryTypeEffect(Stream stream)
    {
        InjuryType = (CompendiumEntryExpr<InjuryType>)BaseExpr.Deserialize(stream);
        if (stream.ReadBoolean())
            Amount = (Expr<float>)BaseExpr.Deserialize(stream);
        Target = (Expr<BodyPart?>)BaseExpr.Deserialize(stream);
        if (stream.ReadBoolean())
            Layer = BaseExpr.Deserialize<Expr<float>>(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        InjuryType.ToBytes(stream);
        stream.WriteBoolean(Amount != null);
        if (Amount != null)
            Amount.ToBytes(stream);
        Target.ToBytes(stream);
        stream.WriteBoolean(Layer != null);
        if (Layer != null)
            Layer.ToBytes(stream);
    }
    public override void EvalEffect(EvalContext ctx)
    {
        var part = Target.Eval(ctx);
        if (part == null)
            return;
        var type = InjuryType.Eval(ctx);
        Injury injury = new Injury(null!, -1);
        BodyLayer layer = null!;
        if (Layer == null)
        {
            foreach (var (inj, lay) in part.InjuriesWithLayers)
            {
                if (inj.Type == type && inj.Severity > 0)
                {
                    injury = inj;
                    layer = lay;
                }
            }
        }
        else
        {
            layer = part.GetLayer((int)Layer.Eval(ctx));
            foreach (var inj in layer.Injuries)
            {
                if (inj.Type == type && inj.Severity > 0)
                {
                    injury = inj;
                }
            }
        }
        if (layer != null && injury.Severity > 0 && injury.Type != null)
        {
            if (Amount != null)
            {
                var inj = new Injury(injury.Type, injury.Severity - Amount.Eval(ctx));
                layer.ChangeInjury(injury, inj);
            }
            else
                layer.RemoveInjury(injury);
        }
    }
}

public class ChangeStabilityEffect : EffectExpr
{
    public readonly Expr<float> Amount;
    public readonly Expr<Body?> Target;
    public ChangeStabilityEffect(Expr<float> amount, Expr<Body?> target)
    {
        Amount = amount;
        Target = target;
    }
    [ExprOp("change_stability", "set_stability", Description = "Sets a body's stability outright.")]
    public static EffectExpr SetStability(
        [Doc("New stability value")] Expr<float> amount,
        [Doc("Body to change")] Expr<Body?> target)
        => new ChangeStabilityEffect(amount, target);

    [ExprOp("add_stability", "addstability", Description = "Increases a body's stability.")]
    public static EffectExpr AddStability(
        [Doc("Amount to add")] Expr<float> amount,
        [Doc("Body to change")] Expr<Body?> target)
        => new ChangeStabilityEffect(new AddExpr(amount, new BodyStabilityExpr(target)), target);

    [ExprOp("remove_stability", "reduce_stability", Description = "Decreases a body's stability.")]
    public static EffectExpr RemoveStability(
        [Doc("Amount to remove")] Expr<float> amount,
        [Doc("Body to change")] Expr<Body?> target)
        => new ChangeStabilityEffect(new SubExpr(new BodyStabilityExpr(target), amount), target);
    public ChangeStabilityEffect(Stream stream)
    {
        Amount = (Expr<float>)BaseExpr.Deserialize(stream);
        Target = (Expr<Body?>)BaseExpr.Deserialize(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Amount.ToBytes(stream);
        Target.ToBytes(stream);
    }
    public override void EvalEffect(EvalContext ctx)
    {
        var body = Target.Eval(ctx);
        if (body != null)
        {
            body.Stability += Amount.Eval(ctx);
        }
    }
}
public class ChangePostureEffect : EffectExpr
{
    public readonly Expr<Body?> Target;
    public readonly Expr<BodyPosture?> Posture;
    public ChangePostureEffect(Expr<BodyPosture?> posture, Expr<Body?> target)
    {
        Posture = posture;
        Target = target;
    }
    public ChangePostureEffect(Stream stream)
    {
        Posture = (Expr<BodyPosture?>)BaseExpr.Deserialize(stream);
        Target = (Expr<Body?>)BaseExpr.Deserialize(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Posture.ToBytes(stream);
        Target.ToBytes(stream);
    }
    public override void EvalEffect(EvalContext ctx)
    {
        var body = Target.Eval(ctx);
        if (body == null)
            return;

        var posture = Posture.Eval(ctx);
        if (posture == null || body.CurrentPosture == posture)
            return;

        body.ChangePosture(posture);
    }

    [ExprOp("change_posture", "set_posture", Description = "Puts a body into a named posture.")]
    public static EffectExpr Op(
        [Doc("Posture compendium id")] Expr<BodyPosture?> posture,
        [Doc("Body to change")] Expr<Body?> target)
        => new ChangePostureEffect(posture, target);

    [ExprOp("change_posture_to_resting", "reset_posture", "go_to_resting_posture",
            Description = "Returns a body to its model's resting posture.")]
    public static EffectExpr ToResting([Doc("Body to change")] Expr<Body?> target)
        => new ChangePostureEffect(
            new CompendiumEntryExpr<BodyPosture>(new BodyRestingPostureNameExpr(target)), target);
}