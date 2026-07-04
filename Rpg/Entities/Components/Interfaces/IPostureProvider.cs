namespace Rpg.Entities.Interfaces;
public interface IPostureProvider
{
    IEnumerable<BodyPosture> GetProvidedPostures();
}