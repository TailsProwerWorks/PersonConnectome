using System;
using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;
using UnityEngine;

namespace ShadowNineX.PersonConnectome
{
    /// <summary>Drives native, breakable PPG limb joints with a two-link tripod gait.</summary>
    [DefaultExecutionOrder(1000)]
    internal sealed class FlyBodyRig : MonoBehaviour, IFlyPhysicalBodyRig
    {
        private LimbBehaviour[] uppers = Array.Empty<LimbBehaviour>();
        private LimbBehaviour[] lowers = Array.Empty<LimbBehaviour>();
        private LimbBehaviour[] wings = Array.Empty<LimbBehaviour>();
        private LimbBehaviour? thorax;
        private FlyHealth? health;
        private float phase;
        private float wingPhase;
        private float driveFilter;
        private float asymmetryFilter;
        private float flightFilter;
        private FlyMotorCommand pendingCommand;
        private bool released = true;

        public bool IsUsable => thorax != null && uppers.Length == 6 && lowers.Length == 6;
        public bool IsGrounded
        {
            get
            {
                foreach (var lower in lowers)
                    if (CanDrive(lower) && lower.IsOnFloor) return true;
                return false;
            }
        }

        public float FlightCapacity
        {
            get
            {
                if (wings.Length != 2 || !CanDrive(wings[0]) || !CanDrive(wings[1])) return 0f;
                return Math.Min(Strength(wings[0]), Strength(wings[1]));
            }
        }

        public void Initialize(LimbBehaviour body, LimbBehaviour[] upperLegs, LimbBehaviour[] lowerLegs, LimbBehaviour[] wingParts)
        {
            thorax = body;
            health = GetComponent<FlyHealth>();
            uppers = upperLegs;
            lowers = lowerLegs;
            wings = wingParts;
        }

        public void Apply(FlyMotorCommand command, float elapsedSeconds)
        {
            pendingCommand = command;
            released = false;
        }

        public bool TryTurnAround()
        {
            if (thorax == null || thorax.Person == null) return false;
            var root = thorax.Person.transform;
            var scale = root.localScale;
            if (float.IsNaN(scale.x) || float.IsInfinity(scale.x) || Math.Abs(scale.x) < .001f) return false;

            // Catalog Q/E facing uses this same root reflection. Flipping the
            // complete Person keeps every collider, hinge, sprite, sensory ray,
            // and local forward axis in agreement.
            scale.x = -scale.x;
            root.localScale = scale;
            return true;
        }

        public void ApplyFlightVelocityChange(float deltaX, float deltaY)
        {
            if (thorax == null || thorax.Person == null ||
                float.IsNaN(deltaX) || float.IsInfinity(deltaX) ||
                float.IsNaN(deltaY) || float.IsInfinity(deltaY)) return;

            // The adapter directly accelerates the thorax. Give every other
            // still-attached dynamic part the same bounded velocity delta so
            // sixteen gravity-affected bodies do not anchor that thorax to the
            // floor through their hinges. Relative limb velocity is preserved.
            foreach (var limb in thorax.Person.Limbs)
            {
                if (limb == null || limb == thorax || limb.IsDismembered ||
                    limb.Joint == null || limb.Joint.connectedBody == null ||
                    limb.PhysicalBehaviour == null || limb.PhysicalBehaviour.isDisintegrated ||
                    limb.PhysicalBehaviour.rigidbody == null ||
                    limb.PhysicalBehaviour.rigidbody.bodyType != RigidbodyType2D.Dynamic ||
                    (limb.CirculationBehaviour != null && limb.CirculationBehaviour.IsDisconnected)) continue;

                var partBody = limb.PhysicalBehaviour.rigidbody;
                var velocity = partBody.velocity;
                if (float.IsNaN(velocity.x) || float.IsInfinity(velocity.x) ||
                    float.IsNaN(velocity.y) || float.IsInfinity(velocity.y)) continue;
                partBody.velocity = new Vector2(velocity.x + deltaX, velocity.y + deltaY);
            }
        }

        private void FixedUpdate()
        {
            if (released) Release();
            else DrivePose(pendingCommand, Time.fixedDeltaTime);
        }

