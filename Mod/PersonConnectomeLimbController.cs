using System;
using UnityEngine;

namespace Mod
{
    internal sealed class PersonConnectomeLimbController
    {
        private enum LimbRole
        {
            Other,
            Head,
            Core,
            Arm,
            Hand,
            Leg,
            Foot
        }

        private enum LimbSide
        {
            Center,
            Left,
            Right
        }

        private readonly LimbBehaviour limb;
        private readonly LimbRole role;
        private readonly LimbSide side;
        private readonly OwnedRegenerationRate bloodRate = new OwnedRegenerationRate();
        private readonly OwnedRegenerationRate limbRate = new OwnedRegenerationRate();

        public PersonConnectomeLimbController(LimbBehaviour limb, Transform root)
        {
            this.limb = limb;
            role = ClassifyRole(limb);
            side = ClassifySide(limb, root);
        }

        public bool IsConfigured => limb != null;

        // People Playground can clear IsCapable across the entire person for
        // global states such as submersion. It is not a reliable per-limb motor
        // gate, so nonterminal control uses only local joint/damage evidence.
        public bool CanDrive => limb != null && !HasLocalMotorFailure();

        public string DiagnosticSummary
        {
            get
            {
                if (limb == null) return "missing-limb";

                if (!limb.HasJoint) return limb.name + ":no-joint";
                var failure = LocalMotorFailure();
                if (failure != null) return limb.name + ":" + failure;
                return null;
            }
        }

        public bool Apply(MotorCommand command, float degreesPerSecond = 30f)
        {
            if (limb == null || !CanDrive)
            {
                Stop();
                return false;
            }

            var speed = ResolveSpeed(command);
            if (float.IsNaN(speed) || float.IsInfinity(speed) || float.IsNaN(degreesPerSecond) || float.IsInfinity(degreesPerSecond))
            {
                Stop();
                return false;
            }
            // InfluenceMotorSpeed consumes JointMotor2D.motorSpeed (degrees/s),
            // not a normalized amplitude. Strength and torque remain native.
            var targetSpeed = Mathf.Clamp(speed, -1f, 1f) * Mathf.Clamp(degreesPerSecond, 0f, 120f);
            if (limb.HasJoint) limb.InfluenceMotorSpeed(targetSpeed, ResolveInfluence());
            ApplyGrip(command);
            return limb.HasJoint;
        }

        public void Stop()
        {
            if (limb == null)
            {
                return;
            }

            if (limb.HasJoint)
            {
                // The game interpolates toward the target; full influence is required
                // to clear an old command, even when the limb has become incapable.
                limb.InfluenceMotorSpeed(0f, 1f);
            }

            var grip = limb.GripBehaviour;
            if (grip != null) grip.DropObject();
            RestoreChemistry();
        }

        private float ResolveSpeed(MotorCommand command)
        {
            switch (role)
            {
                case LimbRole.Head:
                    return command.Head;
                case LimbRole.Core:
                    return command.Core;
                case LimbRole.Arm:
                case LimbRole.Hand:
                    return SideValue(command.LeftArm, command.RightArm);
                case LimbRole.Leg:
                case LimbRole.Foot:
                    return SideValue(command.LeftLeg, command.RightLeg);
                default:
                    return command.Core;
            }
        }

        private float SideValue(float left, float right)
        {
            switch (side)
            {
                case LimbSide.Left:
                    return left;
                case LimbSide.Right:
                    return right;
                default:
                    return (left + right) * .5f;
            }
        }

        private float ResolveInfluence()
        {
            switch (role)
            {
                case LimbRole.Arm:
                case LimbRole.Hand:
                    return .22f;
                case LimbRole.Leg:
                case LimbRole.Foot:
                    return .3f;
                case LimbRole.Head:
                case LimbRole.Core:
                    return .18f;
                default:
                    return .15f;
            }
        }

        private void ApplyGrip(MotorCommand command)
        {
            var grip = limb.GripBehaviour;
            if (grip == null)
            {
                return;
            }

            var request = command.ReachGrab;
            if (side == LimbSide.Left)
            {
                request = command.LeftGrip;
            }
            else if (side == LimbSide.Right)
            {
                request = command.RightGrip;
            }
            if (float.IsNaN(request) || float.IsInfinity(request) || request < .7f)
            {
                grip.DropObject();
                return;
            }

            if (!grip.isHolding)
            {
                grip.Use(default(ActivationPropagation));
            }
        }

