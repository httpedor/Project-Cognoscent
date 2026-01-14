namespace Rpg.Entities;

public partial class SkillExecutorComponent : Component
{
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
            TriggerActionLayer(new ActionLayer(layer, data.Id.ToString(), Board.CurrentTick, Math.Max(skill.GetDelay(this, args, source), 0), Math.Max(skill.GetDuration(this, args, source), 0), Math.Max(skill.GetCooldown(this, args, source), 0), 100, skill.CanCancel(this, args, source)));
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
}