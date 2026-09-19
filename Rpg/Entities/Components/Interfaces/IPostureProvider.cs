namespace Rpg.Entities.Interfaces;
[RegisterComponent]
public interface IPostureProvider
{
    IEnumerable<BodyPosture> GetProvidedPostures();
}