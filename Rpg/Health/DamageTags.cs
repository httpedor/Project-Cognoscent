using Rpg.Health;

namespace Rpg;

public static class DamageTags
{
    public static readonly Tag<DamageType> Physical = new("physical");
    public static readonly Tag<DamageType> Thermal = new("thermal"); // Thermal damage is any damage that is caused by temperature.
    public static readonly Tag<DamageType> Sharp = new("sharp"); // Sharp damage is any damage that is caused by a sharp object, like a knife or a bullet.

    public static Tag<InjuryType> Exposed = new("exposed"); // This is used for injuries that expose the body part, like a cut that opens the skin, or a burn that chars it. This can be used to make certain injuries more likely to get infected, or to make them interact with other systems like cold exposure.
}