using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.v3;
using UnityEngine;

[assembly: Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace ShadowNineX.PersonConnectome.Runtime.Tests;

public sealed class RuntimeScenarioTests
{
    private static int payloadInitialized;

    public static IEnumerable<object[]> Scenarios
    {
        get
        {
            yield return Case("qualified escape starts a bounded neural walking burst from idle", "EscapeLocomotion");
            yield return Case("signed input accumulation is order independent", "SignedInputAccumulationIsOrderIndependent");
            yield return Case("refractory expires after silent ticks", "RefractoryExpiresAfterSilentTicks");
            yield return Case("refractory propagation resumes on exact recovery tick", "RefractoryPropagationRecovers");
            yield return Case("same-tick refractory targets do not consume the budget", "SameTickRefractoryTargets");
            yield return Case("refractory work cannot starve fresh input", "RefractoryWorkCannotStarveFreshInput");
            yield return Case("sensory drive resumes on exact refractory recovery", "SensoryRefractoryRecovery");
            yield return Case("overload preserves fresh propagated input", "OverloadPreservesFreshPropagatedInput");
            yield return Case("excess firing events are dropped without replay", "ExcessFiringEventsDoNotReplay");
            yield return Case("overload cursor includes interspersed priority positions", "OverloadCursorIncludesInterspersedPriorityPositions");
            yield return Case("fresh sensory inputs bypass recurrent backlog", "FreshSensoryInputsBypassRecurrentBacklog");
            yield return Case("terminal reset clears state and recovers", "TerminalResetClearsStateAndRecovers");
            yield return Case("invalid health suspends without resetting neural state", "InvalidHealthSuspendsWithoutReset");
            yield return Case("invalid health invalidates light transition baseline", "InvalidHealthInvalidatesLightBaseline");
            yield return Case("healthy standing leaves sensory headroom", "HealthyStateLeavesSensoryHeadroom");
            yield return Case("injury and native adrenaline do not synthesize endocrine commands", "NativeStressDoesNotDriveChemistry");
            yield return Case("internal chemistry does not fabricate a sensory receptor", "InternalStatesDoNotFabricateReceptors");
            yield return Case("blood and vitality do not fabricate visual input", "NormalizedBloodAndVitalityDriveInjury");
            yield return Case("submersion without neural motor activity is still", "SubmersionWithoutNeuralMotorActivityIsStill");
            yield return Case("supported shallow water keeps normal control", "SupportedShallowWaterKeepsNormalControl");
            yield return Case("hazard cannot force walking without motor activity", "HazardCannotForceWalkingWithoutMotorActivity");
            yield return Case("lost movement authority clears motor requests", "LostMovementAuthorityClearsMotorRequests");
            yield return Case("published fly command matches telemetry and resets", "PublishedCommandMatchesTelemetryAndResets");
            yield return Case("fractional halt activity is published without changing its contract", "FractionalHaltContract");
            yield return Case("reward plasticity is selective, bounded and persistent", "RewardPlasticityIsSelectiveAndPersistent");
            yield return Case("plasticity requires both eligibility and reinforcement", "PlasticityRequiresEligibilityAndReward");
            yield return Case("pair order produces the documented eligibility sign", "PlasticityPairOrder");
            yield return Case("feedback is an impulse rather than a tick-scaled reward", "FeedbackRewardIsAnImpulse");
            yield return Case("default feedback catalog covers fly sensory routes", "DefaultFeedbackCatalogCoversFlySensoryRoutes");
            yield return Case("learned playback applies deltas without further plasticity", "FrozenLearnedPlayback");
            yield return Case("manual looming overrides qualify and suppress neural escape evidence", "ManualVisualEscapeQualification");
            yield return Case("escape evidence expires across long neural tick gaps", "EscapeEvidenceExpiresAcrossLongTickGap");
            yield return Case("motor reversals are rate limited", "MotorReversalsAreRateLimited");
            yield return Case("R7 R8 variants receive light drive", "RetinaVariantsReceiveLightDrive");
            yield return Case("bundled payload identity", "BundledPayloadIdentity");
            yield return Case("bundled ambient light produces real graph spikes", "BundledAmbientLightProducesSpikes");
            yield return Case("food gameplay cues reach only supported sensory populations", "FoodSensoryRoutes");
            yield return Case("bundled sensory and locomotor annotations resolve", "BundledSensoryMappings");
            yield return Case("modalities reach distinct input populations with signed lateralization", "SensoryModalities");
            yield return Case("global luminance changes require a valid continuous baseline", "GlobalLuminanceChanges");
            yield return Case("damage events and regional contact stay in their tactile routes", "InjuryAndRegionalRoutes");
            yield return Case("geometric visual channels reject unavailable and nonfinite features", "GeometricVisualRoutes");
            yield return Case("remembered object threat reaches ordinary visual danger routes", "LearnedObjectThreatRoutes");
            yield return Case("strong neural visual danger qualifies a bounded escape reflex", "StrongVisualDangerQualifiesBoundedEscape");
            yield return Case("escape requests require recent evidence while raw spikes remain visible", "EscapeRequiresContext");
            yield return Case("auditory band weighting uses measured bands or broad fallback", "AuditoryBandRouting");
            yield return Case("neural locomotion modes bridge pulses and expire without input", "LocomotionTemporalBehavior");
            yield return Case("turning populations map lateral activity with immediate safety clearing", "NeuralTurning");
            yield return Case("head-relative visual bearings reach neural turning through synapses", "HeadRelativeTurningLoop");
            yield return Case("spatial visual inputs preserve both sides without multiplying drive", "SpatialVisualInputs");
            yield return Case("named locomotor populations exclude feeding and wing activity", "NamedMotorReadout");
            yield return Case("courtship readout includes intrinsic pC1 neurons", "FlyCourtshipReadout");
            yield return Case("real graph repeats identical input histories deterministically", "RealGraphIsDeterministic");
            yield return Case("sustained full-payload load respects firing and state bounds", "SustainedFullPayloadLoadStaysRealtimeBounded");
            yield return Case("portable SHA-256 vectors and padding boundaries", "PayloadChecksumVectors");
            yield return Case("malformed dataset and counts reject", "MalformedDatasetAndCountsReject");
            yield return Case("soma sample retains real IDs and omits missing positions", "SomaSample");
            yield return Case("diagnostic spikes match actual runtime and reset", "DiagnosticSpikes");
            yield return Case("telemetry fits supported screens with corner clearance", "TelemetryFitsScreens");
            yield return Case("telemetry drag stays reachable and resizing can shrink", "TelemetryDragAndResize");
            yield return Case("display numbers recycle after undo without renumbering survivors", "TelemetryNumbers");
            yield return Case("manual input resolves mixed and manual-only routes", "ManualInputModes");
            yield return Case("manual feature routes work without natural stimuli", "ManualFeatureRoutes");
            yield return Case("manual directional input follows world-axis weighting", "ManualDirection");
            yield return Case("manual pulses consume only valid neural ticks", "ManualPulses");
            yield return Case("manual state stays finite, isolated and cancellable", "ManualStateSafety");
            yield return Case("invalid health disarms manual input", "InvalidHealthDisarmsManualInput");
            yield return Case("unknown channels do not mask valid signals", "MixedUnknownSignals");
            yield return Case("subthreshold inputs and residual decay do not consume spike slots", "SubthresholdIntegration");
            yield return Case("capped spikes preserve priority and fresh arrivals", "CappedSpikePriority");
            yield return Case("uncapped ticks match an independent synchronous reference", "SynchronousReference");
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void ScenarioPasses(string _, string methodName)
    {
        EnsurePayload();
        var method = typeof(global::Program).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            var result = method.Invoke(null, null);
            Assert.Null(result);
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    [Fact]
    public void SixPersonBenchmarkReportsMatchedWorkload()
    {
        EnsurePayload();
        var method = typeof(global::Program).GetMethod("SixPersonBenchmark", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            var result = method.Invoke(null, null);
            Assert.Null(result);
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    [Fact]
    public void FlyVisualStimulusChangesRealGraphMotorReadout()
    {
        EnsurePayload();
        var quiet = ShadowNineX.PersonConnectome.Core.LifBrain.TryCreate(out var status);
        var stimulated = ShadowNineX.PersonConnectome.Core.LifBrain.TryCreate(out _);
        Assert.NotNull(quiet);
        Assert.NotNull(stimulated);
        var changedMotorTicks = 0;
        var changedSpikeTicks = 0;
        for (var tick = 0; tick < 80; tick++)
        {
            var baseline = new ShadowNineX.PersonConnectome.Core.SensoryFrame
            {
                Alive = true,
                HealthValid = true,
                Health = 1f,
                LightValid = true,
                Light = .5f,
                VisualFieldValid = true
            };
            var stimulus = baseline;
            if (tick >= 20 && tick < 60)
            {
                stimulus.ViewFront = new ShadowNineX.PersonConnectome.Core.VisualObservation
                {
                    Observed = true,
                    GeometryValid = true,
                    Strength = .5f,
                    AngularSize = 53.13f,
                    Expansion = 91.67f,
                    Approach = .2f
                };
            }
            var a = quiet.Step(baseline, .05f);
            var b = stimulated.Step(stimulus, .05f);
            if (quiet.FiredCount != stimulated.FiredCount) changedSpikeTicks++;
            if (MotorCommandsDiffer(a, b))
                changedMotorTicks++;
        }
        Console.WriteLine($"FLY visual stimulus: changed spikes on {changedSpikeTicks}/80 ticks; changed motor readout on {changedMotorTicks}/80 ticks. {status}");
        Assert.True(changedSpikeTicks > 0, "Visible approach must reach the retained graph.");
        Assert.True(changedMotorTicks > 0, "Sensory changes must reach motor readouts, not only the brain display.");
    }

    [Fact]
    public void PreparedAssetCreatesBrainsWithoutLaterModApiLoading()
    {
        var sharedAsset = typeof(ShadowNineX.PersonConnectome.Core.LifBrain).GetField("sharedAsset", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(sharedAsset);
        var previousAsset = sharedAsset.GetValue(null);
        var previousTexture = ModAPI.Texture;
        var previousGate = ModAPI.ThrowOnTextureLoad;
        var previousCalls = ModAPI.LoadTextureCalls;
        try
        {
            sharedAsset.SetValue(null, null);
            ModAPI.Texture = CarrierForTest();
            ModAPI.ThrowOnTextureLoad = false;
            ModAPI.LoadTextureCalls = 0;
            Assert.True(ShadowNineX.PersonConnectome.Core.LifBrain.PrepareAsset(out var prepareStatus), prepareStatus);
            Assert.Equal(1, ModAPI.LoadTextureCalls);

            ModAPI.ThrowOnTextureLoad = true;
            var brain = ShadowNineX.PersonConnectome.Core.LifBrain.TryCreate(out var status);

            Assert.NotNull(brain);
            Assert.Contains("loaded", status, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, ModAPI.LoadTextureCalls);
        }
        finally
        {
            sharedAsset.SetValue(null, previousAsset);
            ModAPI.Texture = previousTexture;
            ModAPI.ThrowOnTextureLoad = previousGate;
            ModAPI.LoadTextureCalls = previousCalls;
        }
    }

    private static void EnsurePayload()
    {
        if (payloadInitialized != 0)
            return;

        var method = typeof(global::Program).GetMethod("BundledPayloadIdentity", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Bundled payload scenario not found");
        method.Invoke(null, null);
        payloadInitialized = 1;
    }

    private static object[] Case(string name, string methodName) => new object[] { name, methodName };

    private static Texture2D CarrierForTest()
    {
        var carrier = typeof(global::Program).GetMethod("Carrier", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(carrier);
        var root = typeof(global::Program).GetMethod("RepositoryRoot", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(root);
        var path = Path.Combine((string)root.Invoke(null, null), "assets", "connectome", "malecns-v1.0.flyb.gz");
        return (Texture2D)carrier.Invoke(null, [File.ReadAllBytes(path)]);
    }

    private static bool MotorCommandsDiffer(
        ShadowNineX.PersonConnectome.Core.FlyMotorCommand left,
        ShadowNineX.PersonConnectome.Core.FlyMotorCommand right)
    {
        const float commandTolerance = 0.0001f;
        return MathF.Abs(left.FlyForward - right.FlyForward) > commandTolerance ||
               MathF.Abs(left.FlyBackward - right.FlyBackward) > commandTolerance ||
               MathF.Abs(left.FlyYaw - right.FlyYaw) > commandTolerance ||
               MathF.Abs(left.FlyEscape - right.FlyEscape) > commandTolerance ||
               MathF.Abs(left.FlyWingMotor - right.FlyWingMotor) > commandTolerance ||
               MathF.Abs(left.FlyLegMotor - right.FlyLegMotor) > commandTolerance;
    }
}
