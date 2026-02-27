using System.Text.Json;
using Rpg.Entities;
using Rpg.Health;

namespace Rpg.Scripting;

public class AddInjuryEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly InjuryModel InjuryModel;
    public AddInjuryEffect(InjuryModel model, Expr<Entity?> target)
    {
        InjuryModel = model;
        Target = target;
    }

    [ExprOp(ExprCategory.Effect, "add_injury", "addinjury", "injury", "hurt")]
    [ExprParam("injury", "string", Required = true, Description = "Injury model definition")]
    [ExprParam("target", "selectorExpr", Required = true)]
    public static EffectExpr CompileOp(JsonElement obj)
        => new AddInjuryEffect(
            new InjuryModel(obj.GetProperty("injury")),
            ExpressionCompiler.CompileSelector(obj.GetProperty("target")));
    public AddInjuryEffect(Stream stream)
    {
        InjuryModel = new InjuryModel(stream);
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
    }
    public override void Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return;
        var part = entity.BodyPart;
        if (part == null)
            return;

        part.AddInjury(InjuryModel.Eval(ctx));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        InjuryModel.ToBytes(stream);
        Target.ToBytes(stream);
    }
}
public class HealInjuryTypeEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<float>? Amount;
    public readonly CompendiumEntryExpr<InjuryType> InjuryType;
    public HealInjuryTypeEffect(CompendiumEntryExpr<InjuryType> injuryType, Expr<float> amount, Expr<Entity?> target)
    {
        InjuryType = injuryType;
        Amount = amount;
        Target = target;
    }
    public HealInjuryTypeEffect(CompendiumEntryExpr<InjuryType> injuryType, Expr<Entity?> target)
    {
        InjuryType = injuryType;
        Amount = null;
        Target = target;
    }

    [ExprOp(ExprCategory.Effect, "heal_injury", "healinjury")]
    [ExprParam("injury", "stringExpr", Required = true, Description = "Injury type compendium ID")]
    [ExprParam("target", "selectorExpr", Required = true)]
    [ExprParam("amount", "numberExpr", Description = "Amount to heal (omit to remove entirely)")]
    public static EffectExpr CompileOp(JsonElement obj)
    {
        var injuryType = ExpressionCompiler.CompileCompendiumEntry<InjuryType>(obj.GetProperty("injury"));
        var target = ExpressionCompiler.CompileSelector(obj.GetProperty("target"));
        if (obj.TryGetProperty("amount", out var amountElement))
        {
            var amount = ExpressionCompiler.CompileNumber(amountElement);
            return new HealInjuryTypeEffect(injuryType, amount, target);
        }
        return new HealInjuryTypeEffect(injuryType, target);
    }
    public HealInjuryTypeEffect(Stream stream)
    {
        InjuryType = (CompendiumEntryExpr<InjuryType>)BaseExpr.Deserialize(stream);
        if (stream.ReadBoolean())
            Amount = (Expr<float>)BaseExpr.Deserialize(stream);
        Target = (Expr<Entity?>)BaseExpr.Deserialize(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        InjuryType.ToBytes(stream);
        stream.WriteBoolean(Amount != null);
        if (Amount != null)
            Amount.ToBytes(stream);
        Target.ToBytes(stream);
    }
    public override void Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return;
        var part = entity.BodyPart;
        if (part == null)
            return;
        var type = InjuryType.Eval(ctx);
        var injury = part.Injuries.FirstOrDefault(inj => inj.Type == type, new Injury(null!, -1));
        if (injury.Severity > 0)
        {
            if (Amount != null)
            {
                var inj = new Injury(injury.Type, injury.Severity - Amount.Eval(ctx));
                part.ChangeInjury(injury, inj);
            }
            else
                part.RemoveInjury(injury);
        }
    }
}