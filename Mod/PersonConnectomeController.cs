using System;
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
        }

        private void OnDestroy()
        {
            adapter?.Dispose();
            statusDisplay?.Dispose();
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

            var physical = collision.collider.GetComponentInParent<PhysicalBehaviour>();
            if (physical != null && physical.BulletPenetration)
            {
                ReportProjectile(collision.relativeVelocity.magnitude);
            }
        }
    }
}
