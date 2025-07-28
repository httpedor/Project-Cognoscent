namespace Rpg;

public class Either<TLeft, TRight>
{
    public readonly TLeft? Left;
    public readonly TRight? Right;
    public bool IsLeft { get; }

    public bool IsRight => !IsLeft;

    public Either(TLeft left)
    {
        Left = left;
        IsLeft = true;
    }

    public Either(TRight right)
    {
        Right = right;
        IsLeft = false;
    }

    public T Match<T>(Func<TLeft?, T> leftFunc, Func<TRight?, T> rightFunc)
    {
        return IsLeft ? leftFunc(Left) : rightFunc(Right);
    }
    
    public static implicit operator Either<TLeft, TRight>(TLeft left) => new(left);
    public static implicit operator Either<TLeft, TRight>(TRight right) => new(right);
}
