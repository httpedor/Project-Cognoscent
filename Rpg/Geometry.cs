using System.Numerics;
using System.Reflection;

namespace Rpg;

public class Line
{
    public Vector2 Start;
    public Vector2 End;

    public Line(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
    }

    public override Boolean Equals(Object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        Line other = (Line)obj;
        return Start.Equals(other.Start) && End.Equals(other.End);
    
    }
}

public class OBB
{
    public Vector2 Center;
    public Vector2 HalfSize;
    public float Angle; // In radians

    public Vector2 XAxis => new Vector2(MathF.Cos(Angle), MathF.Sin(Angle));
    public Vector2 YAxis => new Vector2(-MathF.Sin(Angle), MathF.Cos(Angle));

    /// <summary>
    /// Returns the corners of the OBB in clockwise order starting from the bottom left corner.
    /// </summary>
    public Vector2[] Corners
    {
        get
        {
            Vector2[] corners = new Vector2[4];
            Vector2 xAxis = XAxis;
            Vector2 yAxis = YAxis;

            corners[0] = Center - HalfSize.X * xAxis - HalfSize.Y * yAxis;
            corners[1] = Center + HalfSize.X * xAxis - HalfSize.Y * yAxis;
            corners[2] = Center + HalfSize.X * xAxis + HalfSize.Y * yAxis;
            corners[3] = Center - HalfSize.X * xAxis + HalfSize.Y * yAxis;

            return corners;
        }
    }

    public OBB(Vector2 center, Vector2 halfSize, float angle)
    {
        Center = center;
        HalfSize = halfSize;
        Angle = angle;
    }
}

