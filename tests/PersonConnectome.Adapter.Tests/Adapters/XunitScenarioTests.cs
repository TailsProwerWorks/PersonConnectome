using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Xunit;
using Xunit.v3;

[assembly: Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

public sealed class AdapterScenarioTests
{
    public static IEnumerable<object[]> Scenarios
    {
        get
        {
            yield return Case("food cues require original stock identity and actual head contact", "FoodItemCues");
            yield return Case("blood readings distinguish raw amount, relative baseline and detached limbs", "BloodMeasurementContract");
            yield return Case("explicit limb loss emits one measured injury event", "LimbLossEvents");
            yield return Case("jukebox and child playback feed current directional audio", "JukeboxAudio");
            yield return Case("audio source scans stay bounded and exclude self sources", "BoundedAudioSources");
            yield return Case("terminal motors and grips clear immediately", "TerminalStop");
            yield return Case("native pose context actions are suppressed", "ContextMenuPoseActions");
            yield return Case("unconscious and locally damaged limbs clear old commands", "IncapableStop");
            yield return Case("brain injury remains alive with matching signal value", "BrainInjury");
            yield return Case("invalid health stops control without inventing death", "InvalidHealth");
            yield return Case("destroyed and newly added inactive limbs", "LimbLifecycle");
            yield return Case("all 41 stock liquid IDs have explicit exposure routes", "Liquids");
            yield return Case("unknown and invalid liquids cannot fabricate effects", "UnknownAndInvalidLiquids");
            yield return Case("liquid snapshots and native zombie state stay separate", "LiquidSnapshots");
            yield return Case("acid pools produce an explicit acid signal", "AcidPools");
            yield return Case("paralysis, breakage and limb loss stay distinct", "LimbDamageCategories");
            yield return Case("blood baseline and derived minimum limb health", "BloodAndVitality");
            yield return Case("invalid limb health and blood remain unknown", "InvalidLimbSamples");
            yield return Case("small health baselines preserve normalized health and injury events", "SmallHealthBaselines");
            yield return Case("hypoxia and submersion remain distinct", "Oxygen");
            yield return Case("invalid oxygen remains unknown without hypoxia", "InvalidOxygen");
            yield return Case("invalid consciousness suspends output without an unconscious claim", "InvalidConsciousness");
            yield return Case("circulation reports unknown when native flow is unavailable", "InvalidCirculation");
            yield return Case("nearby temperatures provide bounded ambient heat and cold", "AmbientTemperature");
            yield return Case("nearby lava provides heat and hazard", "NearbyLava");
            yield return Case("external audio excludes own limbs, mute and invalid distances", "Audio");
            yield return Case("numbered and cloned Root audio remains conservatively filtered", "NumberedRootAudio");
            yield return Case("last-heard audio persists without stimulating stale sound", "AudioHistory");
            yield return Case("native light owners affect the combined light sensor", "NativeLightSources");
            yield return Case("local light respects state, transformed footprint and invalid data", "LocalLightGeometry");
            yield return Case("local light scan reports truncation and avoids duplicate strength", "LocalLightLimits");
            yield return Case("visible external objects produce a vision proxy", "Vision");
            yield return Case("head rotation and mirroring change the observed field", "HeadRelativeVision");
            yield return Case("visible target search skips blocked and out-of-view candidates", "VisibleTargetSelection");
            yield return Case("visual search stays bounded and reports incomplete queries", "VisualSearchLimits");
            yield return Case("head rotation produces sweep without inventing approach", "HeadRelativeSweep");
            yield return Case("stationary head requests reach only the healthy native head joint", "StationaryHeadMotor");
            yield return Case("surroundings retain independent visible bands as value snapshots", "SpatialSurroundings");
            yield return Case("audio spectra use one current external source and reject invalid data", "SpectrumValidity");
            yield return Case("visual geometry and rotation follow finite native measurements", "GeometryAndRotation");
            yield return Case("directional native sensory readings clear and stay source-bound", "DirectionalSensors");
            yield return Case("damage events and regional contact are observed edge samples", "ObservedBodySensors");
            yield return Case("expanded sensing preserves environment telemetry", "EnvironmentTelemetry");
            yield return Case("vibration, proprioception and projectile channels stay distinct", "AdditionalSenses");
            yield return Case("falling is distinct from walking and floor contact", "Falling");
            yield return Case("contact impacts do not fabricate hearing", "Impacts");
            yield return Case("detached owned limbs remain excluded from collision signals", "DetachedOwnCollisions");
            yield return Case("detached source probes cannot report collisions", "DetachedSourceCollisions");
            yield return Case("connected source probes report projectiles", "ConnectedSourceProjectiles");
            yield return Case("detached limbs remain diagnostic but cannot feed body control", "DetachedLimbsDoNotControl");
            yield return Case("control clock preserves rate and caps catch-up", "ControlClock");
            yield return Case("suspension clears transient events", "SuspensionClearsTransientEvents");
            yield return Case("initially disabled controllers reject events", "InitiallyDisabledControllersRejectEvents");
            yield return Case("disabled controllers do not claim telemetry", "DisabledControllersDoNotClaimTelemetry");
            yield return Case("regeneration ownership and cleanup", "Chemistry");
            yield return Case("front-back hierarchy routes separate channels", "SideRouting");
            yield return Case("missing grip and joint do not interrupt other limbs", "OptionalControls");
            yield return Case("zero grip leaves an existing hold unchanged", "NeutralGrip");
            yield return Case("broken limbs do not suppress healthy limb control", "LocalLimbDamage");
            yield return Case("dead limb health stops only its own actuator", "LocalDeadLimb");
            yield return Case("finite boundary sanitization", "FiniteInputs");
            yield return Case("telemetry follows native readings independently of requests", "NativeTelemetryTrace");
            yield return Case("native motor requests use bounded walking and angular units", "NativeMotorUnits");
            yield return Case("freeze cutoff clears actuators consistently", "FreezeCutoff");
            yield return Case("walking requests persist between neural ticks", "WalkingRequestMaintenance");
            yield return Case("walking maintenance stops on current native state", "WalkingMaintenanceChecksCurrentState");
            yield return Case("component disable clears commands and chemistry", "Disable");
            yield return Case("never-activated cleanup preserves game state", "NeverActivated");
            yield return Case("neutral chemistry preserves adrenaline", "NeutralChemistry");
            yield return Case("active chemistry respects native adrenaline range", "NativeAdrenalineRange");
            yield return Case("hazard walking keeps the native pose gate", "HazardWalkingKeepsNativeGate");
            yield return Case("chemistry interventions scale with elapsed time", "ChemistryScalesWithElapsedTime");
            yield return Case("future fly adapter reports unsupported capabilities safely", "FlyAdapterIsDisabled");
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void ScenarioPasses(string _, string methodName)
    {
        Physics2D.LinecastHandler = null;
        Physics2D.LinecastCalls = 0;
        Physics2D.Hits = [];
        Physics2D.LinecastHits = [];
        Physics2D.LinecastResult = default;
        var method = typeof(Program).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new InvalidOperationException("Scenario method not found: " + methodName);
        try
        {
            method.Invoke(null, null);
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    private static object[] Case(string name, string methodName) => new object[] { name, methodName };
}
