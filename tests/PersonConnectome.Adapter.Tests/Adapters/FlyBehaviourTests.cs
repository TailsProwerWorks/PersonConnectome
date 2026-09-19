using ShadowNineX.PersonConnectome;
using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;
using UnityEngine;
using Xunit;

namespace ShadowNineX.PersonConnectome.AdapterTests;

public sealed class FlyBehaviourTests
{
    private static (GameObject Root, Rigidbody2D Body, PeoplePlaygroundFlyAdapter Adapter) CreateFly()
    {
        Physics2D.Hits = [];
        Physics2D.LinecastHits = [];
        Physics2D.LinecastResult = default;
        Physics2D.LinecastHandler = null;
        RenderSettings.ambientLight = new Color(1f, 1f, 1f, 1f);
        var root = new GameObject("Fly");
        var body = root.AddComponent<Rigidbody2D>();
        root.AddComponent<Collider2D>();
        return (root, body, new PeoplePlaygroundFlyAdapter(root));
    }

    private static void Apply(PeoplePlaygroundFlyAdapter adapter, FlyMotorCommand command, float dt = .02f) =>
        adapter.Apply(command, false, 0f, 0f, dt);

    [Fact]
    public void RegionalHealthFeedsFlyTelemetryAndStopsADeadFly()
    {
        var root = new GameObject("Fly");
        var body = root.AddComponent<Rigidbody2D>();
        root.AddComponent<PhysicalBehaviour>().rigidbody = body;
        var health = root.AddComponent<FlyHealth>();
        health.Initialize();
        health.ApplyDamage(FlyHealthRegion.FrontLeftLeg, .4f);

        var adapter = new PeoplePlaygroundFlyAdapter(root);
        var injured = adapter.Read();
        Assert.True(injured.Alive);
        Assert.True(injured.HealthValid);
        Assert.True(injured.DamageValid);
        Assert.InRange(injured.Vitality, .59f, .61f);
        Assert.Contains("front-left-leg=0.60", health.LimbHealthSummary);

        health.ApplyDamage(FlyHealthRegion.Thorax, 1f);
        var dead = adapter.Read();
        Assert.False(dead.Alive);
        var before = body.velocity;
        Apply(adapter, new FlyMotorCommand { FlyFlightPower = 1f, FlyForward = 1f });
        Assert.Equal(before.x, body.velocity.x);
        Assert.Equal(before.y, body.velocity.y);
    }

    [Fact]
    public void ForwardFollowsMirroredHeadAndBackwardReverses()
    {
        var fly = CreateFly();
        Apply(fly.Adapter, new FlyMotorCommand { FlyForward = 1f });
        Assert.True(fly.Body.velocity.x < 0f);
        fly.Body.velocity = default;
        fly.Root.transform.lossyScale = new Vector3(-1f, 1f, 1f);
        Apply(fly.Adapter, new FlyMotorCommand { FlyForward = 1f });
        Assert.True(fly.Body.velocity.x > 0f);
        fly.Body.velocity = default;
        Apply(fly.Adapter, new FlyMotorCommand { FlyBackward = 1f });
        Assert.True(fly.Body.velocity.x < 0f);
    }

    [Fact]
    public void RepeatedCommandsStayBoundedAndHaltWins()
    {
        var fly = CreateFly();
        for (var i = 0; i < 500; i++)
            Apply(fly.Adapter, new FlyMotorCommand { FlyForward = 1f, FlyYaw = 1f });
        Assert.InRange(Math.Abs(fly.Body.velocity.x), 0f, 2f);
        Assert.InRange(Math.Abs(fly.Body.angularVelocity), 0f, 120f);
        for (var i = 0; i < 100; i++)
            Apply(fly.Adapter, new FlyMotorCommand { FlyForward = 1f, FlyYaw = 1f, FlyHalt = .2f });
        Assert.Equal(0f, fly.Body.velocity.x);
        Assert.Equal(0f, fly.Body.angularVelocity);
    }

