using System.Reflection;
using ShadowNineX.PersonConnectome;
using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;
using UnityEngine;
using Xunit;

namespace ShadowNineX.PersonConnectome.AdapterTests;

public sealed class FlyBodyRigTests
{
    [Fact]
    public void RestPoseUsesLoadBearingHealthScaledLegTorque()
    {
        var thorax = Limb("Thorax", null);
        var health = thorax.gameObject.AddComponent<FlyHealth>();
        health.Initialize();
        var rig = thorax.gameObject.AddComponent<FlyBodyRig>();
        var uppers = new LimbBehaviour[6];
        var lowers = new LimbBehaviour[6];
        for (var i = 0; i < 6; i++)
        {
            uppers[i] = Limb("Upper" + i, thorax.PhysicalBehaviour.rigidbody);
            lowers[i] = Limb("Lower" + i, uppers[i].PhysicalBehaviour.rigidbody);
            lowers[i].IsOnFloor = true;
        }

        rig.Initialize(thorax, uppers, lowers, []);
        rig.Apply(default, .02f);
        InvokeFixedUpdate(rig);

        Assert.All(uppers, limb =>
        {
            Assert.True(limb.Joint.useMotor);
            Assert.Equal(FlyGait.UpperJointTorque, limb.Joint.motor.maxMotorTorque, 4);
        });
        Assert.All(lowers, limb =>
        {
            Assert.True(limb.Joint.useMotor);
            Assert.Equal(FlyGait.LowerJointTorque, limb.Joint.motor.maxMotorTorque, 4);
        });

        lowers[0].Health = lowers[0].InitialHealth * .25f;
        InvokeFixedUpdate(rig);
        Assert.Equal(FlyGait.LowerJointTorque * .25f, lowers[0].Joint.motor.maxMotorTorque, 4);
    }

    [Fact]
    public void MirroredSpawnKeepsRestPoseAnglesInsteadOfDrivingLegsUpsideDown()
    {
        const float thoraxRotation = 22f;
        var thorax = Limb("Thorax", null);
        thorax.transform.lossyScale = new Vector3(-1f, 1f, 1f);
        thorax.transform.RotationDegrees = thoraxRotation;
        thorax.PhysicalBehaviour.rigidbody.rotation = thoraxRotation;
        var health = thorax.gameObject.AddComponent<FlyHealth>();
        health.Initialize();
        var rig = thorax.gameObject.AddComponent<FlyBodyRig>();
        var uppers = new LimbBehaviour[6];
        var lowers = new LimbBehaviour[6];
        for (var i = 0; i < 6; i++)
        {
            FlyGait.Pose(i, 0f, 0f, 0f, 0f, out var upperAngle, out var lowerAngle);
            uppers[i] = Limb("Upper" + i, thorax.PhysicalBehaviour.rigidbody);
            lowers[i] = Limb("Lower" + i, uppers[i].PhysicalBehaviour.rigidbody);
            lowers[i].IsOnFloor = true;
            uppers[i].PhysicalBehaviour.rigidbody.rotation = thoraxRotation + upperAngle;
            lowers[i].PhysicalBehaviour.rigidbody.rotation = thoraxRotation + lowerAngle;
        }

        rig.Initialize(thorax, uppers, lowers, []);
        rig.Apply(default, .02f);
        InvokeFixedUpdate(rig);

        Assert.All(uppers.Concat(lowers), limb =>
            Assert.Equal(0f, limb.Joint.motor.motorSpeed, 3));
    }

