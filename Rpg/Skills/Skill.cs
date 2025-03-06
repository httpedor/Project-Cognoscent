using System.Numerics;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using Rpg.Entities;

namespace Rpg;


public abstract class Skill : ISerializable
{
    public string? CustomName = null;
    public string? CustomIcon = null;
    public static Skill FromBytes(Stream bytes){
        var path = bytes.ReadString();
        Type? type = Type.GetType(path);

        if (type == null)
            throw new Exception("Failed to get action type: " + path);
        if (type.GetConstructor(new Type[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get action constructor: " + path);
        return (Skill)Activator.CreateInstance(type, bytes);
    }

    public Skill()
    {
        
    }

    public Skill(Stream data)
    {
        if (data.ReadByte() != 0)
            CustomName = data.ReadString();
        if (data.ReadByte() != 0)
            CustomIcon = data.ReadString();
    }

    public virtual void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteByte((Byte)(CustomName == null ? 0 : 1));
        if (CustomName != null)
            stream.WriteString(CustomName);
        stream.WriteByte((Byte)(CustomIcon == null ? 0 : 1));
        if (CustomIcon != null)
            stream.WriteString(CustomIcon);
    }

    public virtual string GetName()
    {
        if (CustomName == null)
            return "";
        return CustomName;
    }
    public abstract string GetDescription();

    public virtual string GetIconName()
    {
        if (CustomIcon != null)
            return CustomIcon;
        return GetName().ToLower();
    }

    public virtual void Execute(ISkillSource source, List<SkillArgument> arguments)
    {

    }

    public virtual void Start(ISkillSource source, List<SkillArgument> arguments)
    {

    }

    public virtual void Cancel(ISkillSource source, List<SkillArgument> arguments, bool interrupted = false)
    {

    }

    public virtual bool CanBeUsed(ISkillSource source)
    {
        return true;
    }

    public virtual int GetDelay(ISkillSource source, List<SkillArgument> arguments)
    {
        return 0;
    }
    public virtual int GetCooldown(ISkillSource source, List<SkillArgument> arguments)
    {
        return 0;
    }

    public virtual void CanCancel(ISkillSource source, List<SkillArgument> arguments)
    {

    }

    public bool ValidateArguments(List<SkillArgument> arguments)
    {
        int i = 0;
        if (arguments.Count > GetArguments().Length)
            return false;
        foreach (var arg in arguments)
        {
            bool found = false;

            foreach (var option in GetArguments()[i])
            {
                if (arg.GetType() == option)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
            i++;
        }

        return true;
    }

    public virtual bool CanUseArgument(ISkillSource source, int index, SkillArgument arg)
    {
        return true;
    }

    public virtual Type[][] GetArguments()
    {
        return new Type[0][];
    }

    public Skill WithName(string name)
    {
        CustomName = name;
        return this;
    }
    public Skill WithIcon(string icon)
    {
        CustomIcon = icon;
        return this;
    }
}

public class SkillData : ISerializable
{
    public readonly int Id;
    public Skill Skill;
    public int StartTick;
    public List<SkillArgument> Arguments;
    public SkillData(Skill skill, List<SkillArgument> args, int startTick)
    {
        Id = new Random().Next();
        Skill = skill;
        Arguments = args;
        StartTick = startTick;
    }
    public SkillData(Stream stream)
    {
        Id = stream.ReadInt32();
        Skill = Skill.FromBytes(stream);
        StartTick = stream.ReadInt32();
        int count = stream.ReadByte();
        Arguments = new();
        for (int i = 0; i < count; i++)
        {
            Arguments.Add(SkillArgument.FromBytes(stream));
        }
    }
    public void ToBytes(Stream stream)
    {
        stream.WriteInt32(Id);
        Skill.ToBytes(stream);
        stream.WriteInt32(StartTick);
        stream.WriteByte((Byte)Arguments.Count);
        foreach (var arg in Arguments)
            arg.ToBytes(stream);
    }
}