using System;
using Mod.Core;
using UnityEngine;

namespace Mod.Adapters
{
    /// <summary>
    /// Person-specific request consumed by the People Playground limb and
    /// physiology implementation. It is kept in the adapter layer because its
    /// fields describe a substituted human body, not fly behaviour.
    /// </summary>
    internal struct PersonMotorCommand
    {
        public float Walk, LeftArm, RightArm, LeftLeg, RightLeg, Core, Head;
        public float ReachGrab, LeftGrip, RightGrip, Avoid;
        public float Freeze, Heal, Stimulate, Calm, Extinguish;
        // A fly halt/brake owns joint-target clearing, but not the person's
        // grip or chemistry policy. Keeping this separate from Freeze avoids
        // turning a partial neural halt into sedation or object release.
        public bool MotionStopRequested;
    }

    /// <summary>
    /// Translates shared neural output into the controls exposed by a People
    /// Playground person. Keeping this here avoids leaking person-joint policy
    /// into lifecycle wiring or other body adapters.
    /// </summary>
    internal sealed class PeoplePlaygroundPersonMotorMapper
    {
        private const float DefaultStepSeconds = .05f;
        private const float MotorChangePerSecond = 8f;
        // FlyForward, FlyBackward, and FlyYaw are decoder-level requests. The
        // LIF brain already applies their neural EMA, so this mapper must not
        // smooth them a second time or treat them as raw population activity.
        // Backward has a higher entry threshold because MDN is sparse, while
        // its release threshold matches the forward request hysteresis.
        private const float BackwardEntryThreshold = .2f;
        private const float LocomotionReleaseThreshold = .04f;
        private float forwardFilter;
        private float backwardFilter;
        private float yawFilter;
        private float leftLegFilter;
        private float rightLegFilter;
        private float locomotionDwell;
        private float escapeWalkSeconds;
        private int escapeWalkDirection;
        private int locomotionMode;
        private PersonMotorCommand lastCommand;

        // Kept for small pure mapper tests. Runtime code owns one mapper per
        // person so filtering, locomotion hysteresis, and escape bursts are
        // never shared across spawned bodies.
        public static PersonMotorCommand Map(FlyMotorCommand command)
        {
            return new PeoplePlaygroundPersonMotorMapper().Map(command, new SensoryFrame
            {
                HealthValid = true,
                Alive = true,
                ConsciousnessValid = true,
                Consciousness = 1f
            }, DefaultStepSeconds);
        }

        public PersonMotorCommand Map(FlyMotorCommand command, SensoryFrame frame, float elapsedSeconds)
        {
            var forward = Unit(command.FlyForward);
            var backward = Unit(command.FlyBackward);
            var halt = Mathf.Max(Unit(command.FlyHalt), Unit(command.FlyBrake));
            var wing = Unit(command.FlyWingMotor);
            var asymmetry = Signed(command.FlyLegMotorAsym);
            var jump = Unit(command.FlyJump);
            var takeoff = Unit(command.FlyTakeoff);
            var landing = Unit(command.FlyLanding);
            var flightPower = Unit(command.FlyFlightPower);
            var flightYaw = Signed(command.FlyFlightYaw);

            var bodyThreat = Unit(frame.Pain) + Unit(frame.Fire) + Unit(frame.Shock) + Unit(frame.SubmergedHypoxia) + Unit(frame.Projectile);
            var freeze = Mathf.Clamp01(Unit(frame.Unconscious) + Unit(frame.LiquidSedation));
            var smoothingElapsed = SmoothingElapsedSeconds(elapsedSeconds);
            var timerElapsed = TimerElapsedSeconds(elapsedSeconds);
            var stopRequested = halt >= .2f;
            var movementPermitted = IsMovementPermitted(frame, freeze);

            if (!movementPermitted || stopRequested)
            {
                ResetMotion();
                // A neural halt owns only the requested joint targets.  Do not
                // encode it as physical freeze: Freeze routes through the
                // person safety stop, which also releases grips and restores
                // chemistry.  Actual unconsciousness/sedation remains in the
                // sampled body state and therefore still takes that path.
                lastCommand = BodyState(frame, bodyThreat, freeze);
                lastCommand.MotionStopRequested = stopRequested;
                return lastCommand;
            }

            escapeWalkSeconds = Mathf.Max(0f, escapeWalkSeconds - timerElapsed);
            if (Unit(command.FlyEscape) > 0f)
            {
                escapeWalkSeconds = .6f;
                escapeWalkDirection = IsBackwardEntry(backward) || locomotionMode < 0 ? -1 : 1;
            }

            // These are named filters for telemetry/backward-compatible
            // locomotion state, but their values are owned by the brain's
            // decoder contract rather than a second adapter EMA.
            forwardFilter = forward;
            backwardFilter = backward;
            yawFilter = Signed(command.FlyYaw);
            var leftLeg = Unit(command.FlyLegMotor - asymmetry * .5f);
            var rightLeg = Unit(command.FlyLegMotor + asymmetry * .5f);
            leftLegFilter = Ema(leftLegFilter, leftLeg, smoothingElapsed, .15f);
            rightLegFilter = Ema(rightLegFilter, rightLeg, smoothingElapsed, .15f);
            UpdateLocomotion(backward, timerElapsed);

            var walk = ResolveWalk();
            var center = (leftLegFilter + rightLegFilter) * .5f;
            var sideBias = rightLegFilter - leftLegFilter;
            var armSwing = Signed((leftLegFilter - rightLegFilter) * .75f + walk * .35f + center * .15f);
            var requested = BodyState(frame, bodyThreat, freeze);
            requested.Walk = walk;
            requested.LeftArm = Signed(-armSwing - wing * .8f - flightYaw * .2f);
            requested.RightArm = Signed(armSwing + wing * .8f + flightYaw * .2f);
            requested.LeftLeg = Signed(walk - sideBias * .2f + jump * .5f + takeoff * .25f - landing * .25f);
            requested.RightLeg = Signed(walk + sideBias * .2f + jump * .5f + takeoff * .25f - landing * .25f);
            requested.Core = Signed(walk * .6f + center * .15f + yawFilter * .25f + flightPower * .35f + jump * .25f + takeoff * .2f - landing * .2f);
            requested.Head = Signed(sideBias * .5f + yawFilter + flightYaw * .4f);
            requested.Avoid = bodyThreat > .5f || Unit(command.FlyEscape) > 0f ? 1f : 0f;
            lastCommand = RateLimit(lastCommand, requested, smoothingElapsed);
            return lastCommand;
        }

