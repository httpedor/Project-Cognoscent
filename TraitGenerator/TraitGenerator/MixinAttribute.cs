using System;

namespace TraitGenerator;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class MixinAttribute(Type targetType) : Attribute
{
    public Type TargetType = targetType;
}