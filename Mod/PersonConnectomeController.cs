using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public sealed class PersonConnectomeController : MonoBehaviour
    {
        // Configuration is intentionally exposed in the spawned object's inspector.
        [Range(1f, 60f)] public float TickRateHz = 20f;
        [Range(1f, 30f)] public float VisionRadius = 8f;
        // Engineering conversion settings, not biological calibration.
        [Range(0f, 4f)] public float WalkingRequestGain = 2f;
        [Range(0f, 120f)] public float JointSpeedDegreesPerSecond = 30f;

        private ConnectomeBrain brain;
        private PeoplePlaygroundPersonAdapter adapter;
        private PersonConnectomeStatusDisplay statusDisplay;
        private float accumulator;
        private float sampleElapsed;
        private readonly List<SuppressedContextMenuButton> suppressedContextMenuButtons = [];
        private readonly List<ContextMenuOptionComponent> contextMenuOptions = [];

        private void Awake()
        {
            adapter = new PeoplePlaygroundPersonAdapter(gameObject, VisionRadius, RegisterCollision, RegisterProjectile);
            statusDisplay = new PersonConnectomeStatusDisplay(adapter.StatusAnchor);
            brain = ConnectomeBrain.TryCreate(out var loadStatus);
            if (!adapter.IsUsable)
            {
                Debug.Log("Person Connectome: attachment disabled because PersonBehaviour was unavailable.");
            }
            else if (brain == null)
            {
                Debug.Log("Person Connectome: " + loadStatus + ". Active control is disabled; no fallback graph is substituted.");
            }
            SuppressNativePoseOptions();
        }

        private void Start()
        {
            // LimbBehaviour creates its native pose buttons during startup; repeat
            // once after all child Start methods have run so the menu is consistent.
            SuppressNativePoseOptions();
        }

        private void OnEnable()
        {
            statusDisplay?.SetActive(true);
            SuppressNativePoseOptions();
        }

        private void FixedUpdate()
        {
            if (adapter == null || brain == null)
            {
                return;
            }

            var rate = float.IsNaN(TickRateHz) || float.IsInfinity(TickRateHz) ? 20f : Mathf.Clamp(TickRateHz, 1f, 60f);
            var interval = 1f / rate;
            // Preserve fractional time; still process at most one tick per callback.
            accumulator += Time.fixedDeltaTime;
            sampleElapsed += Time.fixedDeltaTime;
            if (accumulator + .000001f >= interval)
            {
                var remaining = Mathf.Max(0f, accumulator - interval);
                var skipped = (float)Math.Floor((remaining + .000001f) / interval) * interval;
                accumulator = Mathf.Max(0f, remaining - skipped);
                var started = Time.realtimeSinceStartup;
                var sensory = adapter.Read();
                adapter.Apply(brain.Step(sensory, sampleElapsed), true, JointSpeedDegreesPerSecond, WalkingRequestGain, sampleElapsed);
                sampleElapsed = 0f;
                statusDisplay?.RecordTick((Time.realtimeSinceStartup - started) * 1000f, skipped, brain);
            }
        }

        private void LateUpdate()
        {
            statusDisplay?.Update(Time.unscaledDeltaTime, brain, adapter);
        }

        private void OnDisable()
        {
            accumulator = 0f;
            sampleElapsed = 0f;
            statusDisplay?.SetActive(false);
            brain?.Stop();
            adapter?.Suspend();
            RestoreNativePoseOptions();
        }

        private void OnDestroy()
        {
            RestoreNativePoseOptions();
            adapter?.Dispose();
            statusDisplay?.Dispose();
        }

        private void SuppressNativePoseOptions()
        {
            if (brain == null || adapter == null || !adapter.IsUsable || gameObject == null)
            {
                return;
            }

            gameObject.GetComponentsInChildren(true, contextMenuOptions);
            foreach (var optionComponent in contextMenuOptions)
            {
                if (optionComponent == null || optionComponent.Buttons == null)
                {
                    continue;
                }

                for (var i = optionComponent.Buttons.Count - 1; i >= 0; i--)
                {
                    var button = optionComponent.Buttons[i];
                    if (!IsNativePoseOption(button))
                    {
                        continue;
                    }

                    suppressedContextMenuButtons.Add(new SuppressedContextMenuButton(optionComponent, button));
                    optionComponent.Buttons.RemoveAt(i);
                }
            }
        }

        private void RestoreNativePoseOptions()
        {
            for (var i = suppressedContextMenuButtons.Count - 1; i >= 0; i--)
            {
                var suppressed = suppressedContextMenuButtons[i];
                if (suppressed.Component != null && suppressed.Component.Buttons != null && !suppressed.Component.Buttons.Contains(suppressed.Button))
                {
                    suppressed.Component.Buttons.Add(suppressed.Button);
                }
            }

            suppressedContextMenuButtons.Clear();
        }

        private static bool IsNativePoseOption(ContextMenuButton button)
        {
            return IsPoseIdentity(button.Identity) ||
                ContainsPoseDescription(button.Description);
        }

        private static bool IsPoseIdentity(string identity)
        {
            return string.Equals(identity, "startWalking", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identity, "startProtect", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identity, "startSit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identity, "startPetrified", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identity, "startStumbling", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsPoseDescription(string description)
        {
            return !string.IsNullOrEmpty(description) &&
                (description.IndexOf("animation override", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 description.IndexOf("stumbling", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 description.IndexOf("protection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 description.IndexOf("sitting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 description.IndexOf("resting", StringComparison.OrdinalIgnoreCase) >= 0) &&
                (description.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 description.IndexOf("stumbling", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 description.IndexOf("protection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 description.IndexOf("sitting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 description.IndexOf("resting", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private sealed class SuppressedContextMenuButton
        {
            public readonly ContextMenuOptionComponent Component;
            public readonly ContextMenuButton Button;

            public SuppressedContextMenuButton(ContextMenuOptionComponent component, ContextMenuButton button)
            {
                Component = component;
                Button = button;
            }
        }

        private void RegisterCollision(float magnitude) => adapter?.RegisterCollision(magnitude);
        private void RegisterProjectile(float magnitude) => adapter?.RegisterProjectile(magnitude);
    }

    // Collision callbacks arrive on limb GameObjects, so probes forward a bounded
    // signal to the root controller rather than assuming root collision messages.
    public sealed class PersonConnectomeLimbProbe : MonoBehaviour
    {
        public Transform OwnerRoot;
        public Action<float> Report;
        public Action<float> ReportProjectile;
        public Func<Transform, bool> IsOwned;
        public Func<bool> IsConnected;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsSourceConnected() && IsExternal(collision))
            {
                Report?.Invoke(collision.relativeVelocity.magnitude);
                ReportProjectileIfApplicable(collision);
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (IsSourceConnected() && IsExternal(collision) && Report != null)
            {
                Report(collision.relativeVelocity.magnitude * .25f);
            }
        }

        private bool IsSourceConnected()
        {
            return IsConnected == null || IsConnected();
        }

        private bool IsExternal(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return false;
            }

            var other = collision.collider.transform;
            if (IsOwned != null) return !IsOwned(other);
            return OwnerRoot == null || (other != OwnerRoot && !other.IsChildOf(OwnerRoot));
        }

        private void ReportProjectileIfApplicable(Collision2D collision)
        {
            if (ReportProjectile == null || collision == null || collision.collider == null)
            {
                return;
            }

            if (PersonConnectomeProjectileDetection.IsProjectile(collision.collider))
            {
                ReportProjectile(collision.relativeVelocity.magnitude);
            }
        }
    }

    // The installed game still exposes this legacy component on compatible projectiles.
#pragma warning disable CS0612
    internal static class PersonConnectomeProjectileDetection
    {
        public static bool IsProjectile(Collider2D collider)
        {
            return collider != null &&
                (collider.GetComponentInParent<ProjectileBehaviour>() != null ||
                 collider.GetComponentInParent<GorseProjectileBehaviour>() != null ||
                 collider.GetComponentInParent<GenericScifiProjectileBehaviour>() != null ||
                 collider.GetComponentInParent<MachineGunProjectileBehaviour>() != null ||
                 collider.GetComponentInParent<LaunchedRocketBehaviour>() != null);
        }

        public static bool IsMovingProjectile(Collider2D collider, PhysicalBehaviour physical)
        {
            if (!IsProjectile(collider) || physical == null || physical.rigidbody == null)
            {
                return false;
            }

            var velocity = physical.rigidbody.velocity;
            return IsFinite(velocity.x) && IsFinite(velocity.y) && velocity.magnitude > 1f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
#pragma warning restore CS0612
}