        public void Reset()
        {
            ResetMotion();
            lastCommand = default;
        }

        private static PersonMotorCommand BodyState(SensoryFrame frame, float bodyThreat, float freeze)
        {
            return new PersonMotorCommand
            {
                Avoid = bodyThreat > .5f ? 1f : 0f,
                ReachGrab = 0f,
                LeftGrip = 0f,
                RightGrip = 0f,
                Freeze = freeze,
                Heal = Mathf.Clamp01(Unit(frame.Damage) + Unit(frame.Bleeding) + Unit(frame.LiquidHealing)),
                Stimulate = Unit(frame.LiquidStimulation),
                Calm = Unit(frame.LiquidSedation),
                Extinguish = Unit(frame.Fire)
            };
        }

        private void UpdateLocomotion(float backwardRequest, float elapsed)
        {
            locomotionDwell += elapsed;
            if (IsBackwardEntry(backwardRequest))
            {
                if (locomotionMode != -1)
                {
                    locomotionMode = -1;
                    locomotionDwell = 0f;
                }
            }
            else if (locomotionMode == 0 && forwardFilter >= .08f)
            {
                locomotionMode = 1;
                locomotionDwell = 0f;
            }
            else if (locomotionDwell >= .25f && ShouldStopLocomotion())
            {
                locomotionMode = 0;
                locomotionDwell = 0f;
            }
        }

        private bool ShouldStopLocomotion() => locomotionMode > 0 ? forwardFilter < LocomotionReleaseThreshold : backwardFilter < LocomotionReleaseThreshold;

        private float ResolveWalk()
        {
            if (escapeWalkSeconds > 0f) return escapeWalkDirection * .7f;
            if (locomotionMode > 0) return Mathf.Max(.3f, forwardFilter);
            return locomotionMode < 0 ? -Mathf.Max(.3f, backwardFilter) : 0f;
        }

        private static PersonMotorCommand RateLimit(PersonMotorCommand prior, PersonMotorCommand requested, float elapsed)
        {
            var maximumChange = MotorChangePerSecond * Mathf.Clamp(elapsed, 0f, .25f);
            requested.Walk = MoveTowards(prior.Walk, requested.Walk, maximumChange);
            requested.LeftArm = MoveTowards(prior.LeftArm, requested.LeftArm, maximumChange);
            requested.RightArm = MoveTowards(prior.RightArm, requested.RightArm, maximumChange);
            requested.LeftLeg = MoveTowards(prior.LeftLeg, requested.LeftLeg, maximumChange);
            requested.RightLeg = MoveTowards(prior.RightLeg, requested.RightLeg, maximumChange);
            requested.Core = MoveTowards(prior.Core, requested.Core, maximumChange);
            requested.Head = MoveTowards(prior.Head, requested.Head, maximumChange);
            return requested;
        }

        private void ResetMotion()
        {
            forwardFilter = backwardFilter = yawFilter = leftLegFilter = rightLegFilter = locomotionDwell = 0f;
            escapeWalkSeconds = 0f;
            escapeWalkDirection = 0;
            locomotionMode = 0;
        }

        private static bool IsMovementPermitted(SensoryFrame frame, float freeze)
        {
            // A completely empty frame is only used by the mapper's narrow
            // Unity-free tests. The production adapter always supplies its
            // latest sample and separately refuses to actuate before one
            // exists. Once any lifecycle data is present, require a complete
            // living, non-brain-dead and conscious sample.
            if (!HasLifecycleSample(frame)) return true;
            return frame.HealthValid && frame.Alive && !frame.BrainDead && frame.ConsciousnessValid &&
                Unit(frame.Consciousness) > .8f && freeze < .5f;
        }

        private static bool IsBackwardEntry(float request) => request >= BackwardEntryThreshold;
        private static bool HasLifecycleSample(SensoryFrame frame) =>
            frame.HealthValid || frame.Alive || frame.BrainDead || frame.ConsciousnessValid;
        private static float TimerElapsedSeconds(float value) => IsFinite(value) && value > 0f ? value : DefaultStepSeconds;
        private static float SmoothingElapsedSeconds(float value) => Mathf.Clamp(TimerElapsedSeconds(value), 0f, .25f);
        private static float Ema(float prior, float target, float elapsed, float tau) =>
            prior + (target - prior) * (1f - (float)Math.Exp(-elapsed / tau));
        private static float MoveTowards(float prior, float target, float maximumChange)
        {
            var delta = target - prior;
            if (Math.Abs(delta) <= maximumChange)
            {
                return target;
            }

            return prior + Math.Sign(delta) * maximumChange;
        }

        private static float Unit(float value) => !IsFinite(value) ? 0f : Mathf.Clamp01(value);
        private static float Signed(float value) => !IsFinite(value) ? 0f : Mathf.Clamp(value, -1f, 1f);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
