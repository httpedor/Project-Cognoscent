using System.Text.Json;
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
        stat.Holder = (IStatHolder)(object)this;
        OnStatCreated?.Invoke(stats[id]);
        return stat;
    }

    protected void StatsToBytes(Stream stream)
    {
        stream.WriteUInt16((ushort)stats.Count);
        foreach (Stat stat in Stats)
        {
            stat.ToBytes(stream);
        }
    }
    protected void StatsFromBytes(Stream stream)
    {
        ushort statCount = stream.ReadUInt16();
        for (int i = 0; i < statCount; i++)
        {
            Stat stat = new Stat(stream);
            CreateStat(stat);
        }
    }
}

public class StatHolderRef : ISerializable
{
    public IStatHolder? Holder;
    public StatHolderRef(IStatHolder holder)
    {
        this.Holder = holder;
    }

    // Expose the referenced holder (Entity, BodyPart or null)

    public void ToBytes(Stream stream)
    {
        switch (Holder)
        {
            case null:
                stream.WriteByte(0);
                break;
            case Entity e:
                stream.WriteByte(1);
                new EntityRef(e).ToBytes(stream);
                break;
            case BodyPart bp:
                stream.WriteByte(2);
                new BodyPartRef(bp).ToBytes(stream);
                break;
            default:
                throw new NotSupportedException("Unsupported IStatHolder type for serialization: " + Holder.GetType().FullName);
        }
    }

    public StatHolderRef(Stream stream)
    {
        byte type = (byte)stream.ReadByte();
        Holder = type switch
        {
            0 => null,
            1 => new EntityRef(stream).Entity,
            2 => new BodyPartRef(stream).BodyPart,
            _ => throw new Exception("Unknown IStatHolder type: " + type)
        };
    }

}