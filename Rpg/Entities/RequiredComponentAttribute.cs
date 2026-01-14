namespace Rpg.Entities;

[AttributeUsage(AttributeTargets.Field |
                       AttributeTargets.Property)
]
public class RequiredComponentAttribute : Attribute
{
    public Type ComponentType { get; }
    public RequiredComponentAttribute(Type componentType)
    {
        ComponentType = componentType;
    }
}