    [Fact]
    public void KneeTargetIsRelativeToItsFemurInsteadOfTheThorax()
    {
        var thorax = Limb("Thorax", null);
        var health = thorax.gameObject.AddComponent<FlyHealth>();
        health.Initialize();
        var uppers = new LimbBehaviour[6];
        var lowers = new LimbBehaviour[6];
        for (var i = 0; i < 6; i++)
        {
            FlyGait.Pose(i, 0f, 0f, 0f, 0f, out var upperAngle, out var lowerAngle);
            uppers[i] = Limb("Upper" + i, thorax.PhysicalBehaviour.rigidbody);
            lowers[i] = Limb("Lower" + i, uppers[i].PhysicalBehaviour.rigidbody);
            lowers[i].IsOnFloor = true;
            // Carry the same 20-degree world offset through both segments. The
            // knee is correct relative to the femur and must not be driven
            // merely because the whole leg is offset from the thorax.
            uppers[i].PhysicalBehaviour.rigidbody.rotation = upperAngle + 20f;
            lowers[i].PhysicalBehaviour.rigidbody.rotation = lowerAngle + 20f;
        }

        var rig = thorax.gameObject.AddComponent<FlyBodyRig>();
        rig.Initialize(thorax, uppers, lowers, []);
        rig.Apply(default, .02f);
        InvokeFixedUpdate(rig);

        Assert.All(lowers, limb => Assert.Equal(0f, limb.Joint.motor.motorSpeed, 3));
    }

    [Fact]
    public void TurnAroundReflectsTheCompletePersonRootExactlyOnce()
    {
        var thorax = Limb("Thorax", null);
        thorax.transform.localScale = new Vector3(.75f, 1.1f, 1f);
        var rig = thorax.gameObject.AddComponent<FlyBodyRig>();
        rig.Initialize(thorax, [], [], []);

        Assert.True(rig.TryTurnAround());
        Assert.Equal(-.75f, thorax.Person.transform.localScale.x, 4);
        Assert.Equal(1.1f, thorax.Person.transform.localScale.y, 4);
    }

    [Fact]
    public void FlightVelocityChangeLiftsEveryAttachedBodyPartButLeavesDetachedPartsAlone()
    {
        var thorax = Limb("Thorax", null);
        var attached = Limb("Attached abdomen", thorax.PhysicalBehaviour.rigidbody);
        var detached = Limb("Detached leg", thorax.PhysicalBehaviour.rigidbody);
        attached.Person = thorax.Person;
        detached.Person = thorax.Person;
        detached.IsDismembered = true;
        attached.PhysicalBehaviour.rigidbody.velocity = new Vector2(.1f, -.2f);
        detached.PhysicalBehaviour.rigidbody.velocity = new Vector2(.4f, -.5f);
        thorax.Person.Limbs = [thorax, attached, detached];
        var rig = thorax.gameObject.AddComponent<FlyBodyRig>();
        rig.Initialize(thorax, [], [], []);

        rig.ApplyFlightVelocityChange(.3f, .7f);

        Assert.Equal(0f, thorax.PhysicalBehaviour.rigidbody.velocity.x);
        Assert.Equal(0f, thorax.PhysicalBehaviour.rigidbody.velocity.y);
        Assert.Equal(.4f, attached.PhysicalBehaviour.rigidbody.velocity.x, 4);
        Assert.Equal(.5f, attached.PhysicalBehaviour.rigidbody.velocity.y, 4);
        Assert.Equal(.4f, detached.PhysicalBehaviour.rigidbody.velocity.x, 4);
        Assert.Equal(-.5f, detached.PhysicalBehaviour.rigidbody.velocity.y, 4);
    }

    [Fact]
    public void DetachedOrReleasedLegsCannotRetainMotorAuthority()
    {
        var thorax = Limb("Thorax", null);
        var health = thorax.gameObject.AddComponent<FlyHealth>();
        health.Initialize();
        var rig = thorax.gameObject.AddComponent<FlyBodyRig>();
        var uppers = new LimbBehaviour[6];
        var lowers = new LimbBehaviour[6];
        for (var i = 0; i < 6; i++)
        {
            uppers[i] = Limb("Upper" + i, thorax.PhysicalBehaviour.rigidbody);
            lowers[i] = Limb("Lower" + i, uppers[i].PhysicalBehaviour.rigidbody);
            lowers[i].IsOnFloor = true;
        }

        rig.Initialize(thorax, uppers, lowers, []);
        rig.Apply(default, .02f);
        InvokeFixedUpdate(rig);
        uppers[0].IsDismembered = true;
        InvokeFixedUpdate(rig);
        Assert.False(uppers[0].Joint.useMotor);
        Assert.False(lowers[0].Joint.useMotor);

        rig.Release();
        Assert.All(uppers.Concat(lowers), limb => Assert.False(limb.Joint.useMotor));
    }

