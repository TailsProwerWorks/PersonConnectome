using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;
using Xunit;

namespace ShadowNineX.PersonConnectome.AdapterTests;

public sealed class PersonBehaviourTests
{
    private static readonly SensoryFrame Active = new()
    {
        HealthValid = true,
        Alive = true,
        ConsciousnessValid = true,
        Consciousness = 1f
    };

    [Fact]
    public void IdleInputRemainsNeutralAndReportsResting()
    {
        var mapper = new PeoplePlaygroundPersonMotorMapper();
        var output = mapper.Map(default, Active, .05f);

        Assert.Equal(default, output);
        Assert.Equal("Resting", mapper.Activity);
    }

    [Fact]
    public void GroomChannelsProduceDistinctNonGripIdlePostures()
    {
        var antenna = Map(new FlyMotorCommand { FlyGroomAntenna = 1f });
        var head = Map(new FlyMotorCommand { FlyGroomHead = 1f });
        var body = Map(new FlyMotorCommand { FlyGroomLeg = 1f, FlyGroomAbdomen = .5f });

        Assert.True(antenna.Head > head.Head);
        Assert.True(head.RightArm < antenna.RightArm);
        Assert.True(body.Core > 0f && body.RightArm > 0f);
        Assert.Equal(0f, antenna.LeftGrip);
        Assert.Equal(0f, head.RightGrip);
        Assert.Equal(0f, body.ReachGrab);
    }

    [Fact]
    public void FeedingNeedsBothNeuralRequestAndActualFoodContact()
    {
        var mapper = new PeoplePlaygroundPersonMotorMapper();
        var withoutContact = mapper.Map(new FlyMotorCommand { FlyFeed = 1f }, Active, .05f);
        Assert.Equal(default, withoutContact);
        Assert.Equal("Resting", mapper.Activity);

        var contact = Active;
        contact.FoodCuesValid = true;
        contact.FoodContactCue = 1f;
        var feeding = new PeoplePlaygroundPersonMotorMapper();
        var withContact = feeding.Map(new FlyMotorCommand { FlyFeed = 1f }, contact, .05f);
        Assert.True(withContact.Head > 0f && withContact.RightArm > 0f);
        Assert.Equal(0f, withContact.RightGrip);
        Assert.Equal(0f, withContact.ReachGrab);
        Assert.Equal("Feeding request", feeding.Activity);

        var foodOnly = new PeoplePlaygroundPersonMotorMapper().Map(default, contact, .05f);
        Assert.Equal(default, foodOnly);
    }

    [Fact]
    public void LocomotionEscapeAndSafetyPreemptIdleBehaviours()
    {
        var walking = new PeoplePlaygroundPersonMotorMapper();
        var walkingOutput = walking.Map(new FlyMotorCommand { FlyForward = 1f, FlyGroomHead = 1f, FlyFeed = 1f }, Active, .05f);
        Assert.True(walkingOutput.Walk > 0f);
        Assert.Equal("Walking", walking.Activity);

        var escaping = new PeoplePlaygroundPersonMotorMapper();
        escaping.Map(new FlyMotorCommand { FlyEscape = 1f, FlyGroomLeg = 1f }, Active, .05f);
        Assert.Equal("Escaping", escaping.Activity);

        var halted = new PeoplePlaygroundPersonMotorMapper();
        var haltOutput = halted.Map(new FlyMotorCommand { FlyHalt = .2f, FlyGroomHead = 1f }, Active, .05f);
        Assert.True(haltOutput.MotionStopRequested);
        Assert.Equal(0f, haltOutput.Head);
        Assert.Equal(0f, haltOutput.LeftArm);
        Assert.Equal(0f, haltOutput.Walk);
        Assert.Equal("Halting", halted.Activity);

        var suspended = Active;
        suspended.Consciousness = 0f;
        var unsafeMapper = new PeoplePlaygroundPersonMotorMapper();
        Assert.Equal(default, unsafeMapper.Map(new FlyMotorCommand { FlyGroomHead = 1f }, suspended, .05f));
        Assert.Equal("Suspended", unsafeMapper.Activity);
    }

    [Fact]
    public void GroomingRespectsTheSharedMotorRateLimit()
    {
        var mapper = new PeoplePlaygroundPersonMotorMapper();
        var output = mapper.Map(new FlyMotorCommand { FlyGroomAntenna = 1f }, Active, .01f);

        Assert.InRange(Math.Abs(output.LeftArm), 0f, .08001f);
        Assert.InRange(Math.Abs(output.RightArm), 0f, .08001f);
        Assert.InRange(Math.Abs(output.Head), 0f, .08001f);
    }

    [Fact]
    public void StationaryMotorProgramsPreemptIdleGestures()
    {
        var contact = Active;
        contact.FoodCuesValid = true;
        contact.FoodContactCue = 1f;
        FlyMotorCommand[] motions =
        [
            new() { FlyTakeoff = 1f }, new() { FlyLanding = 1f },
            new() { FlyJump = 1f }, new() { FlyYaw = 1f },
            new() { FlyFlightPower = 1f }, new() { FlyFlightYaw = 1f },
            new() { FlyWingMotor = 1f }, new() { FlyLegMotor = 1f }
        ];
        foreach (var motion in motions)
        {
            var expected = new PeoplePlaygroundPersonMotorMapper().Map(motion, contact, .25f);
            var combined = motion;
            combined.FlyGroomHead = combined.FlyFeed = 1f;
            var mapper = new PeoplePlaygroundPersonMotorMapper();
            Assert.Equal(expected, mapper.Map(combined, contact, .25f));
            Assert.Equal("Moving", mapper.Activity);
        }
    }

    private static PersonMotorCommand Map(FlyMotorCommand command) =>
        new PeoplePlaygroundPersonMotorMapper().Map(command, Active, .25f);
}
