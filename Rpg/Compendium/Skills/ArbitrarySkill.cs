using Rpg.Entities.Components;

namespace Rpg.Skills;

public class ArbitrarySkill : Skill
{
    private SkillExpressions code;
    public readonly string Id;
    
    private readonly string description;
    
    public ArbitrarySkill(string id, SkillExpressions code, string description, string name, string icon)
    {
        this.Id = id;
        this.code = code;

        CustomName = name;
        CustomIcon = icon;
        this.description = description;
    }

    public override void Start(SkillExecutor executor, List<SkillArgument> arguments)
    {
        base.Start(executor, arguments);
        code.onStart?.Eval(code.CreateEvalContext(executor, arguments));
    }

    public override void Execute(SkillExecutor executor, List<SkillArgument> arguments, uint tick)
    {
        base.Execute(executor, arguments, tick);
        code.onExecute?.Eval(code.CreateEvalContext(executor, arguments, new Dictionary<string, object> { { "tick", tick } }));
    }

    public override bool CanCancel(SkillExecutor executor, List<SkillArgument> arguments)
    {
        if (code.canCancel != null)
            return code.canCancel.Eval(code.CreateEvalContext(executor, arguments));
        return base.CanCancel(executor, arguments);
    }

    public override void Cancel(SkillExecutor executor, List<SkillArgument> arguments, bool interrupted = false)
    {
        base.Cancel(executor, arguments, interrupted);
        code.onCancel?.Eval(code.CreateEvalContext(executor, arguments, new Dictionary<string, object> { { "interrupted", interrupted } }));
    }

    public override string[] GetLayers(SkillExecutor executor)
    {
        if (code.layers != null)
            return code.layers.Select(layerExpr => layerExpr.Eval(executor.Entity)).ToArray();
        return base.GetLayers(executor);
    }

    public override uint GetDelay(SkillExecutor executor, List<SkillArgument> arguments)
    {
        if (code.delay != null)
            return (uint)code.delay.Eval(code.CreateEvalContext(executor, arguments));
        return base.GetDelay(executor, arguments);
    }

    public override uint GetCooldown(SkillExecutor executor, List<SkillArgument> arguments)
    {
        if (code.cooldown != null)
            return (uint)code.cooldown.Eval(code.CreateEvalContext(executor, arguments));
        return base.GetCooldown(executor, arguments);
    }

    public override uint GetDuration(SkillExecutor executor, List<SkillArgument> arguments)
    {
        if (code.duration != null)
            return (uint)code.duration.Eval(code.CreateEvalContext(executor, arguments));
        return base.GetDuration(executor, arguments);
    }

    public override string GetDescription()
    {
        return description;
    }


    public override Type[][] GetArguments()
    {
        return code.argumentTypes ?? base.GetArguments();
    }

    public override bool CanBeUsed(SkillExecutor executor)
    {
        if (code.canExecute != null)
            return code.canExecute.Eval(executor.Entity);
        return base.CanBeUsed(executor);
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteString(Id);
    }
}