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

    [ExprOp(ExprCategory.Effect, "add_injury", "injury", "hurt")]
    [ExprParam("injury", typeof(InjuryModel), Required = true, Description = "Injury model definition")]
    [ExprParam("target", typeof(BodyPart), Required = true)]
    public static EffectExpr CompileOp(JsonElement obj)
        => new AddInjuryEffect(
            new InjuryModel(obj.GetProperty("injury")),
            ExpressionCompiler.Compile<BodyPart>(obj.GetProperty("target")));
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

    [ExprOp(ExprCategory.Effect, "heal_injury")]
    [ExprParam("injury", typeof(string), Required = true, Description = "Injury type compendium ID")]
    [ExprParam("target", typeof(BodyPart), Required = true)]
    [ExprParam("amount", typeof(float), Description = "Amount to heal (omit to remove entirely)")]
    [ExprParam("layer", typeof(float), Description = "The layer that will be healed")]
    public static EffectExpr CompileOp(JsonElement obj)
    {
        var injuryType = ExpressionCompiler.Compile<InjuryType>(obj.GetProperty("injury"));
        var target = ExpressionCompiler.Compile<BodyPart>(obj.GetProperty("target"));
        if (obj.TryGetProperty("amount", out var amountElement))
        {
            var amount = ExpressionCompiler.Compile<float>(amountElement);
            return new HealInjuryTypeEffect(injuryType, amount, target)
            {
                Layer = obj.TryGetProperty("layer", out var layerProp) ? ExpressionCompiler.Compile<float>(layerProp) : null
            };
        }
        return new HealInjuryTypeEffect(injuryType, target)
        {
            Layer = obj.TryGetProperty("layer", out var layerObj) ? ExpressionCompiler.Compile<float>(layerObj) : null
        };
    }
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
    [ExprOp(ExprCategory.Effect, "change_stability", "set_stability")]
    [ExprParam("amount", typeof(float), Required = true, Description = "Amount of stability to add (negative to remove)")]
    [ExprParam("target", typeof(Body), Required =true, Description = "The body whose stability will be changed")]
    public static EffectExpr CompileOp(JsonElement obj)
        => new ChangeStabilityEffect(ExpressionCompiler.Compile<float>(obj.GetProperty("amount")), ExpressionCompiler.Compile<Body>(obj.GetProperty("target")));
    
    [ExprOp(ExprCategory.Effect, "add_stability", "addstability")]
    [ExprParam("amount", typeof(float), Required = true, Description = "Amount of stability to add")]
    [ExprParam("target", typeof(Body), Required =true, Description = "The body whose stability will be changed")]
    public static EffectExpr CompileAddOp(JsonElement obj)
    {
        var body = ExpressionCompiler.Compile<Body>(obj.GetProperty("target"));
        var amount = ExpressionCompiler.Compile<float>(obj.GetProperty("amount"));
        return new ChangeStabilityEffect(new AddExpr(amount, new BodyStabilityExpr(body)), body);
    }

    [ExprOp(ExprCategory.Effect, "remove_stability", "reduce_stability")]
    [ExprParam("amount", typeof(float), Required = true, Description = "Amount of stability to remove")]
    [ExprParam("target", typeof(Body), Required =true, Description = "The body whose stability will be changed")]
    public static EffectExpr CompileRemoveOp(JsonElement obj)
    {
        var body = ExpressionCompiler.Compile<Body>(obj.GetProperty("target"));
        var amount = ExpressionCompiler.Compile<float>(obj.GetProperty("amount"));
        return new ChangeStabilityEffect(new SubExpr(new BodyStabilityExpr(body), amount), body);
    }
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

    [ExprOp(ExprCategory.Effect, "change_posture", "set_posture")]
    [ExprParam("posture", typeof(string), Required = true, Description = "The posture to set (compendium ID)")]
    [ExprParam("target", typeof(Body), Required =true, Description = "The body whose posture will be changed")]
    public static EffectExpr CompileOp(JsonElement obj)
        => new ChangePostureEffect(ExpressionCompiler.Compile<BodyPosture?>(obj.GetProperty("posture")), ExpressionCompiler.Compile<Body>(obj.GetProperty("target"))!);
    [ExprOp(ExprCategory.Effect, "change_posture_to_resting", "reset_posture", "go_to_resting_posture")]
    [ExprParam("target", typeof(Body), Required =true, Description = "The body whose posture will be changed")]
    public static EffectExpr CompileResting(JsonElement obj)
    {
        var body = ExpressionCompiler.Compile<Body>(obj.GetProperty("target"));
        return new ChangePostureEffect(new CompendiumEntryExpr<BodyPosture>(new BodyRestingPostureNameExpr(body)), body!);
    }
}