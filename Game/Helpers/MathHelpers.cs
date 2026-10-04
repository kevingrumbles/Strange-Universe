using Microsoft.Xna.Framework;

namespace Strange_Universe;

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>Angle and interpolation helpers.</summary>
public static class MathHelpers
{
    /// <summary>Returns true if both components are finite numbers.</summary>
    public static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

    /// <summary>Normalizes a vector, returning <paramref name="fallback"/> for zero-length or non-finite input.</summary>
    public static Vector2 SafeNormalize(Vector2 v, Vector2 fallback)
    {
        float lenSq = v.LengthSquared();
        if (lenSq < 1e-6f || !float.IsFinite(lenSq))
            return fallback;
        return v / System.MathF.Sqrt(lenSq);
    }

    /// <summary>Wraps an angle to the range [-pi, pi] for the shortest-path rotation calc.</summary>
    public static float WrapAngle(float angle)
    {
        // Guard against NaN/Infinity propagating through rotation math:
        // modulo on a non-finite value stays non-finite.
        if (float.IsNaN(angle) || float.IsInfinity(angle)) return 0f;

        angle %= MathHelper.TwoPi;
        if (angle > MathHelper.Pi) angle -= MathHelper.TwoPi;
        if (angle < -MathHelper.Pi) angle += MathHelper.TwoPi;
        return angle;
    }

    /// <summary>
    /// Smoothstep-interpolates the radial profile between the nearest two control angles.
    /// C1 continuity avoids the hard corners produced by linear interpolation.
    /// </summary>
    public static float AsteroidInterpolatedRadius(float angle, float[] angles, float[] radii)
    {
        int n = angles.Length;
        float a = (angle + MathHelper.TwoPi) % MathHelper.TwoPi;

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            float a0 = angles[i];
            float a1 = angles[next];
            if (next == 0) a1 += MathHelper.TwoPi;

            if (a >= a0 && a < a1)
            {
                float t = (a - a0) / (a1 - a0);
                t = t * t * (3f - 2f * t);   // smoothstep
                return MathHelper.Lerp(radii[i], radii[next], t);
            }
        }
        return radii[0];
    }

    public static float StarDeltaAngle(float a, float b)
    {
        float d = (a - b + MathHelper.TwoPi) % MathHelper.TwoPi;
        if (d > MathHelper.Pi) d -= MathHelper.TwoPi;
        return d;
    }
}
