using System.Data.Common;
using System.Numerics;
using Rpg.Inventory;

namespace Rpg;

public readonly struct CreatureRef(string board, int id)
{
    public readonly string Board = board;
    public readonly int Id = id;
    public Creature? Creature
    {
        get
        {
            Board? board = SidedLogic.Instance.GetBoard(Board);
            return board?.GetEntityById(Id) as Creature;
        }
    }

    public CreatureRef(Creature target) : this(target.Board.Name, target.Id)
    {
    }
    public CreatureRef(Stream stream) : this(stream.ReadString(), stream.ReadInt32())
    {
    }
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Board);
        stream.WriteInt32(Id);
    }
}

public class ActionLayer(string name, string id, uint startTick, uint delay, uint duration, uint cooldown, bool cancelable = true) : ISerializable
{
    public string Name = name;
    public string Id = id;
    public uint StartTick = startTick;
    public uint Delay = delay;
    public uint Duration = duration;
    public uint Cooldown = cooldown;
    public bool Cancelable = cancelable;
    
    public uint ExecutionStartTick => StartTick + Delay + 1;
    public uint ExecutionEndTick => ExecutionStartTick + Duration;
    public uint EndTick => StartTick + Delay + Duration + Cooldown;
    
    public ActionLayer(Stream stream) : this(
        stream.ReadString(),
        stream.ReadString(),
        stream.ReadUInt32(),
        stream.ReadUInt32(),
        stream.ReadUInt32(),
        stream.ReadUInt32(),
        stream.ReadByte() != 0
        )
    {
        
    }
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        stream.WriteString(Id);
        stream.WriteUInt32(StartTick);
        stream.WriteUInt32(Delay);
        stream.WriteUInt32(Duration);
        stream.WriteUInt32(Cooldown);
        stream.WriteByte((byte)(Cancelable ? 1 : 0));
    }
}

public class Creature : Entity, IItemHolder, IDamageable
{
    /// <summary>
    /// Called the exact tick an action is added to the activeActions.
    /// </summary>
    public event Action<SkillData>? OnSkillStart;
    public event Action<SkillData>? OnSkillCancel;
    public event Action<ActionLayer>? ActionLayerChanged;
    public event Action<string>? ActionLayerRemoved;

    public BodyPart BodyRoot => Body.Root;

    public Body Body
    {
        get => field;
        set
        {
            if (field != null)
                field.Owner = null;
            field = value;
            field.Owner = this;
        }
    }
    public List<Tuple<ISkillSource, Skill>> AvailableSkills {
        get {
            List<Tuple<ISkillSource, Skill>> ret = new();
            foreach (BodyPart bp in Body.Parts)
            {
                ret.AddRange(bp.Skills.Select(skill => new Tuple<ISkillSource, Skill>(bp, skill)));

                foreach (Item item in bp.Items)
                {
                    ret.AddRange(item.Skills.Select(skill => new Tuple<ISkillSource, Skill>(item, skill)));

                    var ep = item.GetProperty<EquipmentProperty>();
                    if (ep != null)
                        ret.AddRange(ep.Skills.Select(skill => new Tuple<ISkillSource, Skill>(ep, skill)));
                }
            }

            if (SkillTree != null)
            {
                foreach (var entry in SkillTree.EnabledEntries)
                {
                    foreach (var skill in entry.Skills)
                    {
                        ret.Add(new Tuple<ISkillSource, Skill>(entry, skill));
                    }
                }
            }
            return ret;
        }
    }

    public IEnumerable<Item> Items
    {
        get
        {
            foreach (BodyPart bp in Body.PartsWithEquipSlots)
                foreach (Item item in bp.Items)
                    yield return item;
        }
    }

    Board? IItemHolder.Board => Board;

    public double Health => Body.Parts.Sum(p => p.Health);
    public double MaxHealth => Body.Parts.Sum(p => p.MaxHealth);

    private readonly Dictionary<string, ActionLayer> actionLayers = new();
    public IEnumerable<string> ActiveActionLayers => actionLayers.Keys;
    public readonly Dictionary<int, SkillData> ActiveSkills = new();
    public SkillTree? SkillTree;
    public string Owner;
    public float MovementSpeed => this[CreatureStats.MOVEMENT];

