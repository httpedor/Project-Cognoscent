namespace Rpg.Health;

using Rpg.Entities;
using Rpg.Entities.Components.Health;
using Rpg.Scripting;

public class BodyStat : ISerializable
{
    //TODO: Implement stat thresholds. Planning to use it to create "asfixiation" status when respiratory stat is too low
    public class StatDependency : ISerializable
    {
        public string StatName;
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
    /// <summary>
    /// Dependencies that modify this stat based on other stats.
    /// </summary>
    public StatDependency[]? Dependencies;
    /// <summary>
    /// Either the name of the stat that defines the regeneration rate, or a Expr<float> value.
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
    public Dictionary<string, float> GroupEffectiveness = new();
    /// <summary>
    /// Code to execute when the stat changes. The parameters are the old and new values.
    /// </summary>
    public EffectExpr? OnChange;

    public BodyStat(Stat def)
    {
        Definition = def;
    }

    public void Tick(Body body)
    {
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

        if (Regen != null)
        {
            float regenAmount = Regen.Eval(body.Context);
            stat.BaseValue = Math.Clamp(stat.BaseValue + (regenAmount * (1/50f)), stat.MinValue, stat.MaxValue);
        }

        if (Dependencies != null)
        {
            foreach (var dep in Dependencies)
            {
                var depStat = stats.GetStat(dep.StatName);
                if (depStat != null && dep.ModifierValue != null && dep.ModifierType != null)
                {
                    var ctx = new EvalContext(depStat.FinalValue)
                    {
                        Target = body.Entity,
                        Caller = body.Entity,
                        Board = body.Entity.Board
                    };
                    var modValue = dep.ModifierValue.Eval(ctx);
                    var modType = dep.ModifierType.Eval(ctx);
                    stat.SetModifier(dep.StatName, modValue, modType);
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
        stream.WriteBoolean(!string.IsNullOrEmpty(MaxDependencyName));
        if (!string.IsNullOrEmpty(MaxDependencyName))
        {
            stream.WriteString(MaxDependencyName!);
        }
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
