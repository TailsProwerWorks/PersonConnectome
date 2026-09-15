using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mod.Adapters
{
    /// <summary>
    /// Conservative, deterministic tuning for the optional person standing
    /// assist. Values are intentionally modest because People Playground
    /// remains responsible for the ragdoll's actual physical constraints.
    /// </summary>
    internal sealed class PersonStandingControllerParameters
    {
        public float TiltProportional = .42f;
        public float TiltDerivative = .08f;
        public float JointAngleProportional = .08f;
        public float HorizontalVelocityDerivative = .35f;
        public float DesiredHeight = 1.1f;
        public float HeightProportional = 2f;
        public float MaximumMotorSpeed = 28f;
        public float MaximumMotorAcceleration = 90f;
        public float MotorInfluence = .16f;
        public float WalkContribution = 7f;
        public float SideBiasContribution = 5f;
        public float CorePostureScale = .55f;
        public float HeadPostureScale = .2f;
        public float ArmPostureScale = .3f;
        public float UnsupportedLegScale = .35f;

        public PersonStandingControllerParameters Clone()
        {
            return new PersonStandingControllerParameters
            {
                TiltProportional = TiltProportional,
                TiltDerivative = TiltDerivative,
                JointAngleProportional = JointAngleProportional,
                HorizontalVelocityDerivative = HorizontalVelocityDerivative,
                DesiredHeight = DesiredHeight,
                HeightProportional = HeightProportional,
                MaximumMotorSpeed = MaximumMotorSpeed,
                MaximumMotorAcceleration = MaximumMotorAcceleration,
                MotorInfluence = MotorInfluence,
                WalkContribution = WalkContribution,
                SideBiasContribution = SideBiasContribution,
                CorePostureScale = CorePostureScale,
                HeadPostureScale = HeadPostureScale,
                ArmPostureScale = ArmPostureScale,
                UnsupportedLegScale = UnsupportedLegScale
            };
        }

        public void CopyFrom(PersonStandingControllerParameters source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            TiltProportional = source.TiltProportional;
            TiltDerivative = source.TiltDerivative;
            JointAngleProportional = source.JointAngleProportional;
            HorizontalVelocityDerivative = source.HorizontalVelocityDerivative;
            DesiredHeight = source.DesiredHeight;
            HeightProportional = source.HeightProportional;
            MaximumMotorSpeed = source.MaximumMotorSpeed;
            MaximumMotorAcceleration = source.MaximumMotorAcceleration;
            MotorInfluence = source.MotorInfluence;
            WalkContribution = source.WalkContribution;
            SideBiasContribution = source.SideBiasContribution;
            CorePostureScale = source.CorePostureScale;
            HeadPostureScale = source.HeadPostureScale;
            ArmPostureScale = source.ArmPostureScale;
            UnsupportedLegScale = source.UnsupportedLegScale;
            Validate();
        }

        public void Validate()
        {
            TiltProportional = Mathf.Clamp(Finite(TiltProportional), 0f, 4f);
            TiltDerivative = Mathf.Clamp(Finite(TiltDerivative), 0f, 2f);
            JointAngleProportional = Mathf.Clamp(Finite(JointAngleProportional), 0f, 2f);
            HorizontalVelocityDerivative = Mathf.Clamp(Finite(HorizontalVelocityDerivative), 0f, 4f);
            DesiredHeight = Mathf.Clamp(Finite(DesiredHeight), 0f, 4f);
            HeightProportional = Mathf.Clamp(Finite(HeightProportional), 0f, 8f);
            MaximumMotorSpeed = Mathf.Clamp(Finite(MaximumMotorSpeed), 1f, 120f);
            MaximumMotorAcceleration = Mathf.Clamp(Finite(MaximumMotorAcceleration), 1f, 480f);
            MotorInfluence = Mathf.Clamp(Finite(MotorInfluence), .01f, 1f);
            WalkContribution = Mathf.Clamp(Finite(WalkContribution), 0f, 30f);
            SideBiasContribution = Mathf.Clamp(Finite(SideBiasContribution), 0f, 30f);
            CorePostureScale = Mathf.Clamp(Finite(CorePostureScale), 0f, 2f);
            HeadPostureScale = Mathf.Clamp(Finite(HeadPostureScale), 0f, 2f);
            ArmPostureScale = Mathf.Clamp(Finite(ArmPostureScale), 0f, 2f);
            UnsupportedLegScale = Mathf.Clamp(Finite(UnsupportedLegScale), 0f, 1f);
        }

        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }

    /// <summary>
    /// Adapter-local PD posture controller. It does not alter fly neural
    /// output: fly-derived requests merely contribute gentle walk and lateral
    /// bias while measured torso posture remains the dominant input.
    /// </summary>
    internal sealed class PersonStandingController
    {
        private const float DefaultStepSeconds = .05f;
        private readonly PersonStandingControllerParameters parameters;
        private readonly List<RateState> rateStates = [];

        private struct RateState
        {
            public LimbBehaviour Limb;
            public float PreviousSpeed;
            public int LastSeenEvaluation;
        }

        public PersonStandingController(PersonStandingControllerParameters? parameters = null)
        {
            this.parameters = parameters ?? new PersonStandingControllerParameters();
            this.parameters.Validate();
        }

        internal PersonStandingControllerParameters Parameters => parameters;
        internal int LastTargetCount { get; private set; }
        internal float LastPostureCorrection { get; private set; }
        internal bool LastObservationValid { get; private set; }

        public void RestoreParameters(PersonStandingControllerParameters source)
        {
            parameters.CopyFrom(source);
            Reset();
        }

        public void Reset()
        {
            rateStates.Clear();
            LastTargetCount = 0;
            LastPostureCorrection = 0f;
            LastObservationValid = false;
        }

        /// <summary>
        /// Evaluates a sampled person into native degree-per-second joint
        /// targets. <paramref name="walkRequest"/> and
        /// <paramref name="sideBiasRequest"/> are high-level person-adapter
        /// requests in -1..+1; they never replace posture feedback.
        /// </summary>
        public void Evaluate(
            PersonObservation observation,
            float walkRequest,
            float sideBiasRequest,
            float elapsedSeconds,
            PersonJointTargetBuffer targets)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            if (targets == null) throw new ArgumentNullException(nameof(targets));

            targets.Clear();
            LastTargetCount = 0;
            LastObservationValid = observation.Torso.Valid;
            if (!observation.Torso.Valid)
            {
                return;
            }

            var elapsed = PositiveFinite(elapsedSeconds) ? Mathf.Clamp(elapsedSeconds, .001f, .25f) : DefaultStepSeconds;
            var walk = SignedUnit(walkRequest);
            var sideBias = SignedUnit(sideBiasRequest);
            var posture = ResolvePostureCorrection(observation.Torso);
            LastPostureCorrection = posture;
            var evaluation = NextEvaluation();

            for (var index = 0; index < observation.LimbCount; index++)
            {
                var limb = observation[index];
                if (limb.Limb == null || !limb.Usable)
                {
                    continue;
                }

                // Unknown/custom limbs are intentionally left to the ordinary
                // adapter path; the standing assist must not invent anatomy.
                if (limb.Role == PersonLimbRole.Other)
                {
                    continue;
                }

                var requested = ResolveLimbSpeed(limb, posture, walk, sideBias);
                var target = RateLimit(limb.Limb, requested, elapsed, evaluation);
                targets.Add(limb.Limb, target, FiniteUnit(parameters.MotorInfluence));
            }

            LastTargetCount = targets.Count;

            PruneStaleRateStates(evaluation);
        }

        /// <summary>Convenience overload for the existing person projection.</summary>
        public void Evaluate(PersonObservation observation, PersonMotorCommand request, float elapsedSeconds, PersonJointTargetBuffer targets)
        {
            var sideBias = SignedUnit(request.RightLeg) - SignedUnit(request.LeftLeg);
            Evaluate(observation, request.Walk, sideBias, elapsedSeconds, targets);
        }

        private float ResolvePostureCorrection(PersonTorsoObservation torso)
        {
            var tilt = Finite(torso.TiltDegrees);
            var angularVelocity = Finite(torso.AngularVelocityDegreesPerSecond);
            var horizontalVelocity = Finite(torso.Velocity.x);
            var heightError = Finite(parameters.DesiredHeight) - Finite(torso.Height);
            var correction = -tilt * Finite(parameters.TiltProportional)
                - angularVelocity * Finite(parameters.TiltDerivative)
                - horizontalVelocity * Finite(parameters.HorizontalVelocityDerivative)
                + heightError * Finite(parameters.HeightProportional);
            return ClampSpeed(correction);
        }

        private float ResolveLimbSpeed(PersonLimbObservation limb, float posture, float walk, float sideBias)
        {
            var side = limb.Side == PersonLimbSide.Left ? -1f : limb.Side == PersonLimbSide.Right ? 1f : 0f;
            var speed = 0f;
            switch (limb.Role)
            {
                case PersonLimbRole.Leg:
                case PersonLimbRole.Foot:
                    var support = limb.SupportsBody && limb.HasContact ? 1f : FiniteUnit(parameters.UnsupportedLegScale);
                    speed = posture * support + walk * Finite(parameters.WalkContribution) + side * sideBias * Finite(parameters.SideBiasContribution);
                    break;
                case PersonLimbRole.Core:
                    speed = posture * Finite(parameters.CorePostureScale);
                    break;
                case PersonLimbRole.Head:
                    speed = posture * Finite(parameters.HeadPostureScale);
                    break;
                case PersonLimbRole.Arm:
                case PersonLimbRole.Hand:
                    speed = posture * Finite(parameters.ArmPostureScale) - side * sideBias * Finite(parameters.SideBiasContribution) * .3f;
                    break;
                default:
                    return 0f;
            }

            // Joint angle and speed feedback are local, so they remain
            // effective even when the fly emits a strong walk or turn request.
            speed -= Finite(limb.JointAngleDegrees) * Finite(parameters.JointAngleProportional);
            speed -= Finite(limb.JointSpeedDegreesPerSecond) * .04f;
            return ClampSpeed(speed);
        }

        private float RateLimit(LimbBehaviour limb, float requested, float elapsed, int evaluation)
        {
            for (var index = 0; index < rateStates.Count; index++)
            {
                if (rateStates[index].Limb != limb)
                {
                    continue;
                }

                var state = rateStates[index];
                state.PreviousSpeed = MoveTowards(state.PreviousSpeed, requested, MaximumAcceleration() * elapsed);
                state.LastSeenEvaluation = evaluation;
                rateStates[index] = state;
                return state.PreviousSpeed;
            }

            rateStates.Add(new RateState
            {
                Limb = limb,
                PreviousSpeed = requested,
                LastSeenEvaluation = evaluation
            });
            return requested;
        }

        private int evaluationCount;

        private int NextEvaluation()
        {
            if (evaluationCount == int.MaxValue)
            {
                evaluationCount = 1;
                for (var index = 0; index < rateStates.Count; index++)
                {
                    var state = rateStates[index];
                    state.LastSeenEvaluation = 0;
                    rateStates[index] = state;
                }
                return evaluationCount;
            }

            return ++evaluationCount;
        }

        private void PruneStaleRateStates(int evaluation)
        {
            // Retain one skipped update for transient hierarchy sampling, but
            // do not let removed limbs accumulate state for a whole session.
            for (var index = rateStates.Count - 1; index >= 0; index--)
            {
                if (rateStates[index].Limb == null || rateStates[index].LastSeenEvaluation + 1 < evaluation)
                {
                    rateStates.RemoveAt(index);
                }
            }
        }

        private float ClampSpeed(float value) => Mathf.Clamp(Finite(value), -MaximumSpeed(), MaximumSpeed());
        private float MaximumSpeed() => Mathf.Clamp(Finite(parameters.MaximumMotorSpeed), 0f, 120f);
        private float MaximumAcceleration() => Mathf.Clamp(Finite(parameters.MaximumMotorAcceleration), 0f, 480f);
        private static float MoveTowards(float prior, float target, float maximumChange)
        {
            var delta = target - prior;
            return Math.Abs(delta) <= maximumChange ? target : prior + Math.Sign(delta) * maximumChange;
        }
        private static bool PositiveFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        private static float FiniteUnit(float value) => Mathf.Clamp01(Finite(value));
        private static float SignedUnit(float value) => Mathf.Clamp(Finite(value), -1f, 1f);
    }
}
