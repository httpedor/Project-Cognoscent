namespace Rpg.Entities.Components;

interface ICopyable<T>
{
    void CopyFrom(T other);
}