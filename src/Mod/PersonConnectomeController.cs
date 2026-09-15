using System;
using System.Collections.Generic;
using System.Linq;
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
        // Experimental adapter-local posture feedback; disabled until native
        // standing validation is complete.
        public bool JointAwareStandingControllerEnabled;

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
        private bool directFlyControl = true;
        private PersonBehaviour? person;
        private readonly List<SuppressedContextMenuButton> suppressedContextMenuButtons = [];
        private readonly List<ContextMenuOptionComponent> contextMenuOptions = [];
        private readonly List<LimbBehaviour> directControlLimbBuffer = [];
        private readonly List<LimbBehaviour> directControlLimbs = [];
        private readonly List<NativeAssistSnapshot> directControlSnapshotValues = [];
        private readonly Dictionary<LimbBehaviour, NativeAssistSnapshot> directControlSnapshots = [];
        private readonly List<NativePoseSnapshot> directControlPoseSnapshotValues = [];
        private readonly Dictionary<RagdollPose, NativePoseSnapshot> directControlPoseSnapshots = [];
        private const float DirectControlTopologyCheckSeconds = .25f;
        // The fingerprint catches the common root-level changes cheaply.  It
        // cannot see components added below an existing child, so periodically
        // rescan the hierarchy as a bounded fallback for modified bodies.
        private const float DirectControlHierarchyDiscoverySeconds = 2f;
        private bool directControlTopologyKnown;
        private int directControlTopologyFingerprint;
        private float nextDirectControlTopologyCheck;
        private float nextDirectControlHierarchyDiscovery;
        private float pendingDirectControlMs;
        private static readonly List<PersonConnectomeController> activeControllers = [];
        private static PersonConnectomeController? activeTrainingController;
        private const string TrainingProfileKey = "PersonConnectome.Training.Profile";

        public bool DirectFlyControlEnabled => directFlyControl;

        private void Awake()
        {
            pendingPoseSweep = true;
            person = gameObject.GetComponent<PersonBehaviour>();
            adapter = new PeoplePlaygroundPersonAdapter(gameObject, VisionRadius, RegisterCollision, RegisterProjectile);
            sensor = adapter;
            actuator = adapter;
            statusDisplay = new PersonConnectomeStatusDisplay(adapter.StatusAnchor, manualInput,
                new StatusDisplayBindings
                {
                    IsDirectFlyControlEnabled = () => directFlyControl,
                    SetDirectFlyControl = SetDirectFlyControl,
                    TrainingStatus = () => adapter == null ? "TEACH: unavailable" : adapter.TrainingSession.StatusText + "\n" + adapter.StandingControlSummary,
                    StartTraining = StartTrainingTrial,
                    ToggleTrainingPause = ToggleTrainingPause,
                    GivePositiveTrainingFeedback = GivePositiveTrainingFeedback,
                    GiveNegativeTrainingFeedback = GiveNegativeTrainingFeedback,
                    UndoTrainingFeedback = UndoTrainingFeedback,
                    RestoreBestTrainingVersion = RestoreBestTrainingVersion,
                    ResetTrainingSkill = ResetTrainingSkill,
                    SaveTrainingProfile = SaveTrainingProfile,
                    LoadTrainingProfile = LoadTrainingProfile
                });
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
            adapter?.SetJointAwareStandingControllerEnabled(JointAwareStandingControllerEnabled);
            ApplyDirectFlyControl();
            SuppressNativePoseOptions();
        }

        private void FixedUpdate()
        {
            ApplyDirectFlyControlWithTiming();
            if (sensor == null || actuator == null || brain == null || adapter == null)
            {
                return;
            }

            // Mirror inspector changes without recreating the adapter. The
            // standing controller remains opt-in and resets when disabled.
            adapter.SetJointAwareStandingControllerEnabled(JointAwareStandingControllerEnabled);

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
                actuator.Apply(command, true,
                    JointSpeedDegreesPerSecond, WalkingRequestGain, sampleElapsed);
                var actuatorMs = (Time.realtimeSinceStartup - actuatorStarted) * 1000f + pendingDirectControlMs;
                pendingDirectControlMs = 0f;
                sampleElapsed = 0f;
                statusDisplay?.RecordTick(sensorMs, brainMs, actuatorMs, skipped, brain);
            }

            adapter.RefreshStandingControl(Time.fixedDeltaTime);
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
            ApplyDirectFlyControlWithTiming();
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
            pendingDirectControlMs = 0f;
            statusDisplay?.SetActive(false);
            manualInput.Deactivate();
            brain?.Stop();
            (actuator ?? adapter as IBodyActuator)?.Suspend();
            adapter?.SetJointAwareStandingControllerEnabled(false);
            ClearActiveTrainingController(this);
            RestoreDirectFlyControl();
            RestoreNativePoseOptions();
        }

        private void OnDestroy()
        {
            acceptingEvents = false;
            activeControllers.Remove(this);
            ClearActiveTrainingController(this);
            manualInput.Deactivate();
            RestoreDirectFlyControl();
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

        private void SetDirectFlyControl(bool enabled)
        {
            if (directFlyControl == enabled)
            {
                if (enabled) ApplyDirectFlyControl();
                return;
            }

            directFlyControl = enabled;
            if (enabled) ApplyDirectFlyControl();
            else RestoreDirectFlyControl();
        }

        private void StartTrainingTrial()
        {
            if (adapter == null || !adapter.IsUsable) return;
            if (!TryClaimTrainingController(this)) return;
            JointAwareStandingControllerEnabled = true;
            adapter.StartTrainingTrial();
        }

        private static bool TryClaimTrainingController(PersonConnectomeController controller)
        {
            if (activeTrainingController != null && !ReferenceEquals(activeTrainingController, controller)) return false;
            activeTrainingController = controller;
            return true;
        }

        private static void ClearActiveTrainingController(PersonConnectomeController controller)
        {
            if (ReferenceEquals(activeTrainingController, controller)) activeTrainingController = null;
        }

        private void ToggleTrainingPause() => adapter?.ToggleTrainingPause();
        private void GivePositiveTrainingFeedback() => adapter?.GiveTrainingFeedback(true);
        private void GiveNegativeTrainingFeedback() => adapter?.GiveTrainingFeedback(false);
        private void UndoTrainingFeedback() => adapter?.UndoTrainingFeedback();
        private void RestoreBestTrainingVersion() => adapter?.RestoreBestTrainingVersion();
        private void ResetTrainingSkill() => adapter?.ResetTrainingSkill();

        private void SaveTrainingProfile()
        {
            if (adapter == null) return;
            PlayerPrefs.SetString(TrainingProfileKey, adapter.TrainingSession.Serialize());
            PlayerPrefs.Save();
        }

        private void LoadTrainingProfile()
        {
            if (adapter == null || !PlayerPrefs.HasKey(TrainingProfileKey)) return;
            adapter.TrainingSession.TryLoad(PlayerPrefs.GetString(TrainingProfileKey, String.Empty));
        }

        private void ApplyDirectFlyControl()
        {
            if (!CanApplyDirectFlyControl())
            {
                RestoreDirectFlyControlState();
                return;
            }

            RefreshDirectControlLimbCacheIfNeeded();
            foreach (var limb in directControlLimbs)
            {
                if (limb == null)
                {
                    continue;
                }

                if (!directControlSnapshots.ContainsKey(limb))
                {
                    directControlSnapshots.Add(limb, new NativeAssistSnapshot(limb));
                }

                // Keep the connectome responsible for the motor command while
                // retaining native joints, gravity, collisions and pose state.
                limb.FakeUprightForce = 0f;
                limb.BalanceMuscleMovement = 0f;
                limb.DoBalanceJerk = false;
                limb.DoStumble = false;
            }

            // A dismembered limb can leave the hierarchy without being destroyed.
            // Restore and forget it so the snapshot cache does not grow for the
            // person's entire lifetime.
            directControlSnapshotValues.Clear();
            foreach (var snapshot in directControlSnapshots.Values)
            {
                directControlSnapshotValues.Add(snapshot);
            }

            foreach (var snapshot in directControlSnapshotValues)
            {
                if (snapshot.IsAttached(transform))
                {
                    continue;
                }

                snapshot.Restore();
                directControlSnapshots.Remove(snapshot.Limb);
            }

            ApplyDirectPoseControl();
        }

        private void ApplyDirectFlyControlWithTiming()
        {
            var started = Time.realtimeSinceStartup;
            ApplyDirectFlyControl();
            pendingDirectControlMs += (Time.realtimeSinceStartup - started) * 1000f;
        }

        private bool CanApplyDirectFlyControl()
        {
            return directFlyControl && gameObject != null && brain != null &&
                adapter != null && adapter.IsUsable && person != null;
        }

        private void RefreshDirectControlLimbCacheIfNeeded()
        {
            if (Time.time < nextDirectControlTopologyCheck && directControlTopologyKnown)
            {
                return;
            }

            nextDirectControlTopologyCheck = Time.time + DirectControlTopologyCheckSeconds;
            var fingerprint = GetDirectControlTopologyFingerprint();
            var hierarchyDiscoveryDue = Time.time >= nextDirectControlHierarchyDiscovery;
            if (directControlTopologyKnown && fingerprint == directControlTopologyFingerprint && !hierarchyDiscoveryDue)
            {
                return;
            }

            directControlTopologyKnown = true;
            directControlTopologyFingerprint = fingerprint;
            nextDirectControlHierarchyDiscovery = Time.time + DirectControlHierarchyDiscoverySeconds;
            gameObject.GetComponentsInChildren(true, directControlLimbBuffer);
            directControlLimbs.Clear();
            directControlLimbs.AddRange(directControlLimbBuffer.Where(limb => limb != null));
        }

        private int GetDirectControlTopologyFingerprint()
        {
            unchecked
            {
                var fingerprint = transform.childCount;
                var limbs = person == null ? null : person.Limbs;
                fingerprint = fingerprint * 31 + (limbs == null ? 0 : limbs.Length);
                if (limbs == null)
                {
                    return fingerprint;
                }

                foreach (var limb in limbs)
                {
                    fingerprint = fingerprint * 31 + (limb == null ? 0 : limb.GetHashCode());
                }

                return fingerprint;
            }
        }

        private void RestoreDirectFlyControl()
        {
            RestoreDirectFlyControlState();
        }

        private void RestoreDirectFlyControlState()
        {
            if (directControlSnapshots.Count == 0 && directControlPoseSnapshots.Count == 0)
            {
                directControlLimbBuffer.Clear();
                directControlLimbs.Clear();
                directControlTopologyKnown = false;
                nextDirectControlTopologyCheck = 0f;
                nextDirectControlHierarchyDiscovery = 0f;
                return;
            }

            foreach (var snapshot in directControlSnapshots.Values)
            {
                snapshot.Restore();
            }

            directControlSnapshots.Clear();
            directControlSnapshotValues.Clear();
            directControlLimbBuffer.Clear();
            directControlLimbs.Clear();
            directControlTopologyKnown = false;
            nextDirectControlTopologyCheck = 0f;
            nextDirectControlHierarchyDiscovery = 0f;
            RestoreDirectPoseControl();
        }

        private void ApplyDirectPoseControl()
        {
            if (person == null)
            {
                return;
            }

            var poses = person.Poses;
            if (poses != null)
            {
                foreach (var pose in poses)
                {
                    SuppressPose(pose);
                }
            }

            SuppressPose(person.ActivePose);
        }

        private void SuppressPose(RagdollPose pose)
        {
            if (pose == null)
            {
                return;
            }

            if (!directControlPoseSnapshots.ContainsKey(pose))
            {
                directControlPoseSnapshots.Add(pose, new NativePoseSnapshot(pose));
            }

            pose.ShouldStandUpright = false;
            pose.ShouldStumble = false;
            pose.UprightForceMultiplier = 0f;
            pose.ForceMultiplier = 0f;
        }

        private void RestoreDirectPoseControl()
        {
            directControlPoseSnapshotValues.Clear();
            foreach (var snapshot in directControlPoseSnapshots.Values)
            {
                directControlPoseSnapshotValues.Add(snapshot);
            }

            foreach (var snapshot in directControlPoseSnapshotValues)
            {
                snapshot.Restore();
            }

            directControlPoseSnapshots.Clear();
            directControlPoseSnapshotValues.Clear();
        }

        private sealed class NativeAssistSnapshot
        {
            private readonly LimbBehaviour limb;
            private readonly float fakeUprightForce;
            private readonly float balanceMuscleMovement;
            private readonly bool doBalanceJerk;
            private readonly bool doStumble;

            public NativeAssistSnapshot(LimbBehaviour limb)
            {
                this.limb = limb;
                fakeUprightForce = limb.FakeUprightForce;
                balanceMuscleMovement = limb.BalanceMuscleMovement;
                doBalanceJerk = limb.DoBalanceJerk;
                doStumble = limb.DoStumble;
            }

            public LimbBehaviour Limb => limb;

            public bool IsAttached(Transform root) => limb != null && limb.transform.IsChildOf(root);

            public void Restore()
            {
                if (limb == null)
                {
                    return;
                }

                limb.FakeUprightForce = fakeUprightForce;
                limb.BalanceMuscleMovement = balanceMuscleMovement;
                limb.DoBalanceJerk = doBalanceJerk;
                limb.DoStumble = doStumble;
            }
        }

        private sealed class NativePoseSnapshot
        {
            private readonly RagdollPose pose;
            private readonly bool shouldStandUpright;
            private readonly bool shouldStumble;
            private readonly float uprightForceMultiplier;
            private readonly float forceMultiplier;

            public NativePoseSnapshot(RagdollPose pose)
            {
                this.pose = pose;
                shouldStandUpright = pose.ShouldStandUpright;
                shouldStumble = pose.ShouldStumble;
                uprightForceMultiplier = pose.UprightForceMultiplier;
                forceMultiplier = pose.ForceMultiplier;
            }

            public void Restore()
            {
                if (pose == null)
                {
                    return;
                }

                pose.ShouldStandUpright = shouldStandUpright;
                pose.ShouldStumble = shouldStumble;
                pose.UprightForceMultiplier = uprightForceMultiplier;
                pose.ForceMultiplier = forceMultiplier;
            }
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