    [Fact]
    public void RotationEscapeDirectionAndFlightHaltReachTheBody()
    {
        var fly = CreateFly();
        fly.Root.transform.RotationDegrees = 90f;
        Apply(fly.Adapter, new FlyMotorCommand { FlyForward = 1f });
        Assert.True(fly.Body.velocity.y < 0f);
        Assert.InRange(Math.Abs(fly.Body.velocity.x), 0f, .00001f);
        fly.Root.transform.RotationDegrees = 0f;
        fly.Body.velocity = default;
        Apply(fly.Adapter, new FlyMotorCommand { FlyEscape = 1f, FlyBackward = 1f });
        Assert.True(fly.Body.velocity.x > 0f);
        fly.Body.velocity = default;
        Apply(fly.Adapter, new FlyMotorCommand { FlyEscape = 1f, FlyFlightPower = 1f, FlyJump = 1f, FlyHalt = .2f });
        Assert.Equal(0f, fly.Body.velocity.x);
        Assert.Equal(0f, fly.Body.velocity.y);
        Assert.Equal("Halting", fly.Adapter.Activity);
    }

    private sealed class RecordingRig : MonoBehaviour, IFlyBodyRig
    {
        public bool IsUsable => true;
        public FlyMotorCommand Last;
        void IFlyBodyRig.Apply(FlyMotorCommand command, float elapsedSeconds) => Last = command;
    }

    [Fact]
    public void HeldFlightAndEscapeAnimateWingsUntilLanding()
    {
        var fly = CreateFly();
        var rig = fly.Root.AddComponent<RecordingRig>();
        Apply(fly.Adapter, new FlyMotorCommand { FlyEscape = 1f });
        Assert.True(rig.Last.FlyFlightPower > .1f);
        Apply(fly.Adapter, default);
        Assert.True(rig.Last.FlyFlightPower > .1f);
        Assert.True(fly.Body.velocity.y > 0f);
        Apply(fly.Adapter, new FlyMotorCommand { FlyLanding = 1f });
        Assert.Equal(0f, rig.Last.FlyFlightPower);
    }

    [Fact]
    public void FlyInferenceStaysFrozenUntilTeachStarts()
    {
        var brain = new LifBrain();
        var training = new ConnectomeTrainingSession(brain, "test.fly.inference", null,
            ConnectomeLearningMode.FrozenBaseline);

        Assert.Equal(ConnectomeLearningMode.FrozenBaseline, brain.LearningMode);
        Assert.Equal(0, training.ApplyAutonomousFeedback(1f, "unrequested reward"));
        Assert.Equal(0, training.AutonomousFeedbackCount);

        training.Start();
        Assert.Equal(ConnectomeLearningMode.PlasticConnectome, brain.LearningMode);
        training.End();
        Assert.Equal(ConnectomeLearningMode.FrozenBaseline, brain.LearningMode);
    }

    [Fact]
    public void StopPreservesPassivePhysicsAndResumeRestoresControl()
    {
        var fly = CreateFly();
        fly.Body.velocity = new Vector2(1f, -2f);
        fly.Adapter.Stop();
        Apply(fly.Adapter, new FlyMotorCommand { FlyForward = 1f });
        Assert.Equal(1f, fly.Body.velocity.x);
        Assert.Equal(-2f, fly.Body.velocity.y);
        fly.Adapter.Resume();
        Apply(fly.Adapter, new FlyMotorCommand { FlyForward = 1f });
        Assert.True(fly.Body.velocity.x < 1f);
        Assert.Equal(-2f, fly.Body.velocity.y);
    }

    [Fact]
    public void InvalidInputsAndFrozenBodiesDoNotCorruptPhysics()
    {
        var fly = CreateFly();
        Apply(fly.Adapter, new FlyMotorCommand { FlyForward = float.NaN, FlyYaw = float.PositiveInfinity });
        Assert.Equal(0f, fly.Body.velocity.x);
        Assert.Equal(0f, fly.Body.angularVelocity);
        Apply(fly.Adapter, new FlyMotorCommand { FlyForward = 1f }, float.NaN);
        Assert.Equal(0f, fly.Body.velocity.x);
        fly.Body.bodyType = RigidbodyType2D.Static;
        Apply(fly.Adapter, new FlyMotorCommand { FlyFlightPower = 1f, FlyForward = 1f });
        Assert.Equal(0f, fly.Body.velocity.y);
    }

