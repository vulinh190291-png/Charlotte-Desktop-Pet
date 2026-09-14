using Charlotte.Core.Geometry;
using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class FullscreenWindowPolicyTests
{
    private static readonly PxRect Monitor=new(-1920,0,1920,1080);

    [Fact]
    public void A_window_covering_only_the_work_area_is_not_fullscreen()
    {
        var workAreaOnly=new PxRect(-1920,0,1920,1040);

        Assert.False(FullscreenWindowPolicy.IsFullscreen(Monitor,workAreaOnly,true,false,false));
    }

    [Fact]
    public void A_visible_foreground_window_covering_the_monitor_is_fullscreen()
        => Assert.True(FullscreenWindowPolicy.IsFullscreen(Monitor,Monitor,true,false,false));

    [Theory]
    [InlineData(false,false,false)]
    [InlineData(true,true,false)]
    [InlineData(true,false,true)]
    public void Hidden_minimized_shell_or_owned_windows_are_excluded(bool visible,bool minimized,bool excluded)
        => Assert.False(FullscreenWindowPolicy.IsFullscreen(Monitor,Monitor,visible,minimized,excluded));
}
