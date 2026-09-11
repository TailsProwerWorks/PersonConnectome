using System;
using UnityEngine;

namespace Mod
{
    public sealed class PersonConnectomeController : MonoBehaviour
    {
        // Configuration is intentionally exposed in the spawned object's inspector.
        [Range(1f, 60f)] public float TickRateHz = 20f;
        [Range(1f, 30f)] public float VisionRadius = 8f;
        public KeyCode DebugOverlayKey = KeyCode.F7;

        private ConnectomeBrain brain;
        private PeoplePlaygroundPersonAdapter adapter;
        private PersonConnectomeStatusDisplay statusDisplay;
        private float accumulator;

        private void Awake()
        {
            adapter = new PeoplePlaygroundPersonAdapter(gameObject, VisionRadius, RegisterCollision);
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

        private void Update()
        {
            if (Input.GetKeyDown(DebugOverlayKey))
            {
                var status = brain == null ? "MaleCNS v1.0 unavailable" : brain.Status;
                ModAPI.Notify("Person Connectome: " + status + " | " + adapter.CapabilitySummary);
            }
        }

        private void FixedUpdate()
        {
            if (adapter == null || brain == null)
            {
                return;
            }

            var rate = float.IsNaN(TickRateHz) || float.IsInfinity(TickRateHz) ? 20f : Mathf.Clamp(TickRateHz, 1f, 60f);
            accumulator = Mathf.Min(accumulator + Time.fixedDeltaTime, 4f / rate);
            var interval = 1f / rate;
            var steps = 0;
            while (accumulator >= interval && steps++ < 4)
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
    }

    // Collision callbacks arrive on limb GameObjects, so probes forward a bounded
    // signal to the root controller rather than assuming root collision messages.
    public sealed class PersonConnectomeLimbProbe : MonoBehaviour
    {
        public Transform OwnerRoot;
        public Action<float> Report;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsExternal(collision) && Report != null)
            {
                Report(collision.relativeVelocity.magnitude);
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
    }
}
