using System.Reflection;
using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;
using UnityEngine;
using Xunit;

namespace ShadowNineX.PersonConnectome.AdapterTests;

public sealed class FlyNativeHealthTests
{
    [Fact]
    public void BoundRegionsReadNativeHealthBloodBurnAndBreakageWithoutSyntheticDamage()
    {
        var root = new GameObject("Fly");
        root.AddComponent<Rigidbody2D>();
        var health = root.AddComponent<FlyHealth>();
        health.Initialize();
        var head = AddNativeLimb(root, "Head", 50f, 100f);
        var thorax = AddNativeLimb(root, "Thorax", 25f, 100f);
        var leg = AddNativeLimb(root, "Front left leg", 60f, 100f);
        leg.Broken = true;
        leg.PhysicalBehaviour.OnFire = true;
        leg.PhysicalBehaviour.BurnIntensity = .8f;
        leg.PhysicalBehaviour.BurnProgress = .4f;
        leg.CirculationBehaviour.BleedingRate = .7f;
        health.BindRegion(FlyHealthRegion.Head, head);
        health.BindRegion(FlyHealthRegion.Thorax, thorax);
        health.BindRegion(FlyHealthRegion.FrontLeftLeg, leg);

        health.ApplyDamage(FlyHealthRegion.FrontLeftLeg, .5f);
        var frame = default(SensoryFrame);
        health.Read(ref frame);

        Assert.True(frame.Alive);
        Assert.InRange(frame.Health, .849f, .851f);
        Assert.InRange(frame.Vitality, .599f, .601f);
        Assert.Equal(.7f, frame.Bleeding);
        Assert.Equal(.8f, frame.Fire);
        Assert.Equal(.4f, frame.BurnProgress);
        Assert.Equal(1f, frame.Breakage);
        Assert.Contains("front-left-leg=0.60", health.LimbHealthSummary);
    }

    [Fact]
    public void NativeHeadOrThoraxFailureKillsTheFlyAndStopsMotoring()
    {
        var root = new GameObject("Fly");
        var body = root.AddComponent<Rigidbody2D>();
        var health = root.AddComponent<FlyHealth>();
        health.Initialize();
        var head = AddNativeLimb(root, "Head", 0f, 100f);
        var thorax = AddNativeLimb(root, "Thorax", 100f, 100f);
        health.BindRegion(FlyHealthRegion.Head, head);
        health.BindRegion(FlyHealthRegion.Thorax, thorax);
        var adapter = new PeoplePlaygroundFlyAdapter(root);

        var frame = adapter.Read();
        adapter.Apply(new FlyMotorCommand { FlyForward = 1f, FlyFlightPower = 1f }, false, 0f, 0f, .02f);

        Assert.False(frame.Alive);
        Assert.Equal(0f, body.velocity.x);
        Assert.Equal(0f, body.velocity.y);
    }

    [Fact]
    public void LiveCriticalDamageStopsFlightBetweenNeuralSamplesAndInitialAggregateDoesNotKillSpawn()
    {
        var root = new GameObject("Thorax");
        var body = root.AddComponent<Rigidbody2D>();
        var health = root.AddComponent<FlyHealth>();
        var head = AddNativeLimb(root, "Head", 100f, 100f);
        var thorax = AddNativeLimb(root, "Native thorax", 100f, 100f);
        var person = root.AddComponent<PersonBehaviour>();
        person.AverageHealth = 0f;
        head.Person = thorax.Person = person;
        health.BindRegion(FlyHealthRegion.Head, head);
        health.BindRegion(FlyHealthRegion.Thorax, thorax);
        var adapter = new PeoplePlaygroundFlyAdapter(root);
        Assert.True(adapter.Read().Alive);
        head.Health = 0f;
        adapter.Apply(new FlyMotorCommand { FlyFlightPower = 1f }, false, 0f, 0f, .02f);
        Assert.Equal(0f, body.velocity.y);
        Assert.True(adapter.LastFrame.Alive); // Deliberately stale sample.
        head.Health = 100f;
        person.Braindead = true;
        Assert.False(health.IsAlive);
    }

    [Fact]
    public void DestroyedBoundPartRemainsRegionalLossAfterItLeavesTheHierarchy()
    {
        var root = new GameObject("Fly");
        var health = root.AddComponent<FlyHealth>();
        health.Initialize();
        var wing = AddNativeLimb(root, "Left wing", 100f, 100f);
        health.BindRegion(FlyHealthRegion.LeftWing, wing);
        var healthy = default(SensoryFrame);
        health.Read(ref healthy);
        wing.gameObject.Destroyed = true;

        var damaged = default(SensoryFrame);
        health.Read(ref damaged);

        Assert.Equal(.125f, damaged.LimbLoss);
        Assert.Equal(1f, damaged.Breakage);
        Assert.Contains("left-wing=0.00", health.LimbHealthSummary);
    }

    [Fact]
    public void NativeLimbImpactLearnsTheThreatWithoutApplyingAnotherHealthHit()
    {
        var root = new GameObject("Fly");
        var memory = root.AddComponent<FlyExperienceMemory>();
        var health = root.AddComponent<FlyHealth>();
        health.Initialize();
        var leg = AddNativeLimb(root, "Front left leg", 100f, 100f);
        health.BindRegion(FlyHealthRegion.FrontLeftLeg, leg);
        var relay = leg.gameObject.GetComponent<FlyLimbDamageRelay>();
        var hazard = new GameObject("Saw");
        var collider = hazard.AddComponent<Collider2D>();
        var callback = typeof(FlyLimbDamageRelay).GetMethod("OnCollisionEnter2D", BindingFlags.NonPublic | BindingFlags.Instance);

        callback.Invoke(relay, [new Collision2D { collider = collider, relativeVelocity = new Vector2(8f, 0f) }]);

        Assert.Equal(100f, leg.Health);
        Assert.Equal(1, memory.HarmCountFor(hazard));
        Assert.Equal("Saw", memory.LastHarmfulObject);
    }

    private static LimbBehaviour AddNativeLimb(GameObject root, string name, float health, float initialHealth)
    {
        var part = new GameObject(name);
        part.transform.SetParent(root.transform);
        var limb = part.AddComponent<LimbBehaviour>();
        limb.Health = health;
        limb.InitialHealth = initialHealth;
        limb.PhysicalBehaviour = part.AddComponent<PhysicalBehaviour>();
        limb.CirculationBehaviour = part.AddComponent<CirculationBehaviour>();
        return limb;
    }
}