public static class Geometry
{
    public static Vector2? LineLineIntersection(Vector2 start, Vector2 end, Vector2 p3, Vector2 p4)
    {
        Vector2 dir1 = end - start;
        Vector2 dir2 = p4 - p3;

        // Calculate determinants
        float determinant = dir1.X * dir2.Y - dir1.Y * dir2.X;
        if (Math.Abs(determinant) < 0.0001f) // Lines are parallel
        {
            return null;
        }

        // Calculate the intersection point
        Vector2 p = p3 - start;
        float t = (p.X * dir2.Y - p.Y * dir2.X) / determinant;
        float u = (p.X * dir1.Y - p.Y * dir1.X) / determinant;

        // Check if the intersection point is within the line segments
        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            return start + t * dir1;
        }
        return null;
    }

    public static Vector2? LinePointIntersection(Vector2 start, Vector2 end, Vector2 point)
    {
        Vector2 dir = end - start;
        Vector2 diff = point - start;

        float t = Vector2.Dot(diff, dir) / Vector2.Dot(dir, dir);
        if (t >= 0 && t <= 1)
        {
            return start + t * dir;
        }
        return null;
    }

    public static Vector2? LineCircleIntersection(Vector2 start, Vector2 end, Vector2 center, float radius)
    {
        Vector2 dir = end - start;
        Vector2 diff = start - center;

        float a = Vector2.Dot(dir, dir);
        float b = 2 * Vector2.Dot(dir, diff);
        float c = Vector2.Dot(diff, diff) - radius * radius;

        float discriminant = b * b - 4 * a * c;
        if (discriminant < 0)
        {
            return null;
        }

        float t1 = (-b + MathF.Sqrt(discriminant)) / (2 * a);
        float t2 = (-b - MathF.Sqrt(discriminant)) / (2 * a);

        if (t1 >= 0 && t1 <= 1)
        {
            return start + t1 * dir;
        }
        if (t2 >= 0 && t2 <= 1)
        {
            return start + t2 * dir;
        }
        return null;
    }

    public static bool OBBLineIntersection(OBB obb, Line line, out Vector2 MTV)
    {
        // Initialize the MTV
        MTV = Vector2.Zero;

        var start = line.Start;
        var end = line.End;

        // Transform the line into the OBB's local space
        Vector2 localStart = start - obb.Center;
        Vector2 localEnd = end - obb.Center;

        // Initialize the minimum overlap to a large value
        float minOverlap = float.MaxValue;

        // Check for intersection with the OBB along each axis
        Vector2[] axes = new Vector2[] { obb.XAxis, obb.YAxis };
        for (int i = 0; i < 2; i++)
        {
            // Project the line onto the axis
            float lineStart = Vector2.Dot(localStart, axes[i]);
            float lineEnd = Vector2.Dot(localEnd, axes[i]);

            // Calculate the line's minimum and maximum extents along the axis
            float lineMin = Math.Min(lineStart, lineEnd);
            float lineMax = Math.Max(lineStart, lineEnd);

            // Check for overlap with the OBB along the axis
            float halfSize = (i == 0) ? obb.HalfSize.X : obb.HalfSize.Y;
            if (lineMin >= halfSize || lineMax <= -halfSize)
            {
                // No overlap, so no intersection
                return false;
            }

            float obbMin = -halfSize;
            float obbMax = halfSize;

            // Calculate the overlap
            float overlap = MathF.Min(obbMax - lineMin, lineMax - obbMin);

            // If this is the smallest overlap so far, update the MTV
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                MTV = axes[i] * overlap;
            }
        }

        //Do the same for the line's direction
        Vector2 lineDir = Vector2.Normalize(end - start);
        for (int i = 0; i < 2; i++)
        {
            Vector2 axis = (i == 0) ? lineDir : new Vector2(-lineDir.Y, lineDir.X);
            float lineMinDir = Vector2.Dot(start, axis);
            float lineMaxDir = Vector2.Dot(end, axis);
            float obbMinDir = float.MaxValue;
            float obbMaxDir = float.MinValue;

            foreach (var corner in obb.Corners)
            {
                float dot = Vector2.Dot(corner, axis);
                obbMinDir = MathF.Min(obbMinDir, dot);
                obbMaxDir = MathF.Max(obbMaxDir, dot);
            }

            if (lineMinDir >= obbMaxDir || lineMaxDir <= obbMinDir)
            {
                return false;
            }

            float overlapDir = MathF.Min(obbMaxDir - lineMinDir, lineMaxDir - obbMinDir);
            if (overlapDir < minOverlap)
            {
                MTV = axis * overlapDir;
            }
        }

        if (Vector2.Dot(((start + end)/2) - obb.Center, MTV) < 0)
            MTV = -MTV;

        // The line intersects the OBB
        return true;
    }

    public static void ProjectOBB(OBB obb, Vector2 axis, out float min, out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;

        foreach (var corner in obb.Corners)
        {
            float projection = Vector2.Dot(corner, axis);
            if (projection < min) min = projection;
            if (projection > max) max = projection;
        }
    }
    public static bool OBBOBBIntersection(OBB obb1, OBB obb2, out Vector2 MTV)
    {
        MTV = Vector2.Zero;
        float minOverlap = float.MaxValue;
        Vector2 smallestAxis = Vector2.Zero;

        // Get all 4 axes to test (local axes of both OBBs)
        Vector2[] axes = new Vector2[]
        {
            obb1.XAxis,
            obb1.YAxis,
            obb2.XAxis,
            obb2.YAxis
        };

        foreach (Vector2 axis in axes)
        {
            // Project both OBBs onto the current axis
            ProjectOBB(obb1, axis, out float min1, out float max1);
            ProjectOBB(obb2, axis, out float min2, out float max2);

            // Check for overlap
            if (max1 < min2 || max2 < min1)
            {
                // Separating axis found, no intersection
                return false;
            }

            // Calculate overlap
            float overlap = MathF.Min(max1, max2) - MathF.Max(min1, min2);

            // Keep the smallest overlap for MTV
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                smallestAxis = axis;
            }
        }

        // Ensure the MTV points from obb1 to obb2
        Vector2 direction = obb2.Center - obb1.Center;
        if (Vector2.Dot(direction, smallestAxis) < 0)
        {
            smallestAxis = -smallestAxis;
        }

        MTV = smallestAxis * minOverlap;
        return true;
    }
}