    [Fact]
    public void WingsReleaseAtRestAndUseAFilteredStrokeDuringFlight()
    {
        var thorax = Limb("Thorax", null);
        var health = thorax.gameObject.AddComponent<FlyHealth>();
        health.Initialize();
        var wings = new[]
        {
            Limb("LeftWing", thorax.PhysicalBehaviour.rigidbody),
            Limb("RightWing", thorax.PhysicalBehaviour.rigidbody)
        };
        var uppers = new LimbBehaviour[6];
        var lowers = new LimbBehaviour[6];
        for (var i = 0; i < 6; i++)
        {
            uppers[i] = Limb("Upper" + i, thorax.PhysicalBehaviour.rigidbody);
            lowers[i] = Limb("Lower" + i, uppers[i].PhysicalBehaviour.rigidbody);
        }
        var rig = thorax.gameObject.AddComponent<FlyBodyRig>();
        rig.Initialize(thorax, uppers, lowers, wings);

        rig.Apply(default, .02f);
        InvokeFixedUpdate(rig);
        Assert.All(wings, wing => Assert.False(wing.Joint.useMotor));

        rig.Apply(new FlyMotorCommand { FlyFlightPower = 1f }, .02f);
        InvokeFixedUpdate(rig);
        Assert.All(wings, wing =>
        {
            Assert.True(wing.Joint.useMotor);
            Assert.Equal(FlyGait.WingJointTorque, wing.Joint.motor.maxMotorTorque, 4);
            Assert.InRange(Math.Abs(wing.Joint.motor.motorSpeed), 0f, 720f);
        });

        rig.Release();
        Assert.All(wings, wing => Assert.False(wing.Joint.useMotor));
    }

    [Fact]
    public void FlyFragilityCapsHingesWithoutChangingNativeBoneThreshold()
    {
        var limb = Limb("Damaged leg", null);
        limb.PhysicalBehaviour.rigidbody.mass = .006f;
        limb.BreakingThreshold = 3f;
        limb.Joint.breakForce = 500f;
        limb.Joint.breakTorque = 500f;
        var health = limb.gameObject.AddComponent<FlyHealth>();
        health.Initialize();
        var relay = limb.gameObject.AddComponent<FlyLimbDamageRelay>();
        relay.Bind(health, limb);

        InvokeFixedUpdate(relay);

        Assert.Equal(8f, limb.Joint.breakForce, 4);
        Assert.Equal(4f, limb.Joint.breakTorque, 4);
        Assert.Equal(3f, limb.BreakingThreshold);

        limb.Health = 0f;
        limb.Joint.breakForce = 500f;
        limb.Joint.breakTorque = 500f;
        InvokeFixedUpdate(relay);
        Assert.Equal(2f, limb.Joint.breakForce, 4);
        Assert.Equal(1f, limb.Joint.breakTorque, 4);
        Assert.Equal(3f, limb.BreakingThreshold);
    }

    private static LimbBehaviour Limb(string name, Rigidbody2D connectedBody)
    {
        var gameObject = new GameObject(name);
        var person = gameObject.AddComponent<PersonBehaviour>();
        var body = gameObject.AddComponent<Rigidbody2D>();
        var physical = gameObject.AddComponent<PhysicalBehaviour>();
        physical.rigidbody = body;
        var circulation = gameObject.AddComponent<CirculationBehaviour>();
        var limb = gameObject.AddComponent<LimbBehaviour>();
        limb.Person = person;
        limb.PhysicalBehaviour = physical;
        limb.CirculationBehaviour = circulation;
        limb.Joint = gameObject.AddComponent<HingeJoint2D>();
        limb.Joint.connectedBody = connectedBody ?? body;
        return limb;
    }

    private static void InvokeFixedUpdate(FlyBodyRig rig) =>
        typeof(FlyBodyRig).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(rig, null);

    private static void InvokeFixedUpdate(FlyLimbDamageRelay relay) =>
        typeof(FlyLimbDamageRelay).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(relay, null);
}
