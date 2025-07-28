namespace Rpg;

public interface ICustomDataContainer
{
    public byte[]? GetCustomData(string id);
    public void SetCustomData(string id, byte[]? data);
}

public static class CustomDataContainerExtensions
{
    public static void RemoveCustomData(this ICustomDataContainer container, string id)
    {
        container.SetCustomData(id, null);
    }
}