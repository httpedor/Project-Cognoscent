using System.Numerics;

namespace Rpg;

public class TeleportSkill : Skill
{
    public TeleportSkill() : base()
    {
        
    }

    public TeleportSkill(Stream stream) : base(stream)
    {
        
    }
    public override string GetDescription()
    {
        return "Teleporta para um local";
    }

    public override string GetName()
    {
        return "Teleportar";
    }

    public override string GetIconName()
    {
        return "lightning";
    }

    public override void Start(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        Console.WriteLine("Teleportando para " + (arguments[0] as PositionSkillArgument)!.Position);
    }

    public override void Execute(Creature executor, List<SkillArgument> arguments, uint tick, ISkillSource source)
    {
        base.Execute(executor, arguments, tick, source);
        executor.Position = (arguments[0] as PositionSkillArgument)!.Position;
    }

    public override Type[][] GetArguments()
    {
        return [[typeof(PositionSkillArgument)]];
    }

    public override uint GetDelay(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        return 100; //2 seconds
    }
}
