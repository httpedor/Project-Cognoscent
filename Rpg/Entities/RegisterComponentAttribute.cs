using System;

namespace Rpg.Entities;

/// <summary>
/// Marks a type as part of the component registry.
/// <para>
/// Membership used to be decided by matching the declaring file's path against the literal
/// <c>"/Rpg/Entities/Components/"</c>. That made a question about the code depend on where the code
/// happened to live: it broke on a folder rename, on linked or generated files, and on any checkout
/// path that happened to contain that string, and it could not be reasoned about without a
/// filesystem. Saying so in the source removes all of that.
/// </para>
/// <para>
/// On a <b>concrete class</b> it means "give this component an id and a slot in the entity's
/// component array". On an <b>abstract class or interface</b> it means "generate a lookup array of
/// the concrete components that derive from or implement this", e.g.
/// <c>Component.TickableIDs</c>.
/// </para>
/// <para>
/// Every concrete <see cref="Component"/> subclass is expected to carry it; the generator reports
/// <c>COMP003</c> for one that does not, so a component cannot silently drop out of the registry.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
public sealed class RegisterComponentAttribute : Attribute
{
}
