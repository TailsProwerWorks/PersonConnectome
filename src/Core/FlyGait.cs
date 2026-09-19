using System;

namespace ShadowNineX.PersonConnectome.Core
{
    /// <summary>Two-link inverse kinematics for a six-legged alternating tripod gait.</summary>
    internal static class FlyGait
    {
        public const float UpperLength = .23f;
        public const float LowerLength = .28f;
        public const float UpperJointTorque = 1.15f;
        public const float LowerJointTorque = .75f;
        // Wings are tiny native bodies, but the inherited Human hinge motor is
        // also fighting gravity and collider contact. The old .05 torque could
        // not overcome that load, so the visible wing pose stayed nearly still
        // while the thorax flight actuator moved underneath it.
        public const float WingJointTorque = .35f;
        public const float GroomingActivationThreshold = .35f;

        /// <summary>
        /// Filters tonic decoder activity so grooming is a deliberate idle
        /// behaviour rather than the sum of several weak background channels.
        /// </summary>
        public static float GroomingIntent(float antenna, float head, float leg, float abdomen,
            float locomotion, float flight, bool escaping)
        {
            if (escaping || Math.Abs(Finite(locomotion)) > .08f || Finite(flight) > .08f) return 0f;
            var strongest = Math.Max(Math.Max(Unit(antenna), Unit(head)), Math.Max(Unit(leg), Unit(abdomen)));
            return strongest >= GroomingActivationThreshold ? strongest : 0f;
        }

        /// <summary>Returns a size- and health-aware cap for a native hinge's break force.</summary>
        public static float BreakForceCap(float partMass, float healthFraction)
        {
            partMass = Finite(partMass);
            healthFraction = Clamp(Finite(healthFraction), 0f, 1f);
            var healthyCap = Clamp(partMass * 1000f, 8f, 45f);
            return healthyCap * (.25f + healthFraction * .75f);
        }

        /// <summary>Centers asymmetric fly art around the catalog spawn pivot.</summary>
        public static float SpawnCenterOffset(float minimumX, float maximumX)
        {
            minimumX = Finite(minimumX);
            maximumX = Finite(maximumX);
            return maximumX >= minimumX ? -(minimumX + maximumX) * .5f : 0f;
        }

        /// <summary>Wings detach cleanly instead of creating Human loose-tissue springs.</summary>
        public static bool RetainLooseTissue(bool isWing) => !isWing;

        public static void Pose(int leg, float phase, float drive, float flight, float grooming,
            out float upperDegrees, out float lowerDegrees)
        {
            if (leg < 0 || leg >= 6) throw new ArgumentOutOfRangeException(nameof(leg));
            var pair = leg / 2;
            var far = leg % 2 == 0;
            var tripod = far == (pair % 2 == 1);
            var cycle = (Finite(phase) + (tripod ? 0f : .5f)) % 1f;
            if (cycle < 0f) cycle += 1f;
            drive = Clamp(Finite(drive), -1f, 1f);
            var stride = .085f * drive;
            var x = pair == 0 ? -.18f : pair == 1 ? .015f : .20f;
            if (far) x -= .025f;
            var y = -.40f + (far ? -.01f : 0f);
            if (cycle < .6f)
            {
                x += stride * (cycle / .3f - 1f);
            }
            else
            {
                var swing = (cycle - .6f) / .4f;
                x += stride * (1f - 2f * swing);
                y += (float)Math.Sin(swing * Math.PI) * .115f * Math.Abs(drive);
            }
            if (Finite(flight) > .1f) { x *= .45f; y = -.22f; }
            if (Finite(grooming) > .1f && pair == 0) { x = -.28f; y = -.04f; }
            Solve(x, y, pair == 0 ? -1f : 1f, out upperDegrees, out lowerDegrees);
        }

        private static void Solve(float x, float y, float bend, out float upperDegrees, out float lowerDegrees)
        {
            var distance = Clamp((float)Math.Sqrt(x * x + y * y), .06f, UpperLength + LowerLength - .005f);
            var angle = Math.Atan2(y, x);
            var offset = Math.Acos(Clamp((UpperLength * UpperLength + distance * distance - LowerLength * LowerLength) /
                (2f * UpperLength * distance), -1f, 1f));
            var upper = angle + bend * offset;
            var kneeX = (float)Math.Cos(upper) * UpperLength;
            var kneeY = (float)Math.Sin(upper) * UpperLength;
            upperDegrees = (float)(upper * 180d / Math.PI) + 90f;
            lowerDegrees = (float)(Math.Atan2(y - kneeY, x - kneeX) * 180d / Math.PI) + 90f;
        }

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        private static float Unit(float value) => Clamp(Finite(value), 0f, 1f);
        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
