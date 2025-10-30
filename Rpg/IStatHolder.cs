using TraitGenerator;

namespace Rpg;

public interface IStatHolder
{
    public event Action<Stat>? OnStatCreated;
    IEnumerable<Stat> Stats { get; }
    Stat? GetStat(string name);
    float GetStatValue(string name, float defaultValue = 0);
    Stat CreateStat(Stat stat);
}

public static class StatHolderExtensions
{
    extension(IStatHolder holder)
    {
        public bool HasStat(string id)
        {
            return holder.GetStat(id) != null;
        }
        public bool TryAddModifier(string statId, StatModifier modifier)
        {
            Stat? stat = holder.GetStat(statId);
            if (stat == null)
                return false;
            stat.AddModifier(modifier);
            return true;
        }
    }
}

[Mixin(typeof(IStatHolder))]
abstract class StatHolderMixin : IStatHolder
{
    public event Action<Stat>? OnStatCreated;
    protected Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
    public IEnumerable<Stat> Stats => stats.Values.Distinct();

    public Stat? GetStat(string name)
    {
        return stats.GetValueOrDefault(name);
    }

    public float GetStatValue(string name, float defaultValue = 0)
    {
        return GetStat(name)?.FinalValue ?? defaultValue;
    }
    public float this[string id, float defaultValue = 0] => GetStatValue(id, defaultValue);

    public Stat CreateStat(Stat stat)
    {
        string id = stat.Id;
        stats[id] = stat;
        foreach (string alias in stat.Aliases)
        {
            stats[alias] = stat;
        }
        OnStatCreated?.Invoke(stats[id]);
        return stat;
    }
}

