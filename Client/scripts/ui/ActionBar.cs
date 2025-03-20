using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Rpg;
using Rpg.Entities;

namespace TTRpgClient.scripts.ui;

public static class ActionBar
{
    private static HBoxContainer container;

    static ActionBar()
    {
        container = new HBoxContainer()
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1f,
            AnchorBottom = 1,
            Alignment = BoxContainer.AlignmentMode.Center,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Begin,
            OffsetBottom = -8
        };
        GameManager.UILayer.AddChild(container);
        GameManager.UILayer.MoveChild(container, 2);
    }

    public static void AddButton(string id, string title, string desc, Texture2D icon, Action onClick, bool clickable = true)
    {
        var btn = new Button()
        {
            Name = id,
            Icon = icon,
            IconAlignment = HorizontalAlignment.Center,
            ExpandIcon = true,
            TooltipText = title + "\n" + desc,
            CustomMinimumSize = new Vector2(32, 32),
            Disabled = clickable
        };
        btn.ButtonUp += onClick;
        container.AddChild(btn);
    }
    public static void RemoveButton(string id)
    {
        var btn = GetButton(id);
        if (btn == null)
            return;
        container.RemoveChild(btn);
        btn.QueueFree();
    }

    public static Button? GetButton(string id)
    {
        var btn = container.FindChild(id);
        if (btn == null)
            return null;
        return (Button?)btn;
    }

    public static void Clear()
    {
        foreach (var child in container.GetChildren())
            child.QueueFree();
    }

    public static void Hide()
    {
        container.Hide();
    }
    public static void Show()
    {
        container.Show();
    }

    public static void PopulateWithSkills(Creature creature)
    {
        foreach (var (source, skill) in creature.AvailableSkills)
        {
            AddButton(source.Name+";"+skill.GetName(), skill.GetName(), skill.GetDescription() + "\n\nFonte: " + (source is BodyPart ? BodyPart.Parts.Translate(source.Name) : source.Name), Icons.GetIcon(skill.GetIconName()), async () => {
                List<SkillArgument> arguments = new();
                int index = 0;
                foreach (var argSet in skill.GetArguments())
                {
                    TaskCompletionSource<SkillArgument?> result = new();
                    if (argSet.Contains(typeof(BodyPartSkillArgument)))
                    {
                        Predicate<BodyPart> bpPredicate = (bp) => skill.CanUseArgument(source, index, new BodyPartSkillArgument(bp));
                        if (argSet.Contains(typeof(EntitySkillArgument)))
                        {
                            InputManager.Instance.RequestEntity((ent) => {
                                if (ent == null)
                                {
                                    result.SetResult(null);
                                    return;
                                }

                                if (ent is Creature c)
                                {
                                    BodyInspector.Instance.Show(c.Body, new BodyInspector.BodyInspectorSettings()
                                    {
                                        Predicate = bpPredicate,
                                        OnPick = (bp) => {
                                            if (bp == null)
                                                result.SetResult(null);
                                            else
                                                result.SetResult(new BodyPartSkillArgument(bp));
                                        }
                                    });
                                }
                                else
                                {
                                    result.SetResult(new EntitySkillArgument(ent));
                                }
                            }, (ent) => ent is Creature || skill.CanUseArgument(source, index, new EntitySkillArgument(ent)));
                        }
                        else
                        {
                            InputManager.Instance.RequestEntity((ent) => {
                                Creature c = ent as Creature;
                                BodyInspector.Instance.Show(c.Body, new BodyInspector.BodyInspectorSettings()
                                {
                                    Predicate = bpPredicate,
                                    OnPick = (bp) => {
                                        if (bp == null)
                                            result.SetResult(null);
                                        else
                                            result.SetResult(new BodyPartSkillArgument(bp));
                                    }
                                });
                            }, (ent) => ent is Creature);
                        }
                    }
                    else if (argSet.Contains(typeof(EntitySkillArgument)))
                    {
                        Predicate<Entity> predicate = (ent) => skill.CanUseArgument(source, index, new EntitySkillArgument(ent));
                        InputManager.Instance.RequestEntity((ent) => {
                            if (ent == null)
                                result.SetResult(null);
                            else
                                result.SetResult(new EntitySkillArgument(ent));
                        }, predicate);
                    }

                    if (argSet.Contains(typeof(PositionSkillArgument)))
                    {
                        Predicate<Vector3> predicate = (pos) => skill.CanUseArgument(source, index, new PositionSkillArgument(pos.ToNumerics()));
                        InputManager.Instance.RequestPosition((pos) => {
                            if (pos == null)
                                result.SetResult(null);
                            else
                                result.SetResult(new PositionSkillArgument(((Vector3)pos).ToNumerics()));
                        }, predicate);
                    }

                    if (argSet.Contains(typeof(BooleanSkillArgument)))
                    {
                        Modal.OpenConfirmationDialog("Action Boolean", "Sim ou Não", (b) => {
                            result.SetResult(new BooleanSkillArgument(b));
                        }, "Sim", "Não");
                    }

                    var arg = await result.Task;
                    if (arg == null)
                        return;
                    arguments.Add(arg);
                    index++;
                }

                
            }, !skill.CanBeUsed(source));
        }
    }
}
