using Godot;
using Rpg;
using System;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection.Metadata;
using System.Text;
using TTRpgClient.scripts;

public partial class BodyViewer : Tree
{
	public static BodyViewer Instance { get; private set; }
	Creature? selectedCreature = null;

	public BodyViewer()
	{
		Instance = this;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	private void AddPart(TreeItem parent, BodyPart part)
	{
		TreeItem item = parent.CreateChild();
		item.SetText(0, part.Name);
		item.SetText(1, $"{part.Health}/{part.MaxHealth}");
		var actions = "";
		foreach (var action in part.Skills)
		{
			actions += action.GetName();
			actions += ", ";
		}
		if (actions.Length >= 2)
			actions = actions.Substring(0, actions.Length - 2);
		item.SetText(2, actions);
		var stats = new StringBuilder("");
		foreach (var stat in part.ProvidedStats)
		{
			stats.Append(stat.Key);
			stats.Append(":");
			foreach (var mod in stat.Value)
			{
				stats.Append("\n");
				stats.Append("    ");
				stats.Append(mod.op);
				stats.Append(", ");
				stats.Append(mod.CalculateFor(part).Value);
			}
			stats.Append("\n");
		}
		item.SetText(3, stats.ToString());
		var flags = "";
		if (part.IsInternal)
			flags += "Internal, ";
		if (part.IsHard)
			flags += "Hard, ";
		if (part.IsSoft)
			flags += "Soft, ";
		if (part.HasFlag(BodyPart.Flag.Overlaps))
			flags += "Overlaps, ";
		if (flags.Length >= 2)
			flags = flags.Substring(0, flags.Length - 2);
		item.SetText(4, flags);

		item.SetCellMode(0, TreeItem.TreeCellMode.String);
		foreach (BodyPart child in part.Children)
		{
			AddPart(item, child);
		}
	}

	private void SetBody(BodyPart? root)
	{
		Clear();
		if (root == null)
			return;

		TreeItem rootItem = CreateItem();
		rootItem.SetText(0, "Nome");
		rootItem.SetCellMode(0, TreeItem.TreeCellMode.String);
		rootItem.SetText(1, "Vida");
		rootItem.SetCellMode(0, TreeItem.TreeCellMode.String);
		rootItem.SetText(2, "Skills");
		rootItem.SetCellMode(2, TreeItem.TreeCellMode.String);
		rootItem.SetText(3, "Stats");
		rootItem.SetCellMode(3, TreeItem.TreeCellMode.String);
		rootItem.SetText(4, "Flags");
		rootItem.SetCellMode(4, TreeItem.TreeCellMode.String);
		AddPart(rootItem, root);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (GameManager.Instance.CurrentBoard == null)
			return;

		var selected = GameManager.Instance.CurrentBoard.SelectedEntity;
		if (selected == null)
		{
			selectedCreature = null;
			SetBody(null);
			return;
		}
		if (selected is Creature creature)
		{
			if (selectedCreature != creature)
			{
				selectedCreature = creature;
				SetBody(creature.BodyRoot);
			}
		}
	}
}
