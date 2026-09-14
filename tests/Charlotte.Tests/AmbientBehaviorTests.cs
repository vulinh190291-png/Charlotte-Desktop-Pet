using Charlotte.Core.Animation;

namespace Charlotte.Tests;

public class AmbientBehaviorTests
{
    [Fact]
    public void Idle_rhythm_holds_still_for_seventy_percent_of_each_cycle()
    {
        var movingDuration=TimeSpan.FromMilliseconds(300);

        Assert.Equal(TimeSpan.Zero,IdleRhythm.AnimationElapsed(TimeSpan.FromMilliseconds(699),movingDuration,.7));
        Assert.Equal(TimeSpan.FromMilliseconds(150),IdleRhythm.AnimationElapsed(TimeSpan.FromMilliseconds(850),movingDuration,.7));
        Assert.Equal(TimeSpan.Zero,IdleRhythm.AnimationElapsed(TimeSpan.FromMilliseconds(1000),movingDuration,.7));
    }

    [Theory]
    [InlineData(-.1)]
    [InlineData(1)]
    public void Idle_rhythm_rejects_invalid_still_ratios(double ratio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>IdleRhythm.AnimationElapsed(TimeSpan.Zero,TimeSpan.FromSeconds(1),ratio));
    }

    [Fact]
    public void Behavior_options_reject_an_inverted_walk_delay_range()
    {
        var options=BehaviorOptions.Default with
        {
            WalkDelayMinimum=TimeSpan.FromSeconds(41),
            WalkDelayMaximum=TimeSpan.FromSeconds(40)
        };

        Assert.Throws<ArgumentException>(options.Validate);
    }
}
