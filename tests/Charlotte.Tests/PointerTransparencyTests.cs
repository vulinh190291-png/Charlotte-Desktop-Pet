using Charlotte.Core.Geometry;

namespace Charlotte.Tests;

public sealed class PointerTransparencyTests
{
    [Fact]
    public void Transparent_and_opaque_pixels_are_mapped_from_physical_coordinates()
    {
        var mask=AlphaMask.Create([0,0,0,0, 0,0,0,255],2,1,8,16);
        var bounds=new PxRect(100,200,4,2);

        Assert.True(PointerTransparency.ShouldClickThrough(mask,bounds,new(100.5,200.5),2,false));
        Assert.False(PointerTransparency.ShouldClickThrough(mask,bounds,new(102.5,200.5),2,false));
    }

    [Fact]
    public void Mouse_capture_keeps_the_window_interactive_during_drag()
    {
        var mask=AlphaMask.Create([0,0,0,0],1,1,4,16);

        Assert.False(PointerTransparency.ShouldClickThrough(mask,new(0,0,1,1),new(0,0),1,true));
    }

    [Theory]
    [InlineData(-1,0)]
    [InlineData(1,0)]
    [InlineData(0,-1)]
    [InlineData(0,1)]
    public void Pointer_outside_the_window_keeps_a_safe_interactive_state(double x,double y)
    {
        var mask=AlphaMask.Create([0,0,0,0],1,1,4,16);

        Assert.False(PointerTransparency.ShouldClickThrough(mask,new(0,0,1,1),new(x,y),1,false));
    }

    [Fact]
    public void Missing_mask_keeps_the_window_interactive()
        => Assert.False(PointerTransparency.ShouldClickThrough(null,new(0,0,1,1),new(0,0),1,false));

    [Fact]
    public void Scaled_mask_maps_by_window_extent_instead_of_assuming_one_dip_per_pixel()
    {
        var mask=AlphaMask.Create([0,0,0,0, 0,0,0,0, 0,0,0,255],3,1,12,16);
        var bounds=new PxRect(10,20,6,2);

        Assert.False(PointerTransparency.ShouldClickThrough(mask,bounds,new(15.5,20.5),1,false));
    }
}
