using System.Collections;

namespace Rpg.Entities.Components;

public class StatRef : ISerializable
{
    ComponentRef<StatsContainer> ComponentRef;
    public string StatId;
    public StatsContainer? Container => ComponentRef.Component;
    public Stat? Stat => Container?.GetStat(StatId);
    public StatRef(StatsContainer component, string statId)
    {
        ComponentRef = new ComponentRef<StatsContainer>(component);
        StatId = statId;
    }

    public StatRef(Stream stream)
    {
        ComponentRef = new ComponentRef<StatsContainer>(stream);
        StatId = stream.ReadString();
    }

    public void ToBytes(Stream stream)
    {
        ComponentRef.ToBytes(stream);
        stream.WriteString(StatId);
    }
}
public class StatsContainerEvent : ComponentEvent
{
    public StatEvent StatEvent;
    public StatsContainerEvent(StatsContainer container, StatEvent statEvent) : base(container)
    {
        StatEvent = statEvent;
    }
}
public partial class StatsContainer : Component, IEnumerable<Stat>, IStatEventHandler
{
    protected Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
    public IEnumerable<Stat> Stats => stats.Values.Distinct();

    public StatsContainer() : base()
    {

    }
    public StatsContainer(Stream stream) : base(stream)
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
        stat.Container = this;
        return stat;
    }
    public Stat CreateStatIfNotExists(Stat stat)
    {
        string id = stat.Id;
        if (stats.ContainsKey(id))
            return stats[id];
        return CreateStat(stat);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        var distinctStats = Stats.ToArray();
        stream.WriteUInt16((ushort)distinctStats.Length);
        foreach (Stat stat in distinctStats)
        {
            stat.ToBytes(stream);
        }
    }

    public IEnumerator<Stat> GetEnumerator()
    {
        return Stats.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void OnStatChanged(StatEvent statEvent)
    {
        var eventArgs = new StatsContainerEvent(this, statEvent);
        Entity.DispatchEvent(eventArgs);
    }
}

public static class StatIds
{
    public const string Strength = "strength";
    public const string Dexterity = "dexterity";
    public const string Constitution = "constitution";
    public const string Intelligence = "intelligence";
    public const string Wisdom = "wisdom";
    public const string Charisma = "charisma";

    public const string MaxHealth = "max_health";
    public const string MaxMana = "max_mana";
    public const string MaxStamina = "max_stamina";
    public const string Pain = "pain";
}