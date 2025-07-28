using Rpg;

namespace Rpg;

public class DamageSource(DamageType type) : ISerializable
{
    public DamageType Type = type;
    /// <summary>
    /// The entity that initiated the attack.
    /// </summary>
    public Entity? Attacker;
    /// <summary>
    /// The entity that has made contact with the target. This is not always the attacker, because arrows and magic.
    /// </summary>
    public Entity? ContactEntity;
    /// <summary>
    /// Which skill was used to cause damage. Most likely extends <see cref="AttackSkill"/>
    /// </summary>
    public Skill? SkillUsed;
    /// <summary>
    /// Which arguments were passed to <see cref="SkillUsed"/>, always not-null if <see cref="SkillUsed"/> is not null.
    /// </summary>
    public List<SkillArgument>? Arguments;

    public DamageSource(DamageType type, Creature attacker, Skill skillUsed, params SkillArgument[] args) : this(type)
    {
        Attacker = attacker;
        ContactEntity = attacker;
        SkillUsed = skillUsed;
        Arguments = new List<SkillArgument>(args);
    }

    public DamageSource(DamageType type, Entity attacker, Entity? directAttacker = null) : this(type)
    {
        directAttacker ??= attacker;
        
        Attacker = attacker;
        ContactEntity = directAttacker;
    }

    public DamageSource(DamageType type, Creature attacker, Skill skillUsed, List<SkillArgument> args,
        Entity indirectAttacker) : this(type)
    {
        Attacker = attacker;
        ContactEntity = indirectAttacker;
        SkillUsed = skillUsed;
        Arguments = args;
    }

    public DamageSource(Stream stream) : this(DamageType.FromBytes(stream))
    {
        if (stream.ReadByte() != 0)
            Attacker = new EntityRef(stream).Entity;
        if (stream.ReadByte() != 0)
            ContactEntity = new EntityRef(stream).Entity;
        if (stream.ReadByte() == 0) return;
        
        SkillUsed = Skill.FromBytes(stream);
        int count = stream.ReadByte();
        Arguments = new List<SkillArgument>(count);
        for (int i = 0; i < count; i++)
        {
            Arguments.Add(SkillArgument.FromBytes(stream));
        }
    }
    
    public void ToBytes(Stream stream)
    {
        Type.ToBytes(stream);
        if (Attacker != null)
        {
            stream.WriteByte(1);
            new EntityRef(Attacker).ToBytes(stream);
        }
        else
            stream.WriteByte(0);

        if (ContactEntity != null)
        {
            stream.WriteByte(1);
            new EntityRef(ContactEntity).ToBytes(stream);
        }
        else
            stream.WriteByte(0);

        if (SkillUsed != null)
        {
            stream.WriteByte(1);
            SkillUsed.ToBytes(stream);
            
            stream.WriteByte((byte)Arguments!.Count);
            foreach (SkillArgument arg in Arguments)
            {
                arg.ToBytes(stream);
            }
        }
        else
            stream.WriteByte(0);
        
    }
}