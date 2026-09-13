using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class VisibilityPolicyTests
{
    [Fact]
    public void Short_fullscreen_transition_does_not_hide_pet()
    {
        var policy=new VisibilityPolicy(TimeSpan.FromMilliseconds(200));

        Assert.False(policy.Observe(true,TimeSpan.Zero));
        Assert.False(policy.Observe(false,TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void Stable_fullscreen_hides_and_stable_exit_restores()
    {
        var policy=new VisibilityPolicy(TimeSpan.FromMilliseconds(200));

        policy.Observe(true,TimeSpan.Zero);
        Assert.False(policy.Observe(true,TimeSpan.FromMilliseconds(199)));
        Assert.True(policy.Observe(true,TimeSpan.FromMilliseconds(200)));
        policy.Observe(false,TimeSpan.FromMilliseconds(300));
        Assert.True(policy.Observe(false,TimeSpan.FromMilliseconds(499)));
        Assert.False(policy.Observe(false,TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void Repeated_stable_observation_does_not_restart_debounce()
    {
        var policy=new VisibilityPolicy(TimeSpan.FromMilliseconds(200));

        policy.Observe(true,TimeSpan.Zero);
        policy.Observe(true,TimeSpan.FromMilliseconds(100));

        Assert.True(policy.Observe(true,TimeSpan.FromMilliseconds(200)));
    }
}
