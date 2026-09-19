using ShadowNineX.PersonConnectome.Core;
using Xunit;

namespace ShadowNineX.PersonConnectome.AdapterTests;

public sealed class FlyGaitTests
{
    private static (float X, float Y) Foot(int leg, float phase, float drive = 1f, float flight = 0f, float groom = 0f)
    {
        FlyGait.Pose(leg, phase, drive, flight, groom, out var upper, out var lower);
        var a = (upper - 90f) * MathF.PI / 180f;
        var b = (lower - 90f) * MathF.PI / 180f;
        return (MathF.Cos(a) * FlyGait.UpperLength + MathF.Cos(b) * FlyGait.LowerLength,
            MathF.Sin(a) * FlyGait.UpperLength + MathF.Sin(b) * FlyGait.LowerLength);
    }

    [Fact]
    public void IkReachesSixDistinctFeetAtTheSameGroundHeight()
    {
        for (var leg = 0; leg < 6; leg++)
        {
            var foot = Foot(leg, 0f, 0f);
            var expectedX = (leg / 2 == 0 ? -.18f : leg / 2 == 1 ? .015f : .20f) - (leg % 2 == 0 ? .025f : 0f);
            Assert.Equal(expectedX, foot.X, 4);
            var hipY = leg % 2 == 0 ? -.055f : -.065f;
            Assert.Equal(-.465f, hipY + foot.Y, 4);
        }
    }

    [Fact]
    public void OppositeTripodsAlternateSwingAndStanceWithoutStretchingSegments()
    {
        foreach (var phase in new[] { .3f, .8f })
        {
            var swinging = new List<int>();
            for (var leg = 0; leg < 6; leg++)
            {
                var foot = Foot(leg, phase);
                Assert.InRange(MathF.Sqrt(foot.X * foot.X + foot.Y * foot.Y), .05f, FlyGait.UpperLength + FlyGait.LowerLength);
                var ground = leg % 2 == 0 ? -.41f : -.40f;
                if (foot.Y > ground + .01f) swinging.Add(leg);
                else Assert.Equal(ground, foot.Y, 4);
            }
            Assert.Equal(phase < .5f ? new[] { 0, 3, 4 } : new[] { 1, 2, 5 }, swinging);
        }
    }

    [Fact]
    public void StanceSweepsBackwardsAndReversesForBackwardWalking()
    {
        Assert.True(Foot(1, .5f).X > Foot(1, .1f).X);
        Assert.True(Foot(1, .5f, -1f).X < Foot(1, .1f, -1f).X);
        Assert.Equal(Foot(1, .1f, 0f), Foot(1, .8f, 0f));
    }

    [Fact]
    public void FlightTucksAllLegsAndGroomingTargetsTheHeadOnlyWithFrontLegs()
    {
        for (var leg = 0; leg < 6; leg++)
        {
            Assert.Equal(-.22f, Foot(leg, .2f, flight: 1f).Y, 4);
            var groom = Foot(leg, .2f, 0f, groom: 1f);
            if (leg < 2) { Assert.Equal(-.28f, groom.X, 4); Assert.Equal(-.04f, groom.Y, 4); }
            else Assert.Equal(Foot(leg, .2f, 0f), groom);
        }
    }

    [Fact]
    public void GroomingRequiresStrongIdleIntentInsteadOfSummedBackgroundActivity()
    {
        Assert.Equal(0f, FlyGait.GroomingIntent(.12f, .12f, .12f, .12f, 0f, 0f, false));
        Assert.Equal(0f, FlyGait.GroomingIntent(1f, 0f, 0f, 0f, .2f, 0f, false));
        Assert.Equal(0f, FlyGait.GroomingIntent(1f, 0f, 0f, 0f, 0f, .2f, false));
        Assert.Equal(0f, FlyGait.GroomingIntent(1f, 0f, 0f, 0f, 0f, 0f, true));
        Assert.Equal(1f, FlyGait.GroomingIntent(1f, 0f, 0f, 0f, 0f, 0f, false));
    }

    [Fact]
    public void InvalidInputsRemainFiniteAndCycleWrapIsContinuous()
    {
        for (var leg = 0; leg < 6; leg++)
        {
            var invalid = Foot(leg, float.NaN, float.PositiveInfinity, float.NaN, float.NegativeInfinity);
            Assert.True(float.IsFinite(invalid.X) && float.IsFinite(invalid.Y));
            var before = Foot(leg, .999999f);
            var after = Foot(leg, 0f);
            Assert.InRange(MathF.Abs(before.X - after.X) + MathF.Abs(before.Y - after.Y), 0f, .0001f);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => Foot(6, 0f));
    }

    [Fact]
    public void TinyFlyPartsReceiveBreakableForceCapsWithoutWeakeningBones()
    {
        Assert.Equal(8f, FlyGait.BreakForceCap(.002f, 1f), 4);
        Assert.Equal(8f, FlyGait.BreakForceCap(.006f, 1f), 4);
        Assert.Equal(35f, FlyGait.BreakForceCap(.035f, 1f), 4);
        Assert.Equal(8.75f, FlyGait.BreakForceCap(.035f, 0f), 4);
        Assert.Equal(2f, FlyGait.BreakForceCap(float.NaN, float.NaN), 4);
    }

    [Fact]
    public void AsymmetricFlyBoundsStayCenteredForBothSpawnFacings()
    {
        Assert.Equal(-.2f, FlyGait.SpawnCenterOffset(-.4f, .8f), 4);
        Assert.Equal(.2f, FlyGait.SpawnCenterOffset(-.8f, .4f), 4);
        Assert.Equal(0f, FlyGait.SpawnCenterOffset(-.6f, .6f), 4);
        Assert.Equal(0f, FlyGait.SpawnCenterOffset(2f, -2f), 4);
    }

    [Fact]
    public void WingsDetachWithoutInheritedLooseTissueTethers()
    {
        Assert.False(FlyGait.RetainLooseTissue(isWing: true));
        Assert.True(FlyGait.RetainLooseTissue(isWing: false));
    }
}
