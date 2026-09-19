using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;
using UnityEngine;
using Xunit;

namespace ShadowNineX.PersonConnectome.AdapterTests;

public sealed class FlyArticulatedAdapterTests
{
    private sealed class PhysicalRig : MonoBehaviour, IFlyPhysicalBodyRig
    {
        public bool IsUsable => true;
        public bool IsGrounded { get; set; }
        public float FlightCapacity { get; set; } = 1f;
        public bool Released;
        public int TurnCount;
        public FlyMotorCommand Last;
        public float FlightDeltaX, FlightDeltaY;
        public void Apply(FlyMotorCommand command, float elapsedSeconds) { Last = command; Released = false; }
        public void ApplyFlightVelocityChange(float deltaX, float deltaY)
        {
            FlightDeltaX += deltaX;
            FlightDeltaY += deltaY;
        }
        public bool TryTurnAround() { TurnCount++; return true; }
        public void Release() => Released = true;
    }

    [Fact]
    public void BackwardIntentTurnsOnceThenDrivesTheNormalForwardTripod()
    {
        var root = new GameObject("Thorax");
        root.AddComponent<Rigidbody2D>();
        var rig = root.AddComponent<PhysicalRig>();
        var adapter = new PeoplePlaygroundFlyAdapter(root);
        var backward = new FlyMotorCommand { FlyBackward = 1f };

        adapter.Apply(backward, false, 0f, 0f, .02f);
        adapter.Apply(backward, false, 0f, 0f, .02f);

        Assert.Equal(1, rig.TurnCount);
        Assert.Equal(1f, rig.Last.FlyForward);
        Assert.Equal(0f, rig.Last.FlyBackward);

        adapter.Apply(default, false, 0f, 0f, .02f);
        adapter.Apply(backward, false, 0f, 0f, .02f);
        Assert.Equal(2, rig.TurnCount);
    }

    [Fact]
    public void BackgroundBackwardActivityAndHaltCannotTurnTheBody()
    {
        var root = new GameObject("Thorax");
        root.AddComponent<Rigidbody2D>();
        var rig = root.AddComponent<PhysicalRig>();
        var adapter = new PeoplePlaygroundFlyAdapter(root);

        adapter.Apply(new FlyMotorCommand { FlyBackward = .29f }, false, 0f, 0f, .02f);
        adapter.Apply(new FlyMotorCommand { FlyBackward = 1f, FlyHalt = .2f }, false, 0f, 0f, .02f);

        Assert.Equal(0, rig.TurnCount);
    }

    [Fact]
    public void ArticulatedWalkingCommandsReachLegsWithoutSlidingTheThorax()
    {
        var root = new GameObject("Thorax");
        var body = root.AddComponent<Rigidbody2D>();
        var rig = root.AddComponent<PhysicalRig>();
        var adapter = new PeoplePlaygroundFlyAdapter(root);
        body.velocity = new Vector2(.5f, -1f);
        body.angularVelocity = 12f;
        adapter.Apply(new FlyMotorCommand { FlyForward = 1f, FlyYaw = 1f }, false, 0f, 0f, .02f);
        Assert.Equal(.5f, body.velocity.x);
        Assert.Equal(-1f, body.velocity.y);
        Assert.Equal(12f, body.angularVelocity);
        Assert.Equal(1f, rig.Last.FlyForward);
        adapter.Stop();
        Assert.True(rig.Released);
    }

    [Fact]
    public void MissingWingCancelsHeldFlightAndSuspensionReleasesTheJoints()
    {
        var root = new GameObject("Thorax");
        var body = root.AddComponent<Rigidbody2D>();
        var rig = root.AddComponent<PhysicalRig>();
        var adapter = new PeoplePlaygroundFlyAdapter(root);
        adapter.Apply(new FlyMotorCommand { FlyTakeoff = 1f }, false, 0f, 0f, .02f);
        Assert.True(body.velocity.y > 0f);
        Assert.True(rig.FlightDeltaY > 0f);
        rig.FlightCapacity = 0f;
        body.velocity = default;
        adapter.Apply(new FlyMotorCommand { FlyWingMotor = 1f, FlyTakeoff = 1f }, false, 0f, 0f, .02f);
        Assert.Equal(0f, body.velocity.y);
        Assert.NotEqual("Flying", adapter.Activity);
        adapter.Suspend();
        Assert.True(rig.Released);
    }

    [Fact]
    public void SiblingBodyPartsAreExcludedFromTheFlyVisualField()
    {
        var person = new GameObject("Fly");
        person.AddComponent<PersonBehaviour>();
        var root = new GameObject("Thorax");
        root.transform.SetParent(person.transform);
        root.AddComponent<Rigidbody2D>();
        var head = new GameObject("Head");
        head.transform.SetParent(person.transform);
        head.transform.position = new Vector3(-.3f, 0f, 0f);
        Physics2D.Hits = [head.AddComponent<Collider2D>()];
        Physics2D.LinecastHits = [];
        Physics2D.LinecastHandler = null;
        var frame = new PeoplePlaygroundFlyAdapter(root).Read();
        Assert.Equal(0f, frame.Nearby);
        Assert.False(frame.ViewFront.Observed);
        Physics2D.Hits = [];
    }
}