    [Fact]
    public void JumpIsAnEdgeTriggeredGroundImpulse()
    {
        var fly = CreateFly();
        fly.Root.AddComponent<PhysicalBehaviour>().IsTouchingSomething = true;
        fly.Adapter.Read();
        Apply(fly.Adapter, new FlyMotorCommand { FlyJump = 1f });
        Assert.Equal(3f, fly.Body.velocity.y);
        fly.Body.velocity = default;
        for (var i = 0; i < 100; i++) Apply(fly.Adapter, new FlyMotorCommand { FlyJump = 1f });
        Assert.Equal(0f, fly.Body.velocity.y);
        Apply(fly.Adapter, default);
        Apply(fly.Adapter, new FlyMotorCommand { FlyJump = 1f });
        Assert.Equal(3f, fly.Body.velocity.y);
    }

    [Fact]
    public void FlightHoldExpiresAndLandingReturnsToGravity()
    {
        var fly = CreateFly();
        Apply(fly.Adapter, new FlyMotorCommand { FlyTakeoff = 1f });
        Assert.Equal("Flying", fly.Adapter.Activity);
        for (var i = 0; i < 40; i++) Apply(fly.Adapter, default);
        Assert.Equal("Resting", fly.Adapter.Activity);
        fly.Body.velocity = new Vector2(0f, -2f);
        Apply(fly.Adapter, new FlyMotorCommand { FlyLanding = 1f });
        Assert.Equal(-2f, fly.Body.velocity.y);
    }

    [Fact]
    public void WingMotorFeedbackCanChooseFlightWithoutSeparateTakeoffSignal()
    {
        var fly = CreateFly();
        Apply(fly.Adapter, new FlyMotorCommand { FlyWingMotor = .5f });
        Assert.Equal("Flying", fly.Adapter.Activity);
        Assert.True(fly.Body.velocity.y > 0f);
    }

    [Fact]
    public void VisibleTargetAddsBoundedApproachAndFlightTurn()
    {
        var fly = CreateFly();
        var target = new GameObject("Target");
        target.AddComponent<PhysicalBehaviour>().rigidbody = target.AddComponent<Rigidbody2D>();
        var targetCollider = target.AddComponent<Collider2D>();
        targetCollider.Surface = new Vector2(-2f, 1f);
        targetCollider.bounds = new Bounds { center = new Vector3(-2f, 1f, 0f), extents = new Vector3(.25f, .25f, 0f) };
        Physics2D.Hits = [targetCollider];
        Physics2D.LinecastResult = new RaycastHit2D { collider = targetCollider };

        fly.Adapter.Read();
        Apply(fly.Adapter, new FlyMotorCommand { FlyFlightPower = .8f });

        Assert.True(fly.Body.velocity.x < 0f);
        Assert.True(fly.Body.velocity.y > 0f);
        Assert.True(fly.Body.angularVelocity < 0f);
        Assert.InRange(Math.Abs(fly.Body.angularVelocity), 0f, 120f);
    }

    [Fact]
    public void LoomingGeometryUsesDegreesAndOcclusion()
    {
        var fly = CreateFly();
        var target = new GameObject("Approaching object");
        var physical = target.AddComponent<PhysicalBehaviour>();
        physical.rigidbody = target.AddComponent<Rigidbody2D>();
        physical.rigidbody.velocity = new Vector2(4f, 0f);
        var hit = target.AddComponent<Collider2D>();
        hit.Surface = new Vector2(-1f, 0f);
        hit.bounds = new Bounds { center = new Vector3(-2f, 0f, 0f), extents = new Vector3(1f, 1f, 0f) };
        Physics2D.Hits = [hit];
        Physics2D.LinecastResult = new RaycastHit2D { collider = hit };
        var frame = fly.Adapter.Read();
        Assert.True(frame.VisualFieldValid && frame.ViewFront.GeometryValid);
        Assert.InRange(frame.ViewFront.AngularSize, 53f, 54f);
        Assert.True(frame.ViewFront.Expansion > 80f);
        Assert.True(frame.Vision > 0f && !frame.VisionLimited);
        RenderSettings.ambientLight = new Color { grayscale = float.NaN };
        Assert.Equal(0f, fly.Adapter.Read().Vision);
        RenderSettings.ambientLight = new Color(1f, 1f, 1f, 1f);
        var wall = new GameObject("Wall").AddComponent<Collider2D>();
        Physics2D.LinecastResult = new RaycastHit2D { collider = wall };
        Assert.Equal(0f, fly.Adapter.Read().Vision);
    }

