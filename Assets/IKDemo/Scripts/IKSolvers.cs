using System;
using UnityEngine;

namespace CourseIK
{
    /// <summary>Planar IK. p has N+1 points; lengths has N rigid links / rotating joints.</summary>
    public static class IKSolvers
    {
        public struct Result
        {
            public int iterations;
            public float error;
            public bool reachable;
        }

        public static float TotalLength(float[] lengths)
        {
            float sum = 0f;
            foreach (float length in lengths) sum += length;
            return sum;
        }

        private static Vector2 Direction(Vector2 v, Vector2 fallback)
        {
            return v.sqrMagnitude > 1e-12f ? v.normalized : fallback.normalized;
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        private static bool StretchIfUnreachable(Vector2[] p, float[] lengths, Vector2 target)
        {
            if ((target - p[0]).magnitude < TotalLength(lengths)) return false;
            Vector2 direction = Direction(target - p[0], Vector2.right);
            for (int i = 0; i < lengths.Length; i++) p[i + 1] = p[i] + direction * lengths[i];
            return true;
        }

        // A perfectly straight chain can get stuck when the target moves back inwards.
        // Seed a small deterministic bend, preserving the root and every link length.
        private static void UnstickCollinear(Vector2[] p, float[] lengths, Vector2 target)
        {
            Vector2 axis = Direction(target - p[0], Vector2.right);
            float largestCross = 0f;
            for (int i = 0; i < lengths.Length; i++)
            {
                Vector2 d = Direction(p[i + 1] - p[i], axis);
                largestCross = Mathf.Max(largestCross, Mathf.Abs(d.x * axis.y - d.y * axis.x));
            }
            if (largestCross > 0.001f || lengths.Length < 2) return;
            for (int i = 0; i < lengths.Length; i++)
            {
                float bend = (i % 2 == 0 ? 0.17f : -0.23f) + 0.025f * i;
                p[i + 1] = p[i] + Rotate(axis, bend) * lengths[i];
            }
        }

        private static void Validate(Vector2[] p, float[] lengths)
        {
            if (p == null || lengths == null || lengths.Length == 0 || p.Length != lengths.Length + 1)
                throw new ArgumentException("N links require N+1 joint positions.");
            foreach (float length in lengths)
                if (!(length > 0f) || float.IsInfinity(length)) throw new ArgumentException("Link lengths must be finite and positive.");
        }

        /// <summary>CCD: distal joint -> root; rotate downstream points rigidly about the pivot.</summary>
        public static Result CCD(Vector2[] p, float[] lengths, Vector2 target, int maxIterations = 120,
            float tolerance = 0.002f, float maxStepDegrees = 35f)
        {
            Validate(p, lengths);
            int last = p.Length - 1;
            if (StretchIfUnreachable(p, lengths, target))
                return new Result { reachable = false, iterations = 0, error = Vector2.Distance(p[last], target) };
            if (Vector2.Distance(p[last], target) > tolerance) UnstickCollinear(p, lengths, target);
            int iteration = 0;
            for (; iteration < maxIterations && Vector2.Distance(p[last], target) > tolerance; iteration++)
            {
                for (int i = last - 1; i >= 0; i--)
                {
                    Vector2 toEnd = p[last] - p[i], toTarget = target - p[i];
                    if (toEnd.sqrMagnitude < 1e-12f || toTarget.sqrMagnitude < 1e-12f) continue;
                    float delta = Mathf.Clamp(Vector2.SignedAngle(toEnd, toTarget), -maxStepDegrees, maxStepDegrees);
                    for (int j = i + 1; j <= last; j++)
                        p[j] = p[i] + Rotate(p[j] - p[i], delta * Mathf.Deg2Rad);
                    if (Vector2.Distance(p[last], target) <= tolerance) break;
                }
            }
            return new Result { reachable = true, iterations = iteration, error = Vector2.Distance(p[last], target) };
        }

        /// <summary>FABRIK: end -> root length projection, then root -> end length projection.</summary>
        public static Result FABRIK(Vector2[] p, float[] lengths, Vector2 target, int maxIterations = 120,
            float tolerance = 0.002f)
        {
            Validate(p, lengths);
            int last = p.Length - 1;
            if (StretchIfUnreachable(p, lengths, target))
                return new Result { reachable = false, iterations = 0, error = Vector2.Distance(p[last], target) };
            if (Vector2.Distance(p[last], target) > tolerance) UnstickCollinear(p, lengths, target);
            Vector2 root = p[0];
            int iteration = 0;
            for (; iteration < maxIterations && Vector2.Distance(p[last], target) > tolerance; iteration++)
            {
                p[last] = target;
                for (int i = last - 1; i >= 0; i--)
                    p[i] = p[i + 1] + Direction(p[i] - p[i + 1], Vector2.left) * lengths[i];
                p[0] = root;
                for (int i = 0; i < last; i++)
                    p[i + 1] = p[i] + Direction(p[i + 1] - p[i], Vector2.right) * lengths[i];
            }
            return new Result { reachable = true, iterations = iteration, error = Vector2.Distance(p[last], target) };
        }
    }
}
