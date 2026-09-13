using Charlotte.Core.Geometry;

namespace Charlotte.Tests;

public class DragGestureTests
{
    [Fact]
    public void Event_screen_point_drives_origin_without_sampling_global_cursor()
    {
        var origin = DragGesture.WindowOrigin(new PxPoint(1030, 1053), new PxPoint(80, 100), 1.5);
        Assert.Equal(new PxPoint(910, 903), origin);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(5, 2, true)]
    [InlineData(2, 5, true)]
    public void Either_axis_can_start_drag(double x, double y, bool expected)
        => Assert.Equal(expected, DragGesture.HasCrossedThreshold(new(0, 0), new(x, y), 4, 4));
}
