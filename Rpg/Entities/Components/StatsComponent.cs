namespace Rpg.Entities;

public partial class StatsComponent : Component
{
    public event Action<Stat>? OnStatCreated;
    protected Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
    public IEnumerable<Stat> Stats => stats.Values.Distinct();

    public StatsComponent() : base()
    {

    }
    public StatsComponent(Stream stream) : base(stream)
    {
        ushort statCount = stream.ReadUInt16();
        for (int i = 0; i < statCount; i++)
        {
            Stat stat = new Stat(stream);
            CreateStat(stat);
        }
    }

    public Stat? GetStat(string name)
    {
        return stats.GetValueOrDefault(name);
    }

    public float GetStatValue(string name, float defaultValue = 0)
    {
        return GetStat(name)?.FinalValue ?? defaultValue;
    }
    public float this[string id, float defaultValue = 0]
    {
        get => GetStatValue(id, defaultValue);
        set 
        {
            Stat? stat = GetStat(id);
            if (stat != null)
                stat.BaseValue = value;
        }
    }

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

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteUInt16((ushort)stats.Count);
        foreach (Stat stat in Stats)
        {
            stat.ToBytes(stream);
        }
    }
}