    public Vector2 TargetPos = Vector2.NaN;

    public Creature(Body body)
    {
        Owner = "";
        body.Owner = this;
        Body = body;
    }

    public Creature(Stream stream) : base(stream)
    {
        Owner = stream.ReadString();
        Name = stream.ReadString();
        if (stream.ReadBoolean())
            SkillTree = new SkillTree(stream)
            {
                Owner = this
            };
        Body = new Body(stream)
        {
            Owner = this
        };

        int count = stream.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var al = new ActionLayer(stream);
            actionLayers[al.Name] = al;
        }

        count = stream.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var sd = new SkillData(stream);
            ActiveSkills[sd.Id] = sd;
        }
    }

    public override void Tick()
    {
        base.Tick();
        foreach (var part in Body.Parts)
        {
            foreach (var feature in part.EnabledFeatures)
            {
                feature.OnTick(part);
            }
        }

        if (TargetPos != Vector2.NaN)
        {
            var result = Position + new Vector3((TargetPos - Position.XY()) * (MovementSpeed / 50), 0);
            bool canProceed = true;
            foreach (var line in Floor.PossibleOBBIntersections(Hitbox))
            {
                canProceed = !Geometry.OBBLineIntersection(Hitbox, line, out _);
                if (!canProceed)
                    break;
            }

            if (canProceed)
                Position = result;
        }
        if ((TargetPos - Position.XY()).Length() < 0.1)
            TargetPos = Vector2.NaN;
        
        var processed = new HashSet<int>();
        foreach (ActionLayer layer in actionLayers.Values.ToList())
        {
            bool cancelled = false;
            if (layer.ExecutionStartTick > Board.CurrentTick) continue;
            uint relativeTicks = Board.CurrentTick - layer.ExecutionStartTick;
            int skillId = int.Parse(layer.Id);
            if (!processed.Contains(skillId) && ActiveSkills.TryGetValue(skillId, out SkillData? data))
            {
                processed.Add(skillId);
                
                if (data.Source.SkillSource == null)
                {
                    CancelSkill(data.Id, true);
                    cancelled = true;
                    continue;
                }
                ISkillSource source = data.Source.SkillSource;

                uint oldDur = layer.Duration;
                layer.Duration = data.Skill.GetDuration(this, data.Arguments, source);
                if (oldDur != layer.Duration)
                    ActionLayerChanged?.Invoke(layer);
                bool canExecute = data.Skill.CanBeUsed(this, source);
                if (layer.ExecutionEndTick > Board.CurrentTick && canExecute)
                    data.Skill.Execute(this, data.Arguments, relativeTicks, source);

                if (layer.EndTick <= Board.CurrentTick || !canExecute)
                {
                    CancelSkill(data.Id, !canExecute);
                    cancelled = true;
                }
            }

            if (!cancelled && layer.EndTick < Board.CurrentTick)
            {
                CancelActionLayer(layer.Id);
            }
        }
    }
    private void SetVital(string id)
    {
        Stat? stat = GetStat(id);
        if (stat == null) return;
        stat.ValueChanged += (_, newVal) => {
            if (newVal <= 0 && Body.IsReady)
            {
                Kill();
            }
        };
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
    
    public bool CanAddItem(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();
        if (ep == null)
            return false;
        
        foreach (var bp in Body.GetPartsThatCanEquip(ep.Slot))
            if (bp.CanAddItem(item))
                return true;
        return false;
    }
    public void AddItem(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();

        foreach (var bp in Body.GetPartsThatCanEquip(ep.Slot))
            if (bp.GetEquippedItem(ep.Slot) == null)
                bp.AddItem(item);
    }
    public void RemoveItem(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();

        foreach (var bp in Body.GetPartsThatCanEquip(ep.Slot))
            if (bp.GetEquippedItem(ep.Slot) == null)
                bp.RemoveItem(item);
    }

    public ActionLayer? GetActionLayer(string layer)
    {
        return actionLayers!.GetValueOrDefault(layer, null);
    }

    public bool CanUseActionLayer(string layer)
    {
        ActionLayer? al = GetActionLayer(layer);
        if (al != null && al.EndTick >= Board.CurrentTick)
            return false;
        return true;
    }

    public void TriggerActionLayer(ActionLayer layer)
    {
        if (!CanUseActionLayer(layer.Name))
            return;
        actionLayers[layer.Name] = layer;
        ActionLayerChanged?.Invoke(layer);
    }
    
    public void CancelActionLayer(string layer)
    {
        if (!actionLayers.Remove(layer, out ActionLayer? al))
            return;
        ActionLayerRemoved?.Invoke(layer);
    }

    public void UpdateActionLayer(ActionLayer layer)
    {
        if (GetActionLayer(layer.Name) == null)
            return;
        actionLayers[layer.Name] = layer;
    }

    public bool CanExecuteSkill(Skill skill, ISkillSource source)
    {
        if (!skill.CanBeUsed(this, source))
            return false;
        return skill.GetLayers(this, source).All(CanUseActionLayer);
    }

    public void ExecuteSkill(Skill skill, List<SkillArgument> args, ISkillSource source)
    {
        if (!skill.ValidateArguments(args))
            throw new ArgumentException("Invalid arguments for skill " + skill.GetName());
        if (!CanExecuteSkill(skill, source))
            return;
        foreach (Feature feature in Features)
        {
            (bool, string?) result = feature.DoesExecuteSkill(this, skill, args);
            if (result.Item1) continue;
            
            if (result.Item2 != null)
                Log("Não é possível executar" + skill.BBHint + " porquê " + result.Item2);
            return;
        }

        if (!Board.TurnMode && skill.IsCombatSkill(this, args, source))
        {
            Board.StartTurnMode();
        }
        var data = new SkillData(skill, args, source, skill.GetLayers(this, source));

        foreach (string layer in data.Layers)
        {
            ActiveSkills[data.Id] = data;
            TriggerActionLayer(new ActionLayer(layer, data.Id.ToString(), Board.CurrentTick, Math.Max(skill.GetDelay(this, args, source), 0), Math.Max(skill.GetDuration(this, args, source), 0), Math.Max(skill.GetCooldown(this, args, source), 0), skill.CanCancel(this, args, source)));
        }
        skill.Start(this, args, source);
        OnSkillStart?.Invoke(data);
    }
    public void CancelSkill(int id, bool interrupted = false)
    {
        if (!ActiveSkills.Remove(id, out SkillData? skill))
            return;
        skill.Skill.Cancel(this, skill.Arguments, skill.Source.SkillSource!, interrupted);
        OnSkillCancel?.Invoke(skill);
        
        foreach (string layer in skill.Layers)
        {
            CancelActionLayer(layer);
        }
    }
    public void CancelSkill(string layer, bool interrupted = false)
    {
        foreach (var kvp in ActiveSkills)
        {
            if (!kvp.Value.Layers.Contains(layer)) continue;
            
            CancelSkill(kvp.Key, interrupted);
            return;
        }
    }

    public void Log(string message)
    {
        if (SidedLogic.Instance.IsClient())
            Board.AddChatMessage(message);
        else
            Board.BroadcastMessage(message);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(Name);
        stream.WriteString(Owner);
        if (SkillTree != null)
        {
            stream.WriteBoolean(true);
            SkillTree.ToBytes(stream);
        }
        else
            stream.WriteBoolean(false);
        Body.ToBytes(stream);

        stream.WriteInt32(actionLayers.Count);
        foreach (var kvp in actionLayers)
        {
            kvp.Value.ToBytes(stream);
        }

        stream.WriteInt32(ActiveSkills.Count);
        foreach (var kvp in ActiveSkills)
        {
            kvp.Value.ToBytes(stream);
        }
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

    public double Damage(DamageSource source, double damage)
    {
        var part = Body.Parts.ElementAt(new Random().Next(Body.Parts.Count()));
        return part.Damage(source, damage);
    }
    public double Damage(DamageSource source, double damage, string partPath)
    {
        var bp = GetBodyPart(partPath);
        if (bp != null)
            return bp.Damage(source, damage);
        return 0;
    }
}
