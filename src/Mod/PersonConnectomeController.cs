using System;
using System.Collections.Generic;
using UnityEngine;
using Mod.Adapters;
using Mod.Core;
using Mod.UI;

namespace Mod
{
    [DefaultExecutionOrder(-1000)]
    public sealed class PersonConnectomeController : MonoBehaviour
    {
        // Configuration is intentionally exposed in the spawned object's inspector.
        [Range(1f, 60f)] public float TickRateHz = 20f;
        [Range(1f, 30f)] public float VisionRadius = 8f;
        // Engineering conversion settings, not biological calibration.
        [Range(0f, 4f)] public float WalkingRequestGain = 2f;
        [Range(0f, 120f)] public float JointSpeedDegreesPerSecond = 30f;

        private LifBrain? brain;
        private PeoplePlaygroundPersonAdapter? adapter;
        private IBodySensor? sensor;
        private IBodyActuator? actuator;
        private PersonConnectomeStatusDisplay? statusDisplay;
        private readonly ManualInputState manualInput = new();
        private float accumulator;
        private float sampleElapsed;
        private bool acceptingEvents;
        private bool pendingPoseSweep;
        private readonly List<SuppressedContextMenuButton> suppressedContextMenuButtons = [];
        private readonly List<ContextMenuOptionComponent> contextMenuOptions = [];
        private static readonly List<PersonConnectomeController> activeControllers = [];

        private void Awake()
        {
            pendingPoseSweep = true;
            adapter = new PeoplePlaygroundPersonAdapter(gameObject, VisionRadius, RegisterCollision, RegisterProjectile);
            sensor = adapter;
            actuator = adapter;
            statusDisplay = new PersonConnectomeStatusDisplay(adapter.StatusAnchor, manualInput);
            brain = LifBrain.TryCreate(out var loadStatus);
            if (!adapter.IsUsable)
            {
                Debug.Log("Person Connectome: attachment disabled because PersonBehaviour was unavailable.");
            }
            else if (brain == null)
            {
                Debug.Log("Person Connectome: " + loadStatus + ". Active control is disabled; no fallback graph is substituted.");
            }
        }

        private void Start()
        {
            // The controller runs early for walking maintenance; defer the sweep
            // until LateUpdate so later native Start methods have created buttons.
            pendingPoseSweep = true;
        }

        private void OnEnable()
        {
            if (!activeControllers.Contains(this)) activeControllers.Add(this);
            RebalanceTickPhases();
            acceptingEvents = true;
            pendingPoseSweep = true;
            statusDisplay?.SetActive(true);
            SuppressNativePoseOptions();
        }

        private void FixedUpdate()
        {
            if (sensor == null || actuator == null || brain == null || adapter == null)
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
                var sensorStarted = Time.realtimeSinceStartup;
                adapter.UpdateVisionRadius(VisionRadius);
                var sensory = sensor.Read();
                var sensorMs = (Time.realtimeSinceStartup - sensorStarted) * 1000f;
                var brainStarted = Time.realtimeSinceStartup;
                var command = brain.Step(sensory, sampleElapsed, manualInput);
                var brainMs = (Time.realtimeSinceStartup - brainStarted) * 1000f;
                var actuatorStarted = Time.realtimeSinceStartup;
                actuator.Apply(command, true, JointSpeedDegreesPerSecond, WalkingRequestGain, sampleElapsed);
                var actuatorMs = (Time.realtimeSinceStartup - actuatorStarted) * 1000f;
                sampleElapsed = 0f;
                statusDisplay?.RecordTick(sensorMs, brainMs, actuatorMs, skipped, brain);
            }
        }

        private static void RebalanceTickPhases()
        {
            for (var i = activeControllers.Count - 1; i >= 0; i--)
            {
                if (activeControllers[i] == null) activeControllers.RemoveAt(i);
            }

            var count = activeControllers.Count;
            if (count == 0) return;

            // Redistribute phases whenever the population changes. Each
            // controller keeps its own interval; only its position within that
            // interval moves, so no population size is hardcoded here.
            for (var i = 0; i < count; i++)
            {
                var controller = activeControllers[i];
                var interval = controller.TickInterval();
                var phase = interval * i / count;
                controller.accumulator = Mathf.Min(Mathf.Max(0f, interval - .000001f), phase);
            }
        }

        private float TickInterval()
        {
            var rate = float.IsNaN(TickRateHz) || float.IsInfinity(TickRateHz) ? 20f : Mathf.Clamp(TickRateHz, 1f, 60f);
            return 1f / rate;
        }

        private void LateUpdate()
        {
            if (pendingPoseSweep)
            {
                pendingPoseSweep = false;
                SuppressNativePoseOptions();
            }
            (actuator ?? adapter as IBodyActuator)?.RefreshWalkingRequest();
            if (statusDisplay != null && brain != null && adapter != null)
            {
                statusDisplay.Update(Time.unscaledDeltaTime, brain, adapter);
            }
        }

        private void OnDisable()
        {
            acceptingEvents = false;
            accumulator = 0f;
            activeControllers.Remove(this);
            RebalanceTickPhases();
            sampleElapsed = 0f;
            statusDisplay?.SetActive(false);
            manualInput.Deactivate();
            brain?.Stop();
            (actuator ?? adapter as IBodyActuator)?.Suspend();
            RestoreNativePoseOptions();
        }

        private void OnDestroy()
        {
            acceptingEvents = false;
            activeControllers.Remove(this);
            manualInput.Deactivate();
            RestoreNativePoseOptions();
            sensor = null;
            actuator = null;
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
                description.IndexOf("animation override", StringComparison.OrdinalIgnoreCase) >= 0 &&
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

        private void RegisterCollision(float magnitude)
        {
            if (acceptingEvents) adapter?.RegisterCollision(magnitude);
        }

        private void RegisterProjectile(float magnitude)
        {
            if (acceptingEvents) adapter?.RegisterProjectile(magnitude);
        }
    }

}
