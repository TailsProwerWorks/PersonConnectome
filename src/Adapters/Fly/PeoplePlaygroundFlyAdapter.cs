using System;
using System.Collections.Generic;
using ShadowNineX.PersonConnectome.Core;
using UnityEngine;

namespace ShadowNineX.PersonConnectome.Adapters
{
    /// <summary>
    /// Adapter for the mod's fly spawnable. The game does not expose a native fly
    /// organism API, so this adapter owns the small Unity rigidbody projection.
    /// </summary>
    internal sealed class PeoplePlaygroundFlyAdapter : IBodyAdapter
    {
        private const float MaxHorizontalAcceleration = 7f;
        private const float MaxVerticalAcceleration = 10f;
        private const float MaxSpeed = 12f;
        private const float FlightEngageThreshold = .08f;
        private readonly GameObject? root;
        private readonly Rigidbody2D? body;
        private readonly List<MonoBehaviour> behaviourBuffer = new();
        private IFlyBodyRig? rig;
        private SensoryFrame lastFrame;
        private bool hasReadFrame;
        private bool stopped;
        private readonly FlyEnvironmentSensor? environment;
        private readonly FlyHealth? health;
        private readonly FlyExperienceMemory? experienceMemory;
        private float flightSeconds;
        private float flightPowerHold;
        private float jumpCooldown;
        private bool jumpHeld;
        private bool backwardTurnHeld;
        private float targetYaw;
        private float targetForward;

        public PeoplePlaygroundFlyAdapter() { }

        public PeoplePlaygroundFlyAdapter(GameObject root)
        {
            this.root = root;
            body = root == null ? null : root.GetComponent<Rigidbody2D>();
            if (root != null && body != null) environment = new FlyEnvironmentSensor(root, body);
            health = root == null ? null : root.GetComponent<FlyHealth>();
            experienceMemory = root == null ? null : root.GetComponent<FlyExperienceMemory>();
            ResolveRig();
        }

        public bool IsUsable => root != null && body != null;
        public Transform? StatusAnchor => root?.transform;
        public bool HasSample => hasReadFrame;
        public SensoryFrame LastFrame => lastFrame;
        public bool IsTerminal => hasReadFrame && (!IsUsable || (lastFrame.HealthValid && !lastFrame.Alive));
        public bool IsBrainDead => hasReadFrame && lastFrame.BrainDead;
        public string Activity { get; private set; } = "Resting";
        public string LimbHealthSummary => health == null ? "regional health unavailable" : health.LimbHealthSummary;
        public string LearnedThreatSummary => experienceMemory == null ? "none learned" : experienceMemory.Summary;
        public string LastHarmfulObject => experienceMemory == null ? "none" : experienceMemory.LastHarmfulObject;
        public void ResetLearnedThreats() => experienceMemory?.Clear();

        public SensoryFrame Read()
        {
            if (!IsUsable || body == null)
            {
                return default;
            }

            ResolveRig();
            var velocity = body.velocity;
            var velocityValid = IsFinite(velocity.x) && IsFinite(velocity.y);
            var speed = velocityValid ? (float)Math.Sqrt(velocity.x * velocity.x + velocity.y * velocity.y) : 0f;
            var angularVelocityValid = IsFinite(body.angularVelocity);
            var signedTilt = NormalizedTilt(body.rotation, out var tiltValid);
            var ambientLight = RenderSettings.ambientLight.grayscale;
            var lightValid = IsFinite(ambientLight);
            lastFrame = new SensoryFrame
            {
                Alive = true,
                HealthValid = true,
                Health = 1f,
                VelocityValid = velocityValid,
                Velocity = Math.Min(1f, speed / MaxSpeed),
                VelocityX = velocityValid ? ClampSigned(velocity.x / MaxSpeed) : 0f,
                VelocityY = velocityValid ? ClampSigned(velocity.y / MaxSpeed) : 0f,
                Fall = velocityValid ? Math.Min(1f, Math.Max(0f, -velocity.y / 4f)) : 0f,
                AngularVelocityValid = angularVelocityValid,
                AngularVelocity = angularVelocityValid ? body.angularVelocity : 0f,
                Rotation = Math.Abs(signedTilt),
                TiltValid = tiltValid,
                SignedTilt = signedTilt,
                Light = lightValid ? Unit(ambientLight) : 0f,
                AmbientLight = lightValid ? Unit(ambientLight) : 0f,
                LightValid = lightValid,
                VisionLimited = true
            };
            environment?.Read(ref lastFrame);
            health?.Read(ref lastFrame);
            hasReadFrame = true;
            return lastFrame;
        }