    [Fact]
    public void ApproachingProjectileBecomesAVisibleDangerSignal()
    {
        var fly = CreateFly();
        var projectile = new GameObject("Incoming projectile");
        projectile.AddComponent<ProjectileBehaviour>();
        var physical = projectile.AddComponent<PhysicalBehaviour>();
        physical.rigidbody = projectile.AddComponent<Rigidbody2D>();
        physical.rigidbody.velocity = new Vector2(8f, 0f);
        var hit = projectile.AddComponent<Collider2D>();
        hit.Surface = new Vector2(-1f, 0f);
        hit.bounds = new Bounds { center = new Vector3(-2f, 0f, 0f), extents = new Vector3(.2f, .2f, 0f) };
        Physics2D.Hits = [hit];
        Physics2D.LinecastResult = new RaycastHit2D { collider = hit };

        var frame = fly.Adapter.Read();

        Assert.True(frame.Projectile > 0f);
        Assert.True(frame.ViewFront.Approach > 0f);
        Assert.True(frame.VisualFieldValid);
    }

    [Fact]
    public void FoodRequiresStockIdentityAndContactAndDisintegrationStopsControl()
    {
        var fly = CreateFly();
        ModAPI.Pumpkin = new SpawnableAsset { name = "Pumpkin" };
        var food = new GameObject("Pumpkin");
        food.AddComponent<PhysicalBehaviour>();
        var identity = food.AddComponent<SerialiseInstructions>();
        var hit = food.AddComponent<Collider2D>();
        hit.Surface = new Vector2(-1f, 0f);
        Physics2D.Hits = [hit];
        Assert.Equal(0f, fly.Adapter.Read().FoodNearbyCue);
        identity.OriginalSpawnableAsset = ModAPI.Pumpkin;
        Assert.True(fly.Adapter.Read().FoodNearbyCue > 0f);
        Assert.Equal(0f, fly.Adapter.LastFrame.FoodContactCue);
        fly.Root.GetComponent<Collider2D>().Touching = hit;
        Assert.Equal(0f, fly.Adapter.Read().FoodContactCue);
        hit.Surface = new Vector2(-.45f, 0f);
        Assert.Equal(1f, fly.Adapter.Read().FoodContactCue);
        fly.Root.AddComponent<PhysicalBehaviour>().isDisintegrated = true;
        Assert.False(fly.Adapter.Read().Alive);
        Assert.True(fly.Adapter.IsTerminal);
        Apply(fly.Adapter, new FlyMotorCommand { FlyForward = 1f });
        Assert.Equal(0f, fly.Body.velocity.x);
    }

    [Fact]
    public void DedicatedFlyTreatProvidesFoodCueWithoutStockNameGuessing()
    {
        ModAPI.Pumpkin = null;
        var fly = CreateFly();
        var treat = new GameObject("Workshop treat");
        treat.AddComponent<PhysicalBehaviour>();
        treat.AddComponent<FlyFoodMarker>();
        var hit = treat.AddComponent<Collider2D>();
        hit.Surface = new Vector2(-1f, 0f);
        Physics2D.Hits = [hit];
        Physics2D.LinecastResult = new RaycastHit2D { collider = hit };

        var frame = fly.Adapter.Read();
        Assert.True(frame.FoodCuesValid);
        Assert.True(frame.FoodNearbyCue > 0f);
    }

    [Fact]
    public void HarmMemoryRecognizesTheSameSpawnedObjectCategoryButNotAnother()
    {
        var root = new GameObject("Fly");
        var memory = root.AddComponent<FlyExperienceMemory>();
        var bowlingAsset = new SpawnableAsset { name = "Bowling Ball" };
        var firstBall = new GameObject("Bowling Ball (Clone)");
        firstBall.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = bowlingAsset;
        var secondBall = new GameObject("Another copy");
        secondBall.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = bowlingAsset;
        var gun = new GameObject("Pistol");
        gun.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = new SpawnableAsset { name = "Pistol" };

        Assert.True(memory.RecordHarm(firstBall, .3f));
        Assert.True(memory.ThreatFor(secondBall) >= .2f);
        Assert.Equal(1, memory.HarmCountFor(secondBall));
        Assert.Equal(0f, memory.ThreatFor(gun));
        Assert.Equal("Bowling Ball", memory.LastHarmfulObject);
        memory.Clear();
        Assert.Equal(0f, memory.ThreatFor(secondBall));
        Assert.Equal("none", memory.LastHarmfulObject);
    }

