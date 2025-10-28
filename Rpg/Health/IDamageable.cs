namespace Rpg;

public interface IDamageable
{
    Board? Board
    {
        get;
    }
    double Health { get; }
    double MaxHealth { get; }
    double Damage(DamageSource source, double damage);

    string BBLink
    {
        get;
    }
}

public static class IDamageableExtensions
{
    extension(IDamageable damageable)
    {
        
    }
}