        public void Apply(FlyMotorCommand command, bool chemistry, float jointSpeedDegreesPerSecond, float walkingRequestGain, float elapsedSeconds)
        {
            if (!IsUsable || body == null || stopped || body.bodyType != RigidbodyType2D.Dynamic ||
                (health != null && !health.IsAlive) ||
                (hasReadFrame && !lastFrame.Alive) || !IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
            {
                (rig as IFlyPhysicalBodyRig)?.Release();
                return;
            }

            ResolveRig();
            var dt = Math.Min(elapsedSeconds, .25f);
            var state = PrepareActuation(command, dt);
            var targetAssist = ResolveTargetAssist(state, dt);
            if (!TryGetMotionState(body, out var facing, out var length, out var velocity)) return;
            var previousVelocity = velocity;
            ApplyMovement(body, ref velocity, facing, length, state, targetAssist.Forward, dt);
            LimitSpeed(ref velocity);

            body.velocity = velocity;
            if (state.Flight.Flying && rig is IFlyPhysicalBodyRig flightRig)
                flightRig.ApplyFlightVelocityChange(velocity.x - previousVelocity.x, velocity.y - previousVelocity.y);
            var yaw = ClampSigned(ClampSigned(command.FlyYaw) + ClampSigned(command.FlyFlightYaw) + targetAssist.Yaw);
            if (!(rig is IFlyPhysicalBodyRig) || state.Flight.Flying)
                body.angularVelocity = MoveTowards(body.angularVelocity, state.Halted ? 0f : yaw * 120f, 360f * dt);
            Activity = DescribeActivity(command, state.Halted, state.Escape, state.Flight.Flying, state.Horizontal);
            if (rig != null && rig.IsUsable)
            {
                // Expose the actuator's held flight state to its visual program.
                var visualCommand = state.Halted ? default : command;
                if (state.BackwardAsForward)
                {
                    visualCommand.FlyForward = Math.Max(Unit(command.FlyForward), Unit(command.FlyBackward));
                    visualCommand.FlyBackward = 0f;
                }
                if (state.Flight.Flying)
                {
                    visualCommand.FlyFlightPower = Math.Max(state.Flight.HeldPower,
                        Math.Max(state.Flight.Power, state.Flight.Wing));
                }
                rig.Apply(visualCommand, dt);
            }
        }

        private TargetAssist ResolveTargetAssist(FlyActuationState state, float dt)
        {
            // The neural decoder remains authoritative for named motor routes,
            // but a connectome-only population fraction is not a stable pursuit
            // controller. Use the same head-relative visual bearing that feeds
            // the optic routes as a small, bounded action readout. Looming,
            // projectiles and explicit escape suppress approach immediately.
            var visibleTarget = !state.Halted && !state.Escape &&
                lastFrame.VisualFieldValid && lastFrame.VisionHeadBearingValid &&
                lastFrame.VisualGeometryValid && Unit(lastFrame.Vision) > .05f;
            var threat = Unit(lastFrame.VisualApproach) > .35f || Unit(lastFrame.Projectile) > .25f;
            if (visibleTarget && !threat)
            {
                var bearing = ClampSigned(lastFrame.VisionHeadBearingDegrees / 90f);
                var strength = Unit(lastFrame.Vision);
                targetYaw = MoveTowards(targetYaw, bearing * .65f, 2.5f * dt);
                targetForward = MoveTowards(targetForward, strength * .55f, 1.5f * dt);
            }
            else
            {
                targetYaw = MoveTowards(targetYaw, 0f, 4f * dt);
                targetForward = MoveTowards(targetForward, 0f, 2.5f * dt);
            }

            // Explicit reverse and halt requests always beat the assist. The
            // assist is intentionally strongest when the decoder is quiet.
            var forward = Unit(state.Horizontal) > .05f || Unit(state.Flight.Wing) > .05f ? 0f : targetForward;
            if (state.Halted || state.Horizontal < -.05f) forward = 0f;
            var yaw = state.Flight.Flying ? targetYaw : 0f;
            return new TargetAssist(forward, yaw);
        }

        private FlyActuationState PrepareActuation(FlyMotorCommand command, float dt)
        {
            jumpCooldown = Math.Max(0f, jumpCooldown - dt);
            flightSeconds = Math.Max(0f, flightSeconds - dt);
            var halted = Math.Max(Unit(command.FlyBrake), Unit(command.FlyHalt)) >= .2f;
            var escape = !halted && Unit(command.FlyEscape) > .5f;
            var jump = !halted && (escape || Unit(command.FlyJump) > .5f);
            var takeoff = Unit(command.FlyTakeoff);
            var landing = Unit(command.FlyLanding);
            var power = Unit(command.FlyFlightPower);
            var wing = Unit(command.FlyWingMotor);
            var flightIntent = Math.Max(takeoff, Math.Max(power, wing));
            // Wing-motor output is itself a fly-native decision to fly. Treat it
            // as lift intent instead of requiring the narrower takeoff/DNg02
            // channels to happen to cross a second threshold at the same time.
            if (flightIntent >= FlightEngageThreshold || escape)
            {
                flightSeconds = .75f;
                flightPowerHold = Math.Max(flightPowerHold, Math.Max(flightIntent, escape ? 1f : 0f));
            }
            else if (flightSeconds > 0f)
            {
                // Neural output is sampled at 20 Hz while physics runs faster.
                // Hold a bounded lift request between sparse output ticks, then
                // decay it smoothly instead of dropping to zero mid-stroke.
                flightPowerHold = MoveTowards(flightPowerHold, .25f, 1.5f * dt);
            }
            else
            {
                flightPowerHold = 0f;
            }
            if (rig is IFlyPhysicalBodyRig physicalRig && physicalRig.FlightCapacity <= .05f)
            {
                flightSeconds = 0f;
                flightPowerHold = 0f;
            }
            if (halted || (landing > .2f && landing >= flightIntent && !escape))
            {
                flightSeconds = 0f;
                flightPowerHold = 0f;
            }

            var forward = Unit(command.FlyForward);
            var backward = Unit(command.FlyBackward);
            var horizontal = ClampSigned(forward - backward);
            var backwardAsForward = false;
            if (backward <= .12f) backwardTurnHeld = false;
            if (!halted && rig is IFlyPhysicalBodyRig turningRig && backward >= .3f && backward > forward + .05f)
            {
                // MDN is a reverse request, but this articulated body is much
                // more stable turning once and walking normally than attempting
                // a mirrored tripod gait. Hysteresis prevents decoder noise from
                // flipping the whole specimen every physics tick.
                if (!backwardTurnHeld) backwardTurnHeld = turningRig.TryTurnAround();
                if (backwardTurnHeld)
                {
                    horizontal = backward;
                    backwardAsForward = true;
                }
            }
            // Retain the decoder's backward/forward intent. With no directional
            // intent, retreat from the frontal field that supplies looming cues.
            if (escape) horizontal = backwardAsForward || horizontal > 0f ? 1f : -1f;
            if (halted) horizontal = 0f;
            var heldPower = flightSeconds > 0f ? Math.Max(.25f, flightPowerHold) : 0f;
            var flight = new FlightControl(flightSeconds > 0f, takeoff, power, wing, heldPower);
            return new FlyActuationState(halted, escape, jump, flight, horizontal, backwardAsForward);
        }

        private bool TryGetMotionState(Rigidbody2D currentBody, out Vector3 facing, out float length, out Vector2 velocity)
        {
            // The specimen faces local -X. Mirroring and rotation must affect
            // thrust exactly as they affect its sensory field and visible head.
            var flyRoot = root;
            facing = default;
            length = 0f;
            velocity = default;
            if (flyRoot == null) return false;

            facing = flyRoot.transform.TransformVector(new Vector3(-1f, 0f, 0f));
            length = (float)Math.Sqrt(facing.x * facing.x + facing.y * facing.y);
            if (!IsFinite(length) || length < .001f) return false;

            velocity = currentBody.velocity;
            return IsFinite(velocity.x) && IsFinite(velocity.y) && IsFinite(currentBody.angularVelocity);
        }

        private void ApplyMovement(Rigidbody2D currentBody, ref Vector2 velocity, Vector3 facing, float length, FlyActuationState state, float targetAssistForward, float dt)
        {
            var horizontal = ClampSigned(state.Horizontal + targetAssistForward);
            var targetSpeed = horizontal * (state.Flight.Flying ? 5f : 2f);
            var headingX = facing.x / length;
            var headingY = facing.y / length;
            if ((horizontal != 0f || state.Halted) && (!(rig is IFlyPhysicalBodyRig) || state.Flight.Flying || targetAssistForward > .01f))
            {
                var alongHead = velocity.x * headingX + velocity.y * headingY;
                var change = MoveTowards(alongHead, targetSpeed, MaxHorizontalAcceleration * dt) - alongHead;
                velocity.x += headingX * change;
                velocity.y += headingY * change;
            }
            if (state.Flight.Flying)
            {
                // Hover compensation is an engineered flight actuator, not a
                // biological wing model. Gravity remains active when resting.
                var capacity = rig is IFlyPhysicalBodyRig flightRig ? flightRig.FlightCapacity : 1f;
                var sustainedPower = Math.Max(state.Flight.Power, state.Flight.HeldPower);
                var verticalTarget = (state.Escape ? 3f : state.Flight.Takeoff * 2f + sustainedPower * 1.35f + state.Flight.Wing * .9f) * capacity;
                velocity.y = MoveTowards(velocity.y, verticalTarget, MaxVerticalAcceleration * dt);
                velocity.y -= Physics2D.gravity.y * currentBody.gravityScale * dt;
            }
            var grounded = rig is IFlyPhysicalBodyRig groundRig ? groundRig.IsGrounded : lastFrame.PhysicalContact > 0f;
            if (state.Jump && !jumpHeld && jumpCooldown <= 0f && grounded)
            {
                velocity.y = Math.Max(velocity.y, 3f);
                jumpCooldown = .75f;
            }
            jumpHeld = state.Jump;
        }

        private static void LimitSpeed(ref Vector2 velocity)
        {
            var speed = (float)Math.Sqrt(velocity.x * velocity.x + velocity.y * velocity.y);
            if (speed <= MaxSpeed) return;

            var scale = MaxSpeed / speed;
            velocity.x *= scale;
            velocity.y *= scale;
        }

        public void RefreshWalkingRequest() { }
        public void Resume() { stopped = false; }
        public void Suspend() { stopped = true; flightPowerHold = 0f; backwardTurnHeld = false; targetYaw = targetForward = 0f; (rig as IFlyPhysicalBodyRig)?.Release(); }
        public void Stop()
        {
            stopped = true;
            (rig as IFlyPhysicalBodyRig)?.Release();
            flightSeconds = jumpCooldown = 0f;
            flightPowerHold = 0f;
            jumpHeld = false;
            backwardTurnHeld = false;
            targetYaw = targetForward = 0f;
            Activity = "Stopped";
        }
        public void Dispose() { flightPowerHold = 0f; backwardTurnHeld = false; targetYaw = targetForward = 0f; (rig as IFlyPhysicalBodyRig)?.Release(); }

        private void ResolveRig()
        {
            if (root == null || rig != null) return;
            root.GetComponentsInChildren(true, behaviourBuffer);
            foreach (var behaviour in behaviourBuffer)
            {
                if (behaviour is IFlyBodyRig candidate)
                {
                    rig = candidate;
                    break;
                }
            }
        }

        private readonly struct FlyActuationState
        {
            public FlyActuationState(bool halted, bool escape, bool jump, FlightControl flight, float horizontal,
                bool backwardAsForward)
            {
                Halted = halted;
                Escape = escape;
                Jump = jump;
                Flight = flight;
                Horizontal = horizontal;
                BackwardAsForward = backwardAsForward;
            }

            public bool Halted { get; }
            public bool Escape { get; }
            public bool Jump { get; }
            public FlightControl Flight { get; }
            public float Horizontal { get; }
            public bool BackwardAsForward { get; }
        }

        private readonly struct FlightControl
        {
            public FlightControl(bool flying, float takeoff, float power, float wing, float heldPower)
            {
                Flying = flying;
                Takeoff = takeoff;
                Power = power;
                Wing = wing;
                HeldPower = heldPower;
            }

            public bool Flying { get; }
            public float Takeoff { get; }
            public float Power { get; }
            public float Wing { get; }
            public float HeldPower { get; }
        }

        private readonly struct TargetAssist
        {
            public TargetAssist(float forward, float yaw)
            {
                Forward = Unit(forward);
                Yaw = ClampSigned(yaw);
            }

            public float Forward { get; }
            public float Yaw { get; }
        }

        private static float NormalizedTilt(float rotationDegrees, out bool valid)
        {
            valid = IsFinite(rotationDegrees);
            if (!valid) return 0f;
            var wrapped = rotationDegrees % 360f;
            if (wrapped > 180f) wrapped -= 360f;
            if (wrapped < -180f) wrapped += 360f;
            return ClampSigned(wrapped / 180f);
        }

        private static string DescribeActivity(FlyMotorCommand command, bool halted, bool escape, bool flying, float horizontal)
        {
            if (halted) return "Halting";
            if (escape) return "Escaping";
            if (flying) return "Flying";
            if (Unit(command.FlyFeed) > .1f) return "Feeding request";

            var locomotion = ClampSigned(horizontal + ClampSigned(command.FlyLegMotor));
            var grooming = FlyGait.GroomingIntent(command.FlyGroomAntenna, command.FlyGroomHead,
                command.FlyGroomLeg, command.FlyGroomAbdomen, locomotion, flying ? 1f : 0f, escape);
            if (grooming > 0f) return "Grooming";
            return Math.Abs(horizontal) > .03f ? "Walking" : "Resting";
        }

        private static float Unit(float value) => IsFinite(value) ? Math.Min(1f, Math.Max(0f, value)) : 0f;
        private static float MoveTowards(float value, float target, float step) => value + Math.Max(-step, Math.Min(step, target - value));
        private static float ClampSigned(float value) => IsFinite(value) ? Math.Min(1f, Math.Max(-1f, value)) : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