        private void DrivePose(FlyMotorCommand command, float elapsedSeconds)
        {
            if (!IsUsable || thorax == null) return;
            if (health == null || !health.IsAlive) { Release(); return; }
            var dt = Unit(elapsedSeconds);
            var requestedDrive = Signed(command.FlyLegMotor + Unit(command.FlyForward) - Unit(command.FlyBackward));
            var requestedAsymmetry = Signed(command.FlyLegMotorAsym);
            var requestedFlight = Unit(command.FlyFlightPower + command.FlyWingMotor + command.FlyTakeoff) * FlightCapacity;
            // Motor populations are latest-tick readouts, not a continuous
            // torque request. Rate limiting their body projection prevents a
            // one-tick spike from reversing every joint and looking like a
            // seizure in native physics.
            driveFilter = MoveTowards(driveFilter, requestedDrive, 5f * dt);
            asymmetryFilter = MoveTowards(asymmetryFilter, requestedAsymmetry, 6f * dt);
            flightFilter = MoveTowards(flightFilter, requestedFlight, 4f * dt);
            var drive = driveFilter;
            var flight = flightFilter;
            var airborne = !IsGrounded;
            var poweredFlight = flight > .08f;
            var grooming = FlyGait.GroomingIntent(command.FlyGroomAntenna, command.FlyGroomHead,
                command.FlyGroomLeg, command.FlyGroomAbdomen, drive, flight, Unit(command.FlyEscape) > .5f);
            if (!poweredFlight) phase = (phase + dt * Math.Abs(drive) * 2.5f) % 1f;
            var wingFrequency = 3.5f + flight * 3.5f;
            wingPhase = (wingPhase + dt * wingFrequency) % 1f;
            for (var i = 0; i < uppers.Length; i++)
            {
                if (airborne && !poweredFlight)
                {
                    // A falling or recently landed body is not a walking
                    // surface. Releasing the tripod servos prevents the legs
                    // from fighting gravity and producing the seizure-like
                    // folding seen when flight support ends.
                    Release(uppers[i]);
                    Release(lowers[i]);
                    continue;
                }

                var side = i % 2 == 0 ? -1f : 1f;
                var localDrive = poweredFlight ? 0f : Signed(drive + side * asymmetryFilter * .4f);
                FlyGait.Pose(i, poweredFlight ? 0f : phase, localDrive, poweredFlight ? 1f : flight, grooming, out var upper, out var lower);
                // FlyGait returns both segment headings in the thorax frame, but
                // the knee hinge is connected to the femur. Convert the lower
                // heading to a femur-relative angle before driving it.
                if (!CanDrive(uppers[i])) { Release(uppers[i]); Release(lowers[i]); continue; }
                // These torques carry the complete 17-body assembly at rest. The
                // former .22/.12 caps were below the observed native contact load,
                // so healthy legs folded even while the rest-pose IK was active.
                var legTorque = poweredFlight ? .35f : 1f;
                Drive(uppers[i], upper, FlyGait.UpperJointTorque * legTorque);
                Drive(lowers[i], lower - upper, FlyGait.LowerJointTorque * legTorque);
            }
            for (var i = 0; i < wings.Length; i++)
            {
                if (flight <= .02f)
                {
                    Release(wings[i]);
                    continue;
                }

                // Drive the hinge with a bounded angular velocity stroke. A
                // position-error motor cannot keep up with a flapping target;
                // it simply parks at a limit and looks like a dead wing.
                var phase = wingPhase * Math.PI * 2d + i * .08f;
                var frequency = wingFrequency * Math.PI * 2d;
                var amplitude = 6f + flight * 24f + Unit(command.FlySong) * 3f;
                DriveWingStroke(wings[i], (float)(Math.Cos(phase) * amplitude * frequency), FlyGait.WingJointTorque);
            }
            // Only grounded feet provide balance. Airborne legs cannot right the body.
            if (IsGrounded && flight < .1f && thorax.PhysicalBehaviour != null)
            {
                var body = thorax.PhysicalBehaviour.rigidbody;
                var correction = Mathf.DeltaAngle(body.rotation, 0f) * .006f - body.angularVelocity * .002f;
                body.AddTorque(Mathf.Clamp(correction, -.12f, .12f));
            }
        }

        private void Drive(LimbBehaviour limb, float degrees, float torque)
        {
            if (!CanDrive(limb) || thorax == null) { Release(limb); return; }
            var joint = limb.Joint;
            var connectedBody = joint.connectedBody;
            if (connectedBody == null) { Release(limb); return; }
            // Rigidbody2D.rotation excludes scale reflections. Hinge motor
            // targets are relative to the connected body, so use that body's
            // current world angle rather than always using the thorax.
            var targetAngle = connectedBody.rotation + degrees;
            var error = Mathf.DeltaAngle(limb.PhysicalBehaviour.rigidbody.rotation, targetAngle);
            var motor = joint.motor;
            motor.motorSpeed = Mathf.Clamp(error * 14f, -300f, 300f);
            motor.maxMotorTorque = torque * Strength(limb);
            limb.InfluenceMotorSpeed(motor.motorSpeed, 1f);
            joint.motor = motor;
            joint.useMotor = true;
        }

        private void DriveWingStroke(LimbBehaviour limb, float relativeSpeed, float torque)
        {
            if (!CanDrive(limb) || limb.Joint == null)
            {
                Release(limb);
                return;
            }

            var motor = limb.Joint.motor;
            motor.motorSpeed = Mathf.Clamp(relativeSpeed, -720f, 720f);
            motor.maxMotorTorque = torque * Strength(limb);
            limb.InfluenceMotorSpeed(motor.motorSpeed, 1f);
            limb.Joint.motor = motor;
            limb.Joint.useMotor = true;
        }

        public void Release()
        {
            released = true;
            pendingCommand = default;
            driveFilter = asymmetryFilter = flightFilter = 0f;
            foreach (var limb in uppers) Release(limb);
            foreach (var limb in lowers) Release(limb);
            foreach (var limb in wings) Release(limb);
        }

        private static void Release(LimbBehaviour? limb)
        {
            if (limb == null || limb.Joint == null) return;
            limb.InfluenceMotorSpeed(0f, 1f);
            limb.Joint.useMotor = false;
        }

        private static bool CanDrive(LimbBehaviour? limb) => limb != null && limb.Health > 0f &&
            (limb.Person == null || (!limb.Person.Braindead && limb.Person.Consciousness > .05f)) &&
            !limb.IsDismembered && !limb.Broken && !limb.Frozen && !limb.IsParalysed &&
            limb.Joint != null && limb.Joint.connectedBody != null &&
            limb.PhysicalBehaviour != null && !limb.PhysicalBehaviour.isDisintegrated &&
            (limb.CirculationBehaviour == null || !limb.CirculationBehaviour.IsDisconnected);

        private static float Strength(LimbBehaviour limb) => Unit(limb.Health / Math.Max(.001f, limb.InitialHealth));
        private static float MoveTowards(float value, float target, float step) =>
            value + Math.Max(-step, Math.Min(step, target - value));
        private static float Unit(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        private static float Signed(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, -1f, 1f);
        private void OnDisable() => Release();
    }
}
