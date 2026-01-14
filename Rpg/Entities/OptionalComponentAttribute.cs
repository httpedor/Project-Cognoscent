namespace Rpg.Entities;

[AttributeUsage(AttributeTargets.Field |
                       AttributeTargets.Property)
]
public class OptionalComponentAttribute : Attribute
{
    public Type ComponentType { get; }
    public OptionalComponentAttribute(Type componentType)
    {
        ComponentType = componentType;
    }
}