using System;
using System.Diagnostics.CodeAnalysis;

namespace Rpg;

public sealed class Either<TLeft, TRight>
{
    public TLeft? Left { get; }
    public TRight? Right { get; }

    // When IsLeft == true, Left is not null.
    // When IsLeft == false, Right is not null. This signals the analyzer that IsLeft implies !IsRight.
    [MemberNotNullWhen(true, nameof(Left))]
    [MemberNotNullWhen(false, nameof(Right))]
    [MemberNotNullWhen(false, nameof(IsRight))]
    public bool IsLeft { get; }

    [MemberNotNullWhen(true, nameof(Right))]
    [MemberNotNullWhen(false, nameof(IsLeft))]
    [MemberNotNullWhen(false, nameof(Left))]
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

    public T Match<T>(Func<TLeft, T> leftFunc, Func<TRight, T> rightFunc)
    {
        return IsLeft ? leftFunc(Left!) : rightFunc(Right!);
    }

    public static implicit operator Either<TLeft, TRight>(TLeft left) => new(left);
    public static implicit operator Either<TLeft, TRight>(TRight right) => new(right);
}
