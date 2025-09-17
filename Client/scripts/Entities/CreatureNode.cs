using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg;
using TTRpgClient.scripts.RpgImpl;
using TTRpgClient.scripts.ui;

namespace TTRpgClient.scripts;

public partial class CreatureNode(Creature ent, ClientBoard board) : EntityNode(ent, board)
{
    public Creature Creature { get; } = ent;
    
    public override void AddGMContextMenuOptions()
    {
        base.AddGMContextMenuOptions();

        ContextMenu.AddOption("Renomear", _ => {
            Modal.OpenStringDialog("Renomear Entidade", name => {
                if (name == null)
                    return;
                
                Creature.Name = name;
            }, true);
        });

        ContextMenu.AddOption("Danificar Parte", _ => {
            BodyInspector.Instance.Show(Creature.Body,
                new BodyInspector.BodyInspectorSettings(BodyInspector.BodyInspectorSettings.HEALTH)
                {
                    OnPick = bp =>
                    {
                        if (bp == null) return;
                        
                        Modal.OpenOptionsDialog("Tipo de Ferida", "Selecione o tipo de ferida que deseja aplicar", InjuryType.GetInjuryTypes().Select(i => i.Name).ToArray(), async typeTranslation => {
                            if (typeTranslation == null)
                                return;
                            InjuryType type = InjuryType.ByName(typeTranslation)!;
                            Modal.OpenStringDialog("Severidade da Ferida", sevStr => {
                                if (float.TryParse(sevStr, out float severity))
                                    NetworkManager.Instance.SendPacket(new EntityBodyPartInjuryPacket(bp, new Injury(type, severity)));
                            });
                        });
                    }
                }
            );
            ContextMenu.Hide();
        });
        ContextMenu.AddOption("Curar Ferida", _ => {
            BodyInspector.Instance.Show(Creature.Body, new BodyInspector.BodyInspectorSettings(BodyInspector.BodyInspectorSettings.HEALTH)
            {
                OnPick = bp => {
                    if (bp == null) return;
                    
                    Modal.OpenOptionsDialog("Ferida", "Selecione a ferida que deseja curar", bp.Injuries.Select(inj => inj.Type.Name + " - " + inj.Severity).ToArray(), selected => {
                        if (selected == null)
                            return;
                        string[] splitted = selected.Split(" - ");
                        InjuryType it = InjuryType.ByName(splitted[0])!;
                        if (float.TryParse(splitted[1], out float severity))
                            NetworkManager.Instance.SendPacket(new EntityBodyPartInjuryPacket(bp, new Injury(it, severity), true));
                    });
                }
            });
            ContextMenu.Hide();
        });
    }

    public override void AddContextMenuOptions()
    {
        base.AddContextMenuOptions();
        if (Board.OwnedSelectedEntity != Creature)
            return;

        if (Creature.SkillTree != null)
        {
            ContextMenu.AddOption("Habilidades", _ =>
            {
                SkillTreeDisplay.Tree = Creature.SkillTree;
                SkillTreeDisplay.Visible = true;
            });
        }
    }

    public override void _Ready()
    {
        base._Ready();
        Label = "TesteBaixo";
        
        Creature.ActionLayerChanged += (layer) =>
        {
            if (!IsInstanceValid(this))
                return;
            if (GameManager.Instance.CurrentBoard?.OwnedSelectedEntity == Creature)
            {
                ActionBar.Clear();
                ActionBar.PopulateWithSkills(Creature);
            }
        };
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (!Creature.ActiveActionLayers.Any())
        {
            HideLoadBar();
            return;
        }
        
        string layerName = Creature.ActiveActionLayers.First();
        string prefix = "";
        bool show = true;
        ActionLayer layer = Creature.GetActionLayer(layerName)!;
        string layerDesc = layer.Name;
        if (int.TryParse(layer.Id, out int skillId) && Creature.ActiveSkills.TryGetValue(skillId, out SkillData? data))
        {
            layerDesc = data.Skill.GetName();
            if (GameManager.Instance.CurrentBoard?.OwnedSelectedEntity != null && GameManager.Instance.CurrentBoard?.OwnedSelectedEntity != Creature)
                show = data.Skill.CanCreatureSeeSkill(Creature, GameManager.Instance.CurrentBoard.OwnedSelectedEntity, data.Arguments, data.Source.SkillSource);
        }

        if (layer.ExecutionStartTick > Board.CurrentTick)
        {
            prefix = "Preparando ";
            LoadBarFilling = 1 - ((layer.ExecutionStartTick - Board.CurrentTick) / (float)layer.Delay);
        }
        else
        {
            prefix = "Executando ";
            LoadBarFilling = 1 - ((layer.ExecutionEndTick - Board.CurrentTick) / (float)layer.Duration);
        }

        if (LoadBarLabel == null)
        {
            if (show)
            {
                LoadBarLabel = prefix + layerDesc;
            }
            else
            {
                LoadBarLabel = prefix + "...";
            }
        }
        
    }

    public void PlayAnimation(string animation)
    {
        
    }
}
