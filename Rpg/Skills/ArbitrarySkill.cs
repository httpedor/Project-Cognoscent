using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Rpg;

namespace Rpg;

public class ArbitrarySkill : Skill
{
    public class Context
    {
        public Creature? executor;
        public List<SkillArgument>? arguments;
        public ISkillSource? source;
        public uint? tick;
        public bool? interrupted;
    }

    private readonly string id;
    private readonly Func<Context, object>? onExecute;
    private readonly Func<Context, object>? onStart;
    private readonly Func<Context, object>? onCancel;
    private readonly Func<Context, bool>? canCancel;
    private readonly Func<Context, uint>? delay;
    private readonly Func<Context, uint>? cooldown;
    private readonly Func<Context, uint>? duration;
    private readonly Func<Context, string[]>? layers;
    
    private readonly string description;
    
    public ArbitrarySkill(string id,
        string? execute, 
        string? start, 
        string? cancel, 
        string? canCancel,
        string? delay,
        string? cooldown,
        string? duration,
        string? layers,
        string description,
        string name,
        string icon)
    {
        this.id = id;

        if (!SidedLogic.Instance.IsClient())
        {
            Func<Context, T> Compile<T>(string code)
            {
                var script = CSharpScript.Create<T>(code, ScriptOptions.Default.WithReferences(typeof(Creature).Assembly).WithImports("Rpg"), typeof(Context)).CreateDelegate();
                return ctx => script(ctx).Result;
            }
            if (execute != null) onExecute = Compile<object>(execute);
            if (start != null) onStart = Compile<object>(start);
            if (cancel != null) onCancel = Compile<object>(cancel);
            if (canCancel != null) this.canCancel = Compile<bool>(canCancel);
            if (delay != null) this.delay = Compile<uint>(delay);
            if (cooldown != null) this.cooldown = Compile<uint>(cooldown);
            if (duration != null) this.duration = Compile<uint>(duration);
            if (layers != null) this.layers = Compile<string[]>(layers);
        }

        CustomName = name;
        CustomIcon = icon;
        this.description = description;
    }

    public override void Start(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        base.Start(executor, arguments, source);
        if (onStart != null)
            onStart(new Context{ executor = executor, arguments = arguments, source = source });
    }

    public override void Execute(Creature executor, List<SkillArgument> arguments, uint tick, ISkillSource source)
    {
        base.Execute(executor, arguments, tick, source);
        if (onExecute != null)
            onExecute(new Context { executor = executor, arguments = arguments, tick = tick, source = source });
    }

    public override bool CanCancel(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        if (canCancel != null)
            return canCancel(new Context { executor = executor, arguments = arguments, source = source });
        return base.CanCancel(executor, arguments, source);
    }

    public override void Cancel(Creature executor, List<SkillArgument> arguments, ISkillSource source, bool interrupted = false)
    {
        base.Cancel(executor, arguments, source, interrupted);
        if (onCancel != null)
            onCancel(new Context { executor = executor, arguments = arguments, source = source, interrupted = interrupted});
    }

    public override string[] GetLayers(Creature executor, ISkillSource source)
    {
        if (layers != null)
            return layers(new Context { executor = executor, source = source});
        return base.GetLayers(executor, source);
    }

    public override uint GetDelay(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        if (delay != null)
            return delay(new Context { executor = executor, arguments = arguments, source = source });
        return base.GetDelay(executor, arguments, source);
    }

    public override uint GetCooldown(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        if (cooldown != null)
            return cooldown(new Context { executor = executor, arguments = arguments, source = source });
        return base.GetCooldown(executor, arguments, source);
    }

    public override uint GetDuration(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        if (duration != null)
            return duration(new Context { executor = executor, arguments = arguments, source = source });
        return base.GetDuration(executor, arguments, source);
    }

    public override string GetDescription()
    {
        return description;
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteString(id);
    }
}