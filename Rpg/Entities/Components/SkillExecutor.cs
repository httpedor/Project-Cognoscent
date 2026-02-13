using Rpg.Entities.Interfaces;
using Rpg.Features;
using Rpg.Skills;

namespace Rpg.Entities.Components;

public class ActionLayer(string name, string id, uint startTick, uint delay, uint duration, uint cooldown, float concentration, bool cancelable = true) : ISerializable
{
    public string Name = name;
    public string Id = id;
    public uint StartTick = startTick;
    public uint Delay = delay;
    public uint Duration = duration;
    public uint Cooldown = cooldown;
    public bool Cancelable = cancelable;

    public float Concentration = concentration;
    
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
        stream.ReadFloat(),
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
        stream.WriteFloat(Concentration);
        stream.WriteByte((byte)(Cancelable ? 1 : 0));
    }
}
public class ActionLayerChangedEvent : ComponentEvent
{
    public ActionLayer Layer;
    public ActionLayerChangedEvent(Component component, ActionLayer layer) : base(component)
    {
        Layer = layer;
    }
}
public class ActionLayerRemovedEvent : ComponentEvent
{
    public string LayerName;
    public ActionLayerRemovedEvent(Component component, string layerName) : base(component)
    {
        LayerName = layerName;
    }
}
public class SkillEvent(Component component, SkillData skillData) : ComponentEvent(component)
{
    public SkillData SkillData = skillData;
}
public class SkillStartEvent(Component component, SkillData skillData) : SkillEvent(component, skillData)
{
}
public class SkillCancelEvent(Component component, SkillData skillData, bool interrupted) : SkillEvent(component, skillData)
{
    public bool Interrupted = interrupted;
}
public partial class SkillExecutor : Component, ITickableComponent
{
    [RequiredComponent(typeof(StatsContainer))]
    private StatsContainer stats;
    private readonly Dictionary<string, ActionLayer> actionLayers = new();
    public IEnumerable<string> ActiveActionLayers => actionLayers.Keys;
    public readonly Dictionary<int, SkillData> ActiveSkills = new();

    public ActionLayer? GetActionLayer(string layer)
    {
        return actionLayers!.GetValueOrDefault(layer, null);
    }

    public bool CanUseActionLayer(string layer)
    {
        ActionLayer? al = GetActionLayer(layer);
        if (al != null && (Board == null || al.EndTick >= Board.CurrentTick))
            return false;
        return true;
    }

    public void TriggerActionLayer(ActionLayer layer)
    {
        if (!CanUseActionLayer(layer.Name))
            return;
        actionLayers[layer.Name] = layer;
        Entity.DispatchEvent(new ActionLayerChangedEvent(this, layer));
    }
    
    public void CancelActionLayer(string layer)
    {
        if (!actionLayers.Remove(layer, out ActionLayer? al))
            return;
        Entity.DispatchEvent(new ActionLayerRemovedEvent(this, layer));
    }

    public void UpdateActionLayer(ActionLayer layer)
    {
        if (GetActionLayer(layer.Name) == null)
            return;
        actionLayers[layer.Name] = layer;
    }
    public bool CanExecuteSkill(Skill skill)
    {
        if (!skill.CanBeUsed(this))
            return false;
        return skill.GetLayers(this).All(CanUseActionLayer);
    }

    public void ExecuteSkill(Skill skill, List<SkillArgument> args)
    {
        if (!skill.ValidateArguments(args))
            throw new ArgumentException("Invalid arguments for skill " + skill.GetName());
        if (Board == null)
        {
            Entity.Log(this, "Cannot execute skill when not in a board.", LogLevel.Error);
            return;
        }
        if (!CanExecuteSkill(skill))
            return;
        var cFeatures = Entity.FeaturesContainer;
        if (cFeatures != null)
        {
            foreach (Feature feature in cFeatures.EnabledFeatures)
            {
                (bool, string?) result = feature.DoesExecuteSkill(this, skill, args);
                if (result.Item1) continue;
                
                if (result.Item2 != null)
                    Entity.Log("Não é possível executar" + skill.BBHint + " porquê " + result.Item2);
                return;
            }
        }

        if (!Board.TurnMode && skill.IsCombatSkill(this, args))
        {
            Board.StartTurnMode();
        }
        var data = new SkillData(skill, args, skill.GetLayers(this));

        foreach (string layer in data.Layers)
        {
            ActiveSkills[data.Id] = data;
            TriggerActionLayer(new ActionLayer(layer, data.Id.ToString(), Board.CurrentTick, Math.Max(skill.GetDelay(this, args), 0), Math.Max(skill.GetDuration(this, args), 0), Math.Max(skill.GetCooldown(this, args), 0), 100, skill.CanCancel(this, args)));
        }
        skill.Start(this, args);
        Entity.DispatchEvent(new SkillStartEvent(this, data));
    }
    public void CancelSkill(int id, bool interrupted = false)
    {
        if (!ActiveSkills.Remove(id, out SkillData? skill))
            return;
        skill.Skill.Cancel(this, skill.Arguments, interrupted);
        Entity.DispatchEvent(new SkillCancelEvent(this, skill, interrupted));
        
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

    public void OnTick(uint tick)
    {
        if (SidedLogic.Instance.IsClient())
            return;
        var processed = new HashSet<int>();
        foreach (ActionLayer layer in actionLayers.Values.ToList())
        {
            bool cancelled = false;
            if (layer.ExecutionStartTick > Board!.CurrentTick) continue;
            uint relativeTicks = Board.CurrentTick - layer.ExecutionStartTick;
            int skillId = int.Parse(layer.Id);
            if (!processed.Contains(skillId) && ActiveSkills.TryGetValue(skillId, out SkillData? data))
            {
                processed.Add(skillId);
                
                uint oldDur = layer.Duration;
                layer.Duration = data.Skill.GetDuration(this, data.Arguments);
                if (oldDur != layer.Duration)
                    Entity.DispatchEvent(new ActionLayerChangedEvent(this, layer));
                bool canExecute = data.Skill.CanBeUsed(this);
                if (layer.ExecutionEndTick > Board.CurrentTick && canExecute)
                {
                    data.Skill.Execute(this, data.Arguments, relativeTicks);
                }

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
}