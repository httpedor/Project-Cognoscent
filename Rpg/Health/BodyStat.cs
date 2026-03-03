namespace Rpg.Health;

using Rpg.Entities;
using Rpg.Entities.Components.Health;
using Rpg.Scripting;

public class BodyStat : ISerializable
{
    //TODO: Implement stat thresholds. Planning to use it to create "asfixiation" status when respiratory stat is too low
    public class StatThreshold : ISerializable
    {
        public Expr<bool> Condition;
        public EffectExpr Effect;
        public EffectExpr? UnapplyEffect;

        public StatThreshold(Expr<bool> condition, EffectExpr effect, EffectExpr? unapplyEffect = null)
        {
            Condition = condition;
            Effect = effect;
            UnapplyEffect = unapplyEffect;
        }
        public StatThreshold(Stream stream)
        {
            Condition = BaseExpr.Deserialize<Expr<bool>>(stream);
            Effect = BaseExpr.Deserialize<EffectExpr>(stream);
            if (stream.ReadBoolean())
                UnapplyEffect = BaseExpr.Deserialize<EffectExpr>(stream);
        }

        public void ToBytes(Stream stream)
        {
            Condition.ToBytes(stream);
            Effect.ToBytes(stream);
            stream.WriteBoolean(UnapplyEffect != null);
            if (UnapplyEffect != null)
                UnapplyEffect.ToBytes(stream);
        }
    }
    public class StatDependency : ISerializable
    {
        public string StatName;
        public string ModifierId => "dep-" + StatName;
        public Expr<float> ModifierValue;
        public EnumExpr<StatModifierType> ModifierType;

        public StatDependency(string statName, Expr<float> value, EnumExpr<StatModifierType> type)
        {
            StatName = statName;
            ModifierValue = value;
            ModifierType = type;
        }

        public StatDependency(Stream stream)
        {
            StatName = stream.ReadString();
            ModifierValue = BaseExpr.Deserialize<Expr<float>>(stream);
            ModifierType = BaseExpr.Deserialize<EnumExpr<StatModifierType>>(stream);
        }
        public void ToBytes(Stream stream)
        {
            stream.WriteString(StatName);
            ModifierValue.ToBytes(stream);
            ModifierType.ToBytes(stream);
        }
    }

    /// <summary>
    /// The base definition of the stat.
    /// </summary>
    public Stat Definition;
    public string Id => Definition.Id;
    /// <summary>
    /// Dependencies that modify this stat based on other stats.
    /// </summary>
    public StatDependency[]? Dependencies;
    /// <summary>
    /// Thresholds that trigger effects when certain conditions are met. For example, if the stat drops below a certain value, it could apply a debuff to the entity.
    /// Args passed to expressions:
    /// $0 - Current stat value
    /// </summary>
    public StatThreshold[]? Thresholds;
    /// <summary>
    /// A Expr<float> value which defines the regeneration rate of this stat.
    /// </summary>
    public Expr<float>? Regen;
    /// <summary>
    /// Name of the stat that defines the maximum value of this stat.
    /// </summary>
    public string? MaxDependencyName;
    /// <summary>
    /// Name of the stat that defines the minimum value of this stat.
    /// </summary>
    public string? MinDependencyName;
    /// <summary>
    /// If true, the stat is considered vital. If a vital stat reaches 0, the entity may die.
    /// </summary>
    public bool Vital = false;
    /// <summary>
    /// If this stat is calculated only on each individual group, and does not represent something that runs through the whole body
    /// </summary>
    public bool IsLocal = false;
    public Dictionary<string, float> GroupEffectiveness = new();
    /// <summary>
    /// Code to execute when the stat changes. The parameters are the old and new values.
    /// </summary>
    public EffectExpr? OnChange;
    // Caching this to avoid allocating a new EvalContext every tick. The context's Variables[0] will be set to the current value of the dependent stat when evaluating dependencies.
    private EvalContext context = null!;

    public BodyStat(Stat def)
    {
        Definition = def;
    }

    public void Tick(Body body)
    {
        if (context == null)
        {
            context = new EvalContext(0, 0)
            {
                Target = body.Entity,
                Caller = body.Entity,
                Board = body.Entity.Board
            };
        }
        var statName = Definition.Name;
        var stats = body.Entity.Stats;
        if (stats == null) return;

        var stat = stats.GetStat(statName);
        if (stat == null) return;

        if (!string.IsNullOrEmpty(MaxDependencyName))
        {
            var depStat = stats.GetStat(MaxDependencyName);
            if (depStat != null)
                stat.MaxValue = depStat.FinalValue;
        }
        if (!string.IsNullOrEmpty(MinDependencyName))
        {
            var depStat = stats.GetStat(MinDependencyName);
            if (depStat != null)
                stat.MinValue = depStat.FinalValue;
        }

        if (Regen != null)
        {
            float regenAmount = Regen.Eval(body.Context);
            stat.BaseValue = Math.Clamp(stat.BaseValue + (regenAmount * (1/50f)), stat.MinValue, stat.MaxValue);
        }

        // Don't apply dependencies for local stats, since local stats on the body act as "globals" for the body parts
        //  and dependencies should be applied on the body parts themselves when needed.
        if (Dependencies != null && !IsLocal)
        {
            foreach (var dep in Dependencies)
            {
                var depStat = stats.GetStat(dep.StatName);
                if (depStat != null && dep.ModifierValue != null && dep.ModifierType != null)
                {
                    context.Variables[0] = depStat.FinalValue;
                    var modValue = dep.ModifierValue.Eval(context);
                    var modType = dep.ModifierType.Eval(context);
                    stat.SetModifier(dep.ModifierId, modValue, modType, "Dependência de " + depStat);
                }
            }
        }
    }

    public BodyStat(Stream stream)
    {
        Definition = new Stat(stream);

        if (stream.ReadBoolean())
            Regen = BaseExpr.Deserialize<Expr<float>>(stream);
        if (stream.ReadBoolean())
            MaxDependencyName = stream.ReadString();
        if (stream.ReadBoolean())
            MinDependencyName = stream.ReadString();
        IsLocal = stream.ReadBoolean();
        Vital = stream.ReadBoolean();
        int depCount = stream.ReadByte();
        if (depCount > 0)
        {
            Dependencies = new StatDependency[depCount];
            for (int i = 0; i < depCount; i++)
            {
                Dependencies[i] = new StatDependency(stream);
            }
        }
    }
    public void ToBytes(Stream stream)
    {
        Definition.ToBytes(stream);
        stream.WriteBoolean(Regen != null);
        if (Regen != null)
        {
            Regen.ToBytes(stream);
        }
        var hasMaxDep = !string.IsNullOrEmpty(MaxDependencyName);
        stream.WriteBoolean(hasMaxDep);
        if (hasMaxDep)
            stream.WriteString(MaxDependencyName!);
        var hasMinDep = !string.IsNullOrEmpty(MinDependencyName);
        stream.WriteBoolean(hasMinDep);
        if (hasMinDep)
            stream.WriteString(MinDependencyName!);
        stream.WriteBoolean(IsLocal);
        stream.WriteBoolean(Vital);
        stream.WriteByte((byte)(Dependencies?.Length ?? 0));
        if (Dependencies != null)
        {
            foreach (var dep in Dependencies)
            {
                dep.ToBytes(stream);
            }
        }
    }
}
