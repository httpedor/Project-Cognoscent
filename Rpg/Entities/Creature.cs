using System.Data.Common;
using System.Numerics;
using Rpg.Inventory;

namespace Rpg.Entities;

public struct CreatureRef
{
    public string Board;
    public int Id;
    public Creature? Creature
    {
        get
        {
            var board = SidedLogic.Instance.GetBoard(Board);
            if (board == null)
                return null;
            
            return board.GetEntityById(Id) as Creature;
        }
    }
    public CreatureRef(string board, int id)
    {
        Board = board;
        Id = id;
    }
    public CreatureRef(Creature target)
    {
        Board = target.Board.Name;
        Id = target.Id;
    }
    public CreatureRef(Stream stream)
    {
        Board = stream.ReadString();
        Id = stream.ReadInt32();
    }
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Board);
        stream.WriteInt32(Id);
    }
}

public class Creature : Entity, IInventoryHolder
{
    /// <summary>
    /// Called the exact tick an action is added to the activeActions.
    /// </summary>
    public event Action<Skill>? OnSkillStart;
    public event Action<Skill>? OnSkillCancel;

    public BodyPart BodyRoot => Body.Root;
    public Body Body;
    public List<Item> Inventory => throw new NotImplementedException();
    public List<Tuple<ISkillSource, Skill>> AvailableSkills {
        get {
            List<Tuple<ISkillSource, Skill>> ret = new();
            foreach (var bp in Body.Parts)
            {
                foreach (var action in bp.Skills)
                {
                    ret.Add(new Tuple<ISkillSource, Skill>(bp, action));
                }
            }
            return ret;
        }
    }

    public Dictionary<int, SkillData> ActiveSkills = new();
    public string Owner;
    public string Name;

    public Creature() : base()
    {
        Owner = "";
        Name = "";
    
        if (!SidedLogic.Instance.IsClient())
            SetupDefaultStats();
    }

    public Creature(Stream stream) : base(stream)
    {
        Owner = stream.ReadString();
        Body = new Body(stream, this);
        Name = stream.ReadString();
    }
    private void SetVital(string id)
    {
        GetStat(id).ValueChanged += (old, newVal) => {
            if (newVal <= 0)
            {
                Kill();
            }
        };
    }
    private void SetupDefaultStats()
    {
        foreach (var stat in CreatureStats.GetAllStats())
        {
            CreateStat(stat);
        }
    
        GetStat(CreatureStats.CONSCIOUSNESS).AddDependents((GetStat(CreatureStats.DEXTERITY), 1), (GetStat(CreatureStats.UTILITY_STRENGTH), 1), (GetStat(CreatureStats.MOVEMENT_STRENGTH), 1), (GetStat(CreatureStats.INTELLIGENCE), 1), (GetStat(CreatureStats.PERCEPTION), 0.5f), (GetStat(CreatureStats.SIGHT), 0.5f));
        GetStat(CreatureStats.BLOOD_FLOW).AddDependents((GetStat(CreatureStats.CONSCIOUSNESS), 0.2f), (GetStat(CreatureStats.MOVEMENT_STRENGTH), 0.2f));
        GetStat(CreatureStats.RESPIRATION).AddDependents((GetStat(CreatureStats.CONSCIOUSNESS), 0.2f), (GetStat(CreatureStats.MOVEMENT_STRENGTH), 0.2f));
        GetStat(CreatureStats.PERCEPTION).AddDependency(GetStat(CreatureStats.SIGHT), 0.8f);

        SetVital(CreatureStats.CONSCIOUSNESS);
        SetVital(CreatureStats.BLOOD_FLOW);
        SetVital(CreatureStats.RESPIRATION);
    }

    public bool HasOwner()
    {
        return !Owner.Equals("");
    }

    public override EntityType GetEntityType()
    {
        return EntityType.Creature;
    }
    
    public BodyPart? GetBodyPart(string path)
    {
        return BodyRoot.GetChildByPath(path);
    }
    
    public IEnumerable<BodyPart> GetAllBodyParts()
    {
        Stack<BodyPart> s = new Stack<BodyPart>();
        s.Push(BodyRoot);

        while (s.Count > 0)
        {
            BodyPart top = s.Pop();
            foreach (BodyPart child in top.Children)
                s.Push(child);
            
            yield return top;
        }
    }

    public void ExecuteSkill(Skill skill, ISkillSource from, List<SkillArgument> args)
    {
        if (!skill.ValidateArguments(args))
            throw new ArgumentException("Invalid arguments for skill " + skill.GetName());
        if (!skill.CanBeUsed(from))
            return;

        if (!Board.CombatMode)
        {
            skill.Execute(from, args);
            return;
        }
        var data = new SkillData(skill, args, Board.CombatTick);
        ActiveSkills[data.Id] = data;
    }
    public void CancelSkill(int id)
    {
        if (!ActiveSkills.ContainsKey(id))
            return;
        ActiveSkills.Remove(id);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(Owner);
        Body.ToBytes(stream);
        stream.WriteString(Name);
    }

    public override void ClearEvents()
    {
        base.ClearEvents();

        OnSkillStart = null;
        OnSkillCancel = null;
    }

    public void Kill()
    {
        Console.WriteLine(Id + "(" + Name + ") died.");
    }
}
