using System.ComponentModel;
using Rpg;
using Rpg.Inventory;

namespace Rpg;

public class SkillSourceRef
{
    public ISkillSource? SkillSource;
    public SkillSourceRef(ISkillSource skillSource)
    {
        SkillSource = skillSource;
    }

    public SkillSourceRef(Stream stream)
    {
        byte type = (byte)stream.ReadByte();
        SkillSource = type switch
        {
            0 => null,
            1 => new ItemRef(stream).Item,
            2 => new BodyPartRef(stream).BodyPart,
            3 => new ItemRef(stream).Item?.GetProperty<EquipmentProperty>(),
            4 => new SkillTreeEntryRef(stream).Entry,
            _ => throw new Exception("Unknown skill source type: " + type)
        };
    }

    public void ToBytes(Stream stream)
    {
        switch (SkillSource)
        {
            case null:
                stream.WriteByte(0);
                break;
            case Item i:
                stream.WriteByte(1);
                new ItemRef(i).ToBytes(stream);
                break;
            case BodyPart bp:
                stream.WriteByte(2);
                new BodyPartRef(bp).ToBytes(stream);
                break;
            case EquipmentProperty ep:
                stream.WriteByte(3);
                new ItemRef(ep.Item).ToBytes(stream);
                break;
            case SkillTreeEntry ste:
                stream.WriteByte(4);
                new SkillTreeEntryRef(ste).ToBytes(stream);
                break;
            default:
                throw new Exception("Unknown skill source type: " + SkillSource.GetType());
        }
    }
}

public interface ISkillSource
{
    public IEnumerable<Skill> Skills {
        get;
    }

    public string Name {
        get;
    }
}