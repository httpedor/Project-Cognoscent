using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Rpg;
public static class RpgMath
{
    const double EPSILON = 0.00001;
    private static bool IsEqualAprox(float a, float b){
        if (a == b)
            return true;
        return Math.Abs(a - b) < EPSILON;
    }

    public static bool SegmentIntersectSegment(Vector2 fromA, Vector2 toA, Vector2 fromB, Vector2 toB, out Vector2? intersection)
    {
        Vector2 B = toA - fromA;
        Vector2 C = fromB - fromA;
        Vector2 D = toB - fromB;
    
        // TIL the dot product of a vector with itself is the square of its length
        float ABLen = B.X * B.X + B.Y * B.Y;
        if (ABLen <= 0)
        {
            intersection = null;
            return false;
        }

        Vector2 Bn = B / ABLen;
        C = new Vector2(C.X * Bn.X + C.Y * Bn.Y, C.Y * Bn.X - C.X * Bn.Y);
        D = new Vector2(D.X * Bn.X + D.Y * Bn.Y, D.Y * Bn.X - D.X * Bn.Y);

        // Fail if C x B and D x B have the same sign (segments don't intersect).
        if ((C.Y < -EPSILON && D.Y < -EPSILON) || (C.Y >= EPSILON && D.Y >= EPSILON))
        {
            intersection = null;
            return false;
        }

        // Fail if segments are parallel or colinear.
		// (when A x B == zero, i.e (C - D) x B == zero, i.e C x B == D x B)
        if (IsEqualAprox(C.Y, D.Y)){
            intersection = null;
            return false;
        }

        float ABPos = D.X + (C.X - D.X) * D.Y / (D.Y - C.Y);
        
        // Fail if segment C-D intersects line A-B outside segment A-B.
        if ((ABPos < 0) || (ABPos > 1))
        {
            intersection = null;
            return false;
        }

        intersection = fromA + ABPos * B;
        return true;
    }
    public static bool SegmentIntersectSegment(Vector2 fromA, Vector2 toA, Vector2 fromB, Vector2 toB)
    {
        return SegmentIntersectSegment(fromA, toA, fromB, toB, out _);
    }

    public static float Lerp(float firstFloat, float secondFloat, float by)
    {
        return firstFloat * (1 - by) + secondFloat * by;
    }
    public static double Lerp(double firstDouble, double secondDouble, double by)
    {
        return firstDouble * (1 - by) + secondDouble * by;
    }

    public static float RandomFloat()
    {
        return (float) new Random().NextDouble();
    }
    public static float RandomFloat(float min, float max)
    {
        return min + (max - min) * RandomFloat();
    }
    public static int RandomInt()
    {
        return new Random().Next();
    }
    public static int RandomInt(int min, int max)
    {
        return new Random().Next(min, max);
    }

    public static double RandomGaussian(double mean, double stdDev = 0)
    {
        Random rand = new Random(); //reuse this if you are generating many
        double u1 = 1.0-rand.NextDouble(); //uniform(0,1] random doubles
        double u2 = 1.0-rand.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                    Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
        return mean + stdDev * randStdNormal; //random normal(mean,stdDev^2)
    }
}