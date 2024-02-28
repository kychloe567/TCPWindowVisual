using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TCPServer
{
    public readonly struct LineSegment
    {
        public LineSegment(Vector2 from, Vector2 to) : this()
        {
            From = from;
            To = to;
        }

        public Vector2 From { get; }
        public Vector2 To { get; }

        public float Length { get => Vector2.Distance(From, To); }
        public Vector2 Direction { get => Vector2.Normalize(To - From); }
        public Vector2 Normal { get => Vector2.Normalize(new Vector2(-(To.Y - From.Y), (To.X - From.X))); }

        public bool IsFinite
        {
            get => !float.IsNaN(From.X) && !float.IsNaN(From.Y)
                && !float.IsNaN(To.X) && !float.IsNaN(To.Y);
        }
        /// <summary>
        /// Check if a point is contained within the line segment
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool Contains(Vector2 target, bool inclusive = true)
        {
            if (GeometryTools.TryJoin(From, To, out var line))
            {
                target = line.Project(target);
                Vector2 dir = Direction;
                float t = Vector2.Dot(dir, target - From) / Vector2.Dot(dir, To - From);
                return inclusive ? t >= 0 && t <= 1 : t > GeometryTools.TINY && t < 1 - GeometryTools.TINY;
            }
            return false;
        }
        /// <summary>
        /// Try to intersect two line segments
        /// </summary>
        /// <param name="other">The other line segment.</param>
        /// <param name="point">The intersection point.</param>
        /// <returns>True if they intersect, False otherwise</returns>
        public bool TryIntersect(LineSegment other, out Vector2 point, bool inclusive = true)
        {
            point = Vector2.Zero;

            if (GeometryTools.TryJoin(From, To, out InfiniteLine thisLine)
                && GeometryTools.TryJoin(other.From, other.To, out InfiniteLine otherLine))
            {
                if (GeometryTools.TryMeet(thisLine, otherLine, out point))
                {
                    return Contains(point, inclusive) && other.Contains(point, inclusive);
                }
            }
            return false;
        }
        public override string ToString() => $"[{From}-{To}]";
    }

    public readonly struct InfiniteLine
    {
        /// <summary>
        /// The line at the horizon (not on the Eucledian plane).
        /// </summary>
        public static readonly InfiniteLine Horizon = new InfiniteLine(0, 0, 1);

        public InfiniteLine(float a, float b, float c) : this()
        {
            this.Coeff = (a, b, c);
            float m = (float)Math.Sqrt(a * a + b * b);
            this.IsFinite = m > GeometryTools.TINY;
        }
        /// <summary>
        /// The (a,b,c) coefficients define a line by the equation: <code>a*x+b*y+c=0</code>
        /// </summary>
        public (float a, float b, float c) Coeff { get; }

        /// <summary>
        /// True if line is in finite space, False if line is at horizon.
        /// </summary>
        public bool IsFinite { get; }

        /// <summary>
        /// Check if point belongs to the infinite line.
        /// </summary>
        /// <param name="point">The target point.</param>
        /// <returns>True if point is one the line.</returns>
        public bool Contains(Vector2 point)
        {
            return IsFinite
                && Math.Abs(Coeff.a * point.X + Coeff.b * point.Y + Coeff.c) <= GeometryTools.TINY;
        }

        /// <summary>
        /// Projects a target point onto the line.
        /// </summary>
        /// <param name="target">The target point.</param>
        /// <returns>The point on the line closest to the target.</returns>
        /// <remarks>If line is not finite the resulting point has NaN or Inf coordinates.</remarks>
        public Vector2 Project(Vector2 target)
        {
            (float a, float b, float c) = Coeff;
            float m2 = a * a + b * b;
            float px = b * (b * target.X - a * target.Y) - a * c;
            float py = a * (a * target.Y - b * target.X) - b * c;
            return new Vector2(px / m2, py / m2);
        }
        public override string ToString() => $"({Coeff.a})*x+({Coeff.b})*y+({Coeff.c})=0";
    }

    public static class GeometryTools
    {
        /// <summary>
        /// The value of 2^-19 is tiny
        /// </summary>
        public const float TINY = 1f / 524288;

        /// <summary>
        /// Try to join two points with an infinite line.
        /// </summary>
        /// <param name="A">The first point.</param>
        /// <param name="B">The second point.</param>
        /// <param name="line">The line joining the two points.</param>
        /// <returns>False if the two points are coincident, True otherwise.</returns>
        public static bool TryJoin(Vector2 A, Vector2 B, out InfiniteLine line)
        {
            float dx = B.X - A.X, dy = B.Y - A.Y;
            float m = A.X * B.Y - A.Y * B.X;
            line = new InfiniteLine(-dy, dx, m);
            return line.IsFinite;
        }
        /// <summary>
        /// Try to find the point where two infinite lines meet.
        /// </summary>
        /// <param name="L">The fist line.</param>
        /// <param name="M">The second line.</param>
        /// <param name="point">The point where the two lines meet.</param>
        /// <returns>False if the two lines are parallel, True othrwise.</returns>
        public static bool TryMeet(InfiniteLine L, InfiniteLine M, out Vector2 point)
        {
            (float a1, float b1, float c1) = L.Coeff;
            (float a2, float b2, float c2) = M.Coeff;
            float d = a1 * b2 - a2 * b1;
            if (d != 0)
            {
                point = new Vector2((b1 * c2 - b2 * c1) / d, (a2 * c1 - a1 * c2) / d);
                return true;
            }
            point = Vector2.Zero;
            return false;
        }
    }
}