        private static LimbRole ClassifyRole(LimbBehaviour limb)
        {
            if (limb == null)
            {
                return LimbRole.Other;
            }

            var name = limb.name ?? String.Empty;
            if (ContainsAny(name, "hand", "finger", "thumb", "palm", "wrist"))
            {
                return LimbRole.Hand;
            }

            if (ContainsAny(name, "foot", "toe", "ankle"))
            {
                return LimbRole.Foot;
            }

            if (ContainsAny(name, "upperarm", "lowerarm", "forearm", "arm", "elbow"))
            {
                return LimbRole.Arm;
            }

            if (ContainsAny(name, "upperleg", "lowerleg", "thigh", "leg", "knee"))
            {
                return LimbRole.Leg;
            }

            if (ContainsAny(name, "head", "neck", "brain", "skull") || limb.HasBrain)
            {
                return LimbRole.Head;
            }

            if (ContainsAny(name, "body", "chest", "torso", "pelvis", "hip", "stomach", "waist"))
            {
                return LimbRole.Core;
            }

            return LimbRole.Other;
        }

        private static LimbSide ClassifySide(LimbBehaviour limb, Transform root)
        {
            if (limb == null)
            {
                return LimbSide.Center;
            }

            // Front/back is an explicit game-plane convention, not human anatomy.
            // Inspect the hierarchy too: stock arm/leg groups can carry the side name.
            for (var node = limb.transform; node != null && node != root; node = node.parent)
            {
                if (Contains(node.name, "left")) return LimbSide.Left;
                if (Contains(node.name, "right")) return LimbSide.Right;
            }
            for (var node = limb.transform; node != null && node != root; node = node.parent)
            {
                if (Contains(node.name, "front")) return LimbSide.Right;
                if (Contains(node.name, "back")) return LimbSide.Left;
            }
            // Horizontal location is not anatomical side in a side-on ragdoll.
            // Unknown naming deliberately receives the average of the two channels.
            return LimbSide.Center;
        }

        public void ApplyChemistry(MotorCommand command, bool enabled)
        {
            if (limb == null) return;
            if (!enabled || !CanDrive)
            {
                RestoreChemistry();
                return;
            }
            var heal = FiniteUnit(command.Heal);
            var circulation = limb.CirculationBehaviour;
            if (circulation != null)
                circulation.BloodRegenerationPerSecond = bloodRate.Apply(circulation.BloodRegenerationPerSecond, heal);
            limb.RegenerationSpeed = limbRate.Apply(limb.RegenerationSpeed, heal);
            var physical = limb.PhysicalBehaviour;
            var extinguish = FiniteUnit(command.Extinguish);
            if (physical != null && extinguish > .01f)
                physical.BurnIntensity = Mathf.Max(0f, physical.BurnIntensity - extinguish * .05f);
        }

        private void RestoreChemistry()
        {
            if (limb == null) return;
            var circulation = limb.CirculationBehaviour;
            if (circulation != null)
                circulation.BloodRegenerationPerSecond = bloodRate.Restore(circulation.BloodRegenerationPerSecond);
            limb.RegenerationSpeed = limbRate.Restore(limb.RegenerationSpeed);
        }

        private bool HasLocalMotorFailure()
        {
            return !limb.HasJoint || LocalMotorFailure() != null;
        }

        private string LocalMotorFailure()
        {
            if (limb == null) return "missing-limb";
            if (!limb.HasJoint) return "no-joint";
            if (limb.Broken || limb.CurrentlyShattered != 0) return "broken";
            if (limb.IsDismembered) return "dismembered";
            if (!IsFinite(limb.Health) || !IsFinite(limb.InitialHealth) || limb.InitialHealth <= 0f) return "invalid-health";
            if (limb.Health <= 0f) return "dead-limb";

            var physical = limb.PhysicalBehaviour;
            if (physical != null && physical.isDisintegrated) return "disintegrated";
            if (limb.IsParalysed) return "paralysed";

            var circulation = limb.CirculationBehaviour;
            if (circulation != null && (circulation.IsDisconnected || !circulation.HasCirculation)) return "disconnected";
            return null;
        }

        private static float FiniteUnit(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // Undo only our own last assignment. A newer game/other-mod write wins.
        private sealed class OwnedRegenerationRate
        {
            private bool owns;
            private float baseline, last;
            public float Apply(float current, float requested)
            {
                if (owns && current != last) owns = false;
                if (requested <= 0f) return Restore(current);
                if (!owns) baseline = current;
                last = Mathf.Max(baseline, requested);
                owns = last > baseline;
                return last;
            }
            public float Restore(float current)
            {
                var restored = owns && current == last ? baseline : current;
                owns = false;
                return restored;
            }
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            foreach (var part in parts)
            {
                if (Contains(value, part))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(string value, string part)
        {
            return !String.IsNullOrEmpty(part) && value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
