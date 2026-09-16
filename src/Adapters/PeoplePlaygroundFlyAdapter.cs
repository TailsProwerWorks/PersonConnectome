using System;
using System.Collections.Generic;
using Mod.Core;
using UnityEngine;

namespace Mod.Adapters
{
    /// <summary>
    /// Adapter for the mod's fly spawnable. The game does not expose a native fly
    /// organism API, so this adapter owns the small Unity rigidbody projection.
    /// </summary>
    internal sealed class PeoplePlaygroundFlyAdapter : IBodyAdapter
    {
        private const float MaxHorizontalAcceleration = 7f;
        private const float MaxVerticalAcceleration = 10f;
        private const float MaxAngularAcceleration = 180f;
        private const float MaxSpeed = 12f;
        private readonly GameObject? root;
        private readonly Rigidbody2D? body;
        private readonly List<MonoBehaviour> behaviourBuffer = [];
        private IFlyBodyRig? rig;
        private SensoryFrame lastFrame;
        private bool hasReadFrame;
        private bool stopped;

        public PeoplePlaygroundFlyAdapter() { }

        public PeoplePlaygroundFlyAdapter(GameObject root)
        {
            this.root = root;
            body = root == null ? null : root.GetComponent<Rigidbody2D>();
            ResolveRig();
        }

        public bool IsUsable => root != null && body != null;
        public Transform? StatusAnchor => root?.transform;
        public bool HasSample => hasReadFrame;
        public SensoryFrame LastFrame => lastFrame;
        public bool IsTerminal => hasReadFrame && root == null;
        public bool IsBrainDead => false;

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
                OxygenValid = true,
                Oxygen = 1f,
                ConsciousnessValid = true,
                Consciousness = 1f,
                VitalityValid = true,
                Vitality = 1f,
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
            hasReadFrame = true;
            return lastFrame;
        }

        public void Apply(FlyMotorCommand command, bool chemistry, float jointSpeedDegreesPerSecond, float walkingRequestGain, float elapsedSeconds)
        {
            if (!IsUsable || body == null || stopped)
            {
                return;
            }

            ResolveRig();
            var dt = IsFinite(elapsedSeconds) && elapsedSeconds > 0f ? Math.Min(elapsedSeconds, .25f) : .02f;
            var horizontal = ClampSigned(command.FlyForward - command.FlyBackward);
            var vertical = ClampSigned(command.FlyFlightPower + command.FlyTakeoff - command.FlyLanding);
            if (command.FlyJump > .5f) vertical = Math.Max(vertical, .8f);
            if (command.FlyEscape > .5f)
            {
                horizontal = Math.Sign(horizontal == 0f ? 1f : horizontal);
                vertical = Math.Max(vertical, .9f);
            }

            var velocity = body.velocity;
            velocity.x = velocity.x + horizontal * MaxHorizontalAcceleration * dt;
            velocity.y = velocity.y + vertical * MaxVerticalAcceleration * dt;
            var brake = Math.Max(Unit(command.FlyBrake), Unit(command.FlyHalt));
            if (brake >= .2f)
            {
                var damping = Math.Max(0f, 1f - Math.Min(1f, brake) * 8f * dt);
                velocity.x *= damping;
                velocity.y *= damping;
            }

            var speed = (float)Math.Sqrt(velocity.x * velocity.x + velocity.y * velocity.y);
            if (speed > MaxSpeed)
            {
                var scale = MaxSpeed / speed;
                velocity.x *= scale;
                velocity.y *= scale;
            }

            body.velocity = velocity;
            body.angularVelocity = body.angularVelocity +
                ClampSigned(command.FlyYaw + command.FlyFlightYaw) * MaxAngularAcceleration * dt;
            if (rig != null && rig.IsUsable)
            {
                rig.Apply(command, dt);
            }
        }

        public void RefreshWalkingRequest() { }
        public void Suspend() { stopped = true; }
        public void Stop()
        {
            stopped = true;
            if (body != null) body.velocity = new Vector2(0f, 0f);
        }
        public void Dispose() { }

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

        private static float NormalizedTilt(float rotationDegrees, out bool valid)
        {
            valid = IsFinite(rotationDegrees);
            if (!valid) return 0f;
            var wrapped = rotationDegrees % 360f;
            if (wrapped > 180f) wrapped -= 360f;
            if (wrapped < -180f) wrapped += 360f;
            return ClampSigned(wrapped / 180f);
        }

        private static float Unit(float value) => IsFinite(value) ? Math.Min(1f, Math.Max(0f, value)) : 0f;
        private static float ClampSigned(float value) => IsFinite(value) ? Math.Min(1f, Math.Max(-1f, value)) : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
