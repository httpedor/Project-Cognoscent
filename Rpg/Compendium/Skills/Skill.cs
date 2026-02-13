using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Scripting;

namespace Rpg.Skills;

public abstract partial class Skill : ISerializable, ITaggable
{
    public static readonly CompileContext DefaultCompilerContext = new CompileContext();
    public string? CustomName = null;
    public string? CustomIcon = null;
    public string? CustomLayer = null;

    public string BBHint => $"[hint={GetTooltip()}]{GetName()}[/hint]";

    HashSet<string> Tags { get; set; } = new();
    HashSet<string> ITaggable.Tags
    {
        get => Tags;
        set => Tags = value;
    }

    public static Skill FromBytes(Stream bytes){
        string path = bytes.ReadString();
        Type? type = Type.GetType(path);
        if (type != null && (type.IsAssignableTo(typeof(ArbitrarySkill)) ||
                             type.IsAssignableTo(typeof(ArbitraryAttackSkill))))
        {
            return Compendium.GetEntry<Skill>(bytes.ReadString())!;
        }

        if (type == null)
            throw new Exception("Failed to get skill type: " + path);
        if (type.GetConstructor([typeof(Stream)]) == null)
            throw new Exception("Failed to get skill constructor: " + path);
        return (Skill)Activator.CreateInstance(type, bytes)!;
    }
    
    public static Skill? FromJson(string id, JsonElement obj)
    {
        try
        {
            string type = obj.GetPropertyOrNull("type")?.GetString() ?? "arbitrary";
            Skill skill;
            switch (type)
            {
                case "arbitrary":
                    skill = new ArbitrarySkill(
                        id,
                        new SkillExpressions(obj),
                        obj.GetPropertyOrNull("description")?.GetString() ?? "",
                        obj.GetPropertyOrNull("name")?.GetString() ?? "",
                        obj.GetPropertyOrNull("icon")?.GetString() ?? ""
                    );

                    break;
                case "attack":
                    skill = new ArbitraryAttackSkill(
                        id,
                        new SkillExpressions(obj),
                        obj.GetPropertyOrNull("description")?.GetString() ?? "",
                        obj.GetPropertyOrNull("name")?.GetString() ?? "",
                        obj.GetPropertyOrNull("icon")?.GetString() ?? ""
                    );
                    break;
                default:
                    throw new Exception("Unknown skill type: " + type);
            }

            return skill;
        }
        catch (Exception e)
        {
            Console.WriteLine("Couldn't load skill from JSON");
            Console.WriteLine(e);
        }

        return null;
    }

    public Skill()
    {
        
    }

    protected Skill(Stream data)
    {
        if (data.ReadByte() != 0)
            CustomName = data.ReadString();
        if (data.ReadByte() != 0)
            CustomIcon = data.ReadString();
        if (data.ReadByte() != 0)
            CustomLayer = data.ReadString();
        this.LoadTags(data);
    }

    public virtual void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteByte((byte)(CustomName == null ? 0 : 1));
        if (CustomName != null)
            stream.WriteString(CustomName);
        stream.WriteByte((byte)(CustomIcon == null ? 0 : 1));
        if (CustomIcon != null)
            stream.WriteString(CustomIcon);
        stream.WriteByte((byte)(CustomLayer == null ? 0 : 1));
        if (CustomLayer != null)
            stream.WriteString(CustomLayer);
        this.SaveTags(stream);
    }

    public virtual string GetName()
    {
        if (CustomName == null)
            return "";
        return CustomName;
    }
    public abstract string GetDescription();

    public virtual string GetTooltip()
    {
        return GetName() + "\n" + GetDescription();
    }

    public virtual string GetIconName()
    {
        if (CustomIcon != null)
            return CustomIcon;
        return GetName().ToLower();
    }

    public virtual string[] GetLayers(SkillExecutor executor)
    {
        return ["default"];
    }

    public virtual bool IsCombatSkill(SkillExecutor executor, List<SkillArgument> arguments)
    {
        return false;
    }

    public virtual void Execute(SkillExecutor executor, List<SkillArgument> arguments, uint tick)
    {
        var cFeatures = executor.Entity.Features;
        if (cFeatures == null)
            return;
        foreach (var feature in cFeatures.EnabledFeatures)
        {
            feature.OnExecuteSkill(executor, this, arguments, tick);
        }
    }

    public virtual void Start(SkillExecutor executor, List<SkillArgument> arguments)
    {

    }

    public virtual void Cancel(SkillExecutor executor, List<SkillArgument> arguments, bool interrupted = false)
    {

    }

    public virtual bool CanBeUsed(SkillExecutor executor)
    {
        return true;
    }

    public virtual uint GetDelay(SkillExecutor executor, List<SkillArgument> arguments)
    {
        return 0;
    }
    public virtual uint GetCooldown(SkillExecutor executor, List<SkillArgument> arguments)
    {
        return 0;
    }
    public virtual uint GetDuration(SkillExecutor executor, List<SkillArgument> arguments)
    {
        return 1;
    }
    public virtual bool CanCancel(SkillExecutor executor, List<SkillArgument> arguments)
    {
        return true;
    }

    public virtual bool CanTokenSeeSkill(SkillExecutor executor, Token seeing, List<SkillArgument> arguments)
    {
        return true;
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

    public virtual bool CanUseArgument(SkillExecutor executor, int index, SkillArgument arg)
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

    public Skill WithTags(params string[] tags)
    {
        Tags = [.. tags];
        return this;
    }
}

public class SkillData : ISerializable
{
    public readonly int Id;
    public string[] Layers;
    public Skill Skill;
    public List<SkillArgument> Arguments;
    public GenericComponentRef? Source;
    public SkillData(Skill skill, List<SkillArgument> args, string[] layers)
    {
        Id = new Random().Next();
        Skill = skill;
        Arguments = args;
        Layers = layers;
    }
    public SkillData(Stream stream)
    {
        Id = stream.ReadInt32();
        Skill = Skill.FromBytes(stream);
        if (stream.ReadBoolean())
            Source = new GenericComponentRef(stream);
        Layers = new string[stream.ReadByte()];
        for (int i = 0; i < Layers.Length; i++)
        {
            Layers[i] = stream.ReadString();
        }
        int count = stream.ReadByte();
        Arguments = new List<SkillArgument>();
        for (int i = 0; i < count; i++)
        {
            Arguments.Add(SkillArgument.FromBytes(stream));
        }
    }
    public void ToBytes(Stream stream)
    {
        stream.WriteInt32(Id);
        Skill.ToBytes(stream);
        if (Source == null)
            stream.WriteBoolean(false);
        else
        {
            stream.WriteBoolean(true);
            Source.ToBytes(stream);
        }
        stream.WriteByte((byte)Layers.Length);
        foreach (string layer in Layers)
            stream.WriteString(layer);
        stream.WriteByte((byte)Arguments.Count);
        foreach (SkillArgument arg in Arguments)
            arg.ToBytes(stream);
    }
}