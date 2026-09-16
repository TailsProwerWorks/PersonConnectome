using System;
using Mod.Adapters;
using Mod.Core;
using Mod.UI;
using UnityEngine;

namespace Mod
{
    [DefaultExecutionOrder(-1000)]
    public sealed class FlyConnectomeController : MonoBehaviour
    {
        [Range(1f, 60f)] public float TickRateHz = 20f;

        private PeoplePlaygroundFlyAdapter? adapter;
        private LifBrain? brain;
        private PersonConnectomeStatusDisplay? statusDisplay;
        private readonly ManualInputState manualInput = new();
        private float accumulator;
        private float sampleElapsed;

        private void Awake()
        {
            adapter = new PeoplePlaygroundFlyAdapter(gameObject);
            brain = LifBrain.TryCreate(out var loadStatus);
            if (brain == null)
            {
                Debug.Log("Person Connectome: fly brain unavailable: " + loadStatus);
            }
            var statusAnchor = adapter.StatusAnchor;
            if (adapter.IsUsable && statusAnchor != null)
            {
                statusDisplay = ConnectomeStatusHost.RegisterFly(statusAnchor, manualInput);
            }
        }

        private void OnEnable()
        {
            statusDisplay?.SetActive(true);
        }

        private void FixedUpdate()
        {
            if (adapter == null || brain == null || !adapter.IsUsable) return;
            var rate = float.IsNaN(TickRateHz) || float.IsInfinity(TickRateHz) ? 20f : Mathf.Clamp(TickRateHz, 1f, 60f);
            var interval = 1f / rate;
            accumulator += Time.fixedDeltaTime;
            sampleElapsed += Time.fixedDeltaTime;
            if (accumulator < interval) return;

            accumulator -= interval;
            var sensorStarted = Time.realtimeSinceStartup;
            var frame = adapter.Read();
            var sensorMs = (Time.realtimeSinceStartup - sensorStarted) * 1000f;
            var brainStarted = Time.realtimeSinceStartup;
            var command = brain.Step(frame, sampleElapsed, manualInput);
            var brainMs = (Time.realtimeSinceStartup - brainStarted) * 1000f;
            var actuatorStarted = Time.realtimeSinceStartup;
            adapter.Apply(command, false, 0f, 0f, sampleElapsed);
            var actuatorMs = (Time.realtimeSinceStartup - actuatorStarted) * 1000f;
            statusDisplay?.RecordTick(sensorMs, brainMs, actuatorMs, 0f, brain);
            sampleElapsed = 0f;
        }

        private void LateUpdate()
        {
            if (statusDisplay != null && brain != null && adapter != null)
            {
                statusDisplay.Update(Time.unscaledDeltaTime, brain, adapter);
            }
        }

        private void OnDisable()
        {
            adapter?.Stop();
            brain?.Stop();
            manualInput.Deactivate();
            statusDisplay?.SetActive(false);
        }

        private void OnDestroy()
        {
            adapter?.Dispose();
            ConnectomeStatusHost.Unregister(statusDisplay);
            adapter = null;
            brain = null;
        }
    }
}