    [Fact]
    public void RememberedHarmfulCategoryReturnsAsALearnedVisualThreat()
    {
        var root = new GameObject("Fly");
        root.AddComponent<Rigidbody2D>();
        root.AddComponent<Collider2D>();
        var memory = root.AddComponent<FlyExperienceMemory>();
        var bowlingAsset = new SpawnableAsset { name = "Bowling Ball" };
        var priorBall = new GameObject("Prior ball");
        priorBall.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = bowlingAsset;
        memory.RecordHarm(priorBall, .7f);
        var adapter = new PeoplePlaygroundFlyAdapter(root);

        var visibleBall = new GameObject("Visible ball");
        visibleBall.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = bowlingAsset;
        var hit = visibleBall.AddComponent<Collider2D>();
        hit.Surface = new Vector2(-1f, 0f);
        hit.bounds = new Bounds { center = new Vector3(-1f, 0f, 0f), extents = new Vector3(.2f, .2f, 0f) };
        Physics2D.Hits = [hit];
        Physics2D.LinecastResult = new RaycastHit2D { collider = hit };

        var frame = adapter.Read();
        Assert.True(frame.LearnedThreatValid);
        Assert.True(frame.LearnedThreat > 0f);
        Assert.InRange(frame.LearnedThreatDirection, -.01f, .01f);
        Assert.Contains("Bowling Ball", adapter.LearnedThreatSummary);

        RenderSettings.ambientLight = new Color(0f, 0f, 0f, 1f);
        var darkFrame = adapter.Read();
        Assert.False(darkFrame.LearnedThreatValid);
        Assert.Equal(0f, darkFrame.LearnedThreat);
    }

    [Fact]
    public void FoodAndPainProduceOppositeDopamineFeedbackOnRecentNeuralEligibility()
    {
        var brain = new LifBrain();
        var training = new ConnectomeTrainingSession(brain, "test.fly.dopamine", null,
            ConnectomeLearningMode.PlasticConnectome);
        var dopamine = new FlyDopamineSystem();

        Assert.Equal(ConnectomeLearningMode.PlasticConnectome, brain.LearningMode);
        var reward = dopamine.Observe(new SensoryFrame { FoodCuesValid = true, FoodContactCue = 1f }, .05f);
        Assert.True(reward > 0f);
        Assert.Equal(1, training.ApplyAutonomousFeedback(reward, dopamine.LastEvent));
        Assert.True(brain.LastFeedback > 0f);

        var punishment = dopamine.Observe(new SensoryFrame { DamageEvent = .6f, Pain = .6f }, .05f);
        Assert.True(punishment < 0f);
        Assert.Equal(1, training.ApplyAutonomousFeedback(punishment, dopamine.LastEvent, .05f));
        Assert.True(brain.LastFeedback < 0f);
        Assert.Equal(.05f, brain.AdvancedLearningSeconds);
        Assert.Equal(2, training.AutonomousFeedbackCount);
        Assert.Contains("pain", training.StatusText);
        Assert.Equal(0f, dopamine.Observe(new SensoryFrame { Pain = .6f, Damage = .6f }, .05f));

        training.Start();
        training.TogglePause();
        Assert.Equal(0, training.ApplyAutonomousFeedback(-1f, "paused pain"));
        training.End();
        Assert.Equal(ConnectomeLearningMode.PlasticConnectome, brain.LearningMode);
    }

    [Fact]
    public void ResetSkillClearsBothSynapticAndObjectCategoryMemory()
    {
        var root = new GameObject("Fly");
        root.AddComponent<Rigidbody2D>();
        var memory = root.AddComponent<FlyExperienceMemory>();
        var harmful = new GameObject("Bowling Ball");
        harmful.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = new SpawnableAsset { name = "Bowling Ball" };
        memory.RecordHarm(harmful, .5f);
        var adapter = new PeoplePlaygroundFlyAdapter(root);
        var brain = new LifBrain { LearnedSynapseCount = 2 };
        var training = new ConnectomeTrainingSession(brain, "test.fly.reset", null, ConnectomeLearningMode.PlasticConnectome);
        var bindings = training.CreateBindings(adapter.ResetLearnedThreats);

        bindings.Reset();

        Assert.Equal(0, brain.LearnedSynapseCount);
        Assert.Equal(0f, memory.ThreatFor(harmful));
        Assert.Equal("none", memory.LastHarmfulObject);
    }
}
