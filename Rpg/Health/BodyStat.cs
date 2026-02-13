namespace Rpg.Health;

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

public class BodyStat : ISerializable
{
    //TODO: Implement stat thresholds. Planning to use it to create "asfixiation" status when respiratory stat is too low
    public class StatDepCodeGlobals
    {
        public float x;
        public Entity entity;
    }
    public class StatDependency : ISerializable
    {
        public string StatName;
        public string Code;
        public Func<float, Entity, (float, StatModifierType)>? Compiled;

        public StatDependency(string statName, string code)
        {
            StatName = statName;
            Code = code;
            if (SidedLogic.Instance.IsClient())
            {
                Compiled = CompileDep(code);
            }
        }

        private static Func<float, Entity, (float, StatModifierType)> CompileDep(string code)
        {
            var script = CSharpScript.Create<(float, StatModifierType)>(code,
                ScriptOptions.Default.WithReferences(typeof(StatModifierType).Assembly, typeof(Math).Assembly)
                    .WithImports("Rpg", "System.Math"), typeof(StatDepCodeGlobals)).CreateDelegate();
            return (x, y) => script(new StatDepCodeGlobals{x = x, entity=y}).Result;
        }
        public StatDependency(Stream stream)
        {
            StatName = stream.ReadString();
            Code = stream.ReadString();
            if (SidedLogic.Instance.IsClient())
            {
                Compiled = CompileDep(Code);
            }
        }
        public void ToBytes(Stream stream)
        {
            stream.WriteString(StatName);
            stream.WriteString(Code);
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
    /// Either the name of the stat that defines the regeneration rate, or a flat float value.
    /// </summary>
    public Either<string, float>? Regen;
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
    /// Code to execute when the stat changes. It must be an Action<float, float> where the parameters are the old and new values.
    /// </summary>
    public string? OnChangeCode;
    /// <summary>
    /// Compiled code to execute when the stat changes.
    /// </summary>
    public Action<float, Entity>? OnChange;

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
            float regenAmount = Regen.IsRight ? Regen.Right : stats.GetStat(Regen.Left)?.FinalValue ?? 0;
            stat.BaseValue = Math.Clamp(stat.BaseValue + (regenAmount * (1/50f)), stat.MinValue, stat.MaxValue);
        }

        if (Dependencies != null)
        {
            foreach (var dep in Dependencies)
            {
                var depStat = stats.GetStat(dep.StatName);
                if (depStat != null && dep.Compiled != null)
                {
                    var (modValue, modType) = dep.Compiled(depStat.FinalValue, body.Entity);
                    stat.SetModifier(dep.StatName, modValue, modType);
                }
            }
        }
    }

    public static Action<float, Entity> CompileOnChange(string code)
    {
        var script = CSharpScript.Create<Action<float, float>>(code,
            ScriptOptions.Default.WithReferences(typeof(StatModifierType).Assembly, typeof(Math).Assembly)
                .WithImports("Rpg", "System.Math"), typeof(StatDepCodeGlobals)).CreateDelegate();
        return (x, y) => script(new StatDepCodeGlobals{x = x, entity=y});
    }

    public BodyStat(Stream stream)
    {
        Definition = new Stat(stream);

        bool hasRegen = stream.ReadBoolean();
        if (hasRegen)
        {
            bool isLeft = stream.ReadBoolean();
            if (isLeft)
            {
                string statName = stream.ReadString();
                Regen = new Either<string, float>(statName);
            }
            else
            {
                float value = stream.ReadFloat();
                Regen = new Either<string, float>(value);
            }
        }
        bool hasMaxDep = stream.ReadBoolean();
        if (hasMaxDep)
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
            if (Regen.IsLeft)
            {
                stream.WriteBoolean(true);
                stream.WriteString(Regen.Left);
            }
            else
            {
                stream.WriteBoolean(false);
                stream.WriteFloat(Regen.Right);
            }
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
