using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg;
using Rpg.Inventory;
using TTRpgClient.scripts.RpgImpl;
using TTRpgClient.scripts.ui;

namespace TTRpgClient.scripts;

public partial class CreatureNode : EntityNode
{
    private static Texture2D handTexture = GD.Load<Texture2D>("res://assets/sprites/circle_gradient.tres");
    private Dictionary<string, Sprite2D> limbs = new();
    public Creature Creature { get; }

    public CreatureNode(Creature ent, ClientBoard board) : base(ent, board)
    {
        Creature = ent;

        /*var body = ent.Body;
        foreach (var hand in body.GetPartsWithSlot(EquipmentSlot.Hand))
        {
            var sprite = new Sprite2D();
            sprite.Texture = handTexture;
            sprite.ZIndex = 1;
            sprite.Visible = false;
            AddChild(sprite);
            limbs[hand.Path] = sprite;
        }*/

        ent.Body.OnInjuryAdded += (part, injury) =>
        {
            if (injury.Type == InjuryType.Cut)
            {
                
            }
        };

        if (GameManager.OwnsEntity(ent))
        {
            NameKnown = true;
            StatsKnown = true;
        }
    }
    
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

        var ose = GameManager.Instance?.CurrentBoard?.OwnedSelectedEntity;
        if (ose != null && ose != Creature)
        {
            ContextMenu.AddOption("Sussurrar", _ =>
            {
                Modal.OpenStringDialog("Sussurrar para " + Creature.Name, message =>
                {
                    if (message == null)
                        return;
                    
                    NetworkManager.Instance.SendPacket(new PrivateMessagePacket(ose, Creature, message));
                });
            });
        }

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
                show = data.Skill.CanCreatureSeeSkill(Creature, GameManager.Instance.CurrentBoard!.OwnedSelectedEntity, data.Arguments, data.Source.SkillSource!);
            
            ProcessAnimation(data, layer);
        }
        else
            ProcessAnimation(null);

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

    private void ProcessAnimation(SkillData? skillData, ActionLayer? layer = null)
    {
        return;
        if (skillData == null)
        {
            foreach (var limb in limbs.Values)
                limb.Visible = false;
            return;
        }

        string id = skillData.Skill switch
        {
            ArbitrarySkill arbSkill => arbSkill.Id,
            ArbitraryAttackSkill aas => aas.Id,
            _ => skillData.Skill.GetType().Name
        };
        if (skillData.Skill is AttackSkill)
        {
            
        }
    }
}
