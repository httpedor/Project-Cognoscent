using Rpg;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Skills;

namespace Rpg.Health;

public class DamageSource(DamageType type) : ISerializable
{
    public DamageType Type = type;
    /// <summary>
    /// The entity that initiated the attack.
    /// </summary>
    public SkillExecutor? Attacker;
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

    public DamageSource(DamageType type, SkillExecutor attacker, Skill skillUsed, params SkillArgument[] args) : this(type)
    {
        Attacker = attacker;
        ContactEntity = attacker.Entity;
        SkillUsed = skillUsed;
        Arguments = [.. args];
    }

    public DamageSource(DamageType type, SkillExecutor attacker, Entity? directAttacker = null) : this(type)
    {
        Attacker = attacker;
        ContactEntity = directAttacker;
        if (directAttacker == null)
            ContactEntity = attacker.Entity;
    }

    public DamageSource(DamageType type, SkillExecutor attacker, Skill skillUsed, List<SkillArgument> args,
        Entity indirectAttacker) : this(type)
    {
        Attacker = attacker;
        ContactEntity = indirectAttacker;
        SkillUsed = skillUsed;
        Arguments = [.. args];
    }

    public DamageSource(Stream stream) : this(DamageType.FromBytes(stream))
    {
        if (stream.ReadByte() != 0)
            Attacker = new ComponentRef<SkillExecutor>(stream).Component;
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
            new ComponentRef<SkillExecutor>(Attacker).ToBytes(stream);
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
public class DamageInstance : ISerializable
{
    public DamageSource Source;
    public float Amount;

    public DamageInstance(DamageSource source, float amount)
    {
        Source = source;
        Amount = amount;
    }
    public DamageInstance(Stream stream)
    {
        Source = new DamageSource(stream);
        Amount = stream.ReadFloat();
    }
    public void ToBytes(Stream stream)
    {
        Source.ToBytes(stream);
        stream.WriteFloat(Amount);
    }
}