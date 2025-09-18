using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Rpg;

namespace Rpg;

public class ArbitrarySkill : Skill
{
    private SkillJson code;
    public readonly string Id;
    
    private readonly string description;
    
    public ArbitrarySkill(string id, SkillJson code, string description, string name, string icon)
    {
        this.Id = id;
        this.code = code;

        CustomName = name;
        CustomIcon = icon;
        this.description = description;
    }

    public override void Start(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        base.Start(executor, arguments, source);
        code.onStart?.Invoke(new SkillJson.Context{ executor = executor, arguments = arguments, source = source });
    }

    public override void Execute(Creature executor, List<SkillArgument> arguments, uint tick, ISkillSource source)
    {
        base.Execute(executor, arguments, tick, source);
        code.onExecute?.Invoke(new SkillJson.Context { executor = executor, arguments = arguments, tick = tick, source = source });
    }

    public override bool CanCancel(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        if (code.canCancel != null)
            return code.canCancel(new SkillJson.Context { executor = executor, arguments = arguments, source = source });
        return base.CanCancel(executor, arguments, source);
    }

    public override void Cancel(Creature executor, List<SkillArgument> arguments, ISkillSource source, bool interrupted = false)
    {
        base.Cancel(executor, arguments, source, interrupted);
        code.onCancel?.Invoke(new SkillJson.Context { executor = executor, arguments = arguments, source = source, interrupted = interrupted});
    }

    public override string[] GetLayers(Creature executor, ISkillSource source)
    {
        if (code.layers != null)
            return code.layers(new SkillJson.Context { executor = executor, source = source});
        return base.GetLayers(executor, source);
    }

    public override uint GetDelay(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        if (code.delay != null)
            return code.delay(new SkillJson.Context { executor = executor, arguments = arguments, source = source });
        return base.GetDelay(executor, arguments, source);
    }

    public override uint GetCooldown(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        if (code.cooldown != null)
            return code.cooldown(new SkillJson.Context { executor = executor, arguments = arguments, source = source });
        return base.GetCooldown(executor, arguments, source);
    }

    public override uint GetDuration(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        if (code.duration != null)
            return code.duration(new SkillJson.Context { executor = executor, arguments = arguments, source = source });
        return base.GetDuration(executor, arguments, source);
    }

    public override string GetDescription()
    {
        return description;
    }

    public override bool CanBeUsed(Creature executor, ISkillSource source)
    {
        if (code.canExecute != null)
            return code.canExecute(new SkillJson.Context { executor = executor, source = source });
        return base.CanBeUsed(executor, source);
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteString(Id);
    }
}