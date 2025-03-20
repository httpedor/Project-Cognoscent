
using Rpg.Entities;

namespace Rpg.Inventory;

public class EquipmentProperty : ItemProperty, ISkillSource
{
    /// <summary>
    /// The <see cref="EquipmentSlot"/> this equipment is equipped in.
    /// </summary>
    public string Slot;
    /// <summary>
    /// The body parts this equipment covers.
    /// </summary>
    public List<string> Coverage;
    public Dictionary<string, StatModifier> StatModifiers = new();
    public List<Skill> Skills = new();
    public BodyPart? EquippedPart;
    public EquipmentProperty(Item item, string equipmentSlot, params string[] coverage): base(item)
    {
        Slot = equipmentSlot;
        Coverage = coverage.ToList();
        EquippedPart = null;
    }

    protected EquipmentProperty(Stream stream) : base(stream)
    {
    }


    public String Name => Item.Name;

    IEnumerable<Skill> ISkillSource.Skills => Skills;

}
