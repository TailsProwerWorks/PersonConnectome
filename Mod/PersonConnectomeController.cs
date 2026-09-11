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

        private ConnectomeBrain brain;
        private PeoplePlaygroundPersonAdapter adapter;
        private PersonConnectomeStatusDisplay statusDisplay;
        private float accumulator;
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
            // Process at most one tick per physics callback. Dropping excess elapsed
            // time prevents a slow frame from turning into a catch-up spike.
            accumulator = Mathf.Min(accumulator + Time.fixedDeltaTime, interval);
            if (accumulator >= interval)
            {
                accumulator -= interval;
                var sensory = adapter.Read();
                adapter.Apply(brain.Step(sensory), true);
            }
        }

        private void LateUpdate()
        {
            statusDisplay?.Update(Time.deltaTime, brain, adapter);
        }

        private void OnDisable()
        {
            accumulator = 0f;
            brain?.Step(default(SensoryFrame));
            adapter?.Stop();
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

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsExternal(collision))
            {
                Report?.Invoke(collision.relativeVelocity.magnitude);
                ReportProjectileIfApplicable(collision);
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (IsExternal(collision) && Report != null)
            {
                Report(collision.relativeVelocity.magnitude * .25f);
            }
        }

        private bool IsExternal(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return false;
            }

            var other = collision.collider.transform;
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
