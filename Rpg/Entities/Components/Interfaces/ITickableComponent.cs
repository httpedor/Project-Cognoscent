namespace Rpg.Entities.Interfaces;

[RegisterComponent]
public interface ITickableComponent
{
    virtual void PreTick()
    {

    }
    virtual void OnTick()
    {

    }
    virtual void PostTick()
    {

    }
}