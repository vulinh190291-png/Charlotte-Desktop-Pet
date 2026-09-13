using Charlotte.Core.Geometry;
namespace Charlotte.Tests;
public class AlphaMaskTests
{
    [Fact]
    public void Threshold_and_bounds_filter_noninteractive_pixels()
    {
        byte[] pixels = [0,0,0,0, 0,0,0,15, 0,0,0,16, 0,0,0,255];
        var mask = AlphaMask.Create(pixels, 4, 1, 16, 16);
        Assert.False(mask.Contains(0,0)); Assert.False(mask.Contains(1,0));
        Assert.True(mask.Contains(2,0)); Assert.True(mask.Contains(3,0));
        Assert.False(mask.Contains(-1,0)); Assert.False(mask.Contains(4,0));
    }
    [Fact]
    public void Row_padding_is_not_a_pixel()
    {
        var mask = AlphaMask.Create([0,0,0,255, 0,0,0,0, 0,0,0,16, 0,0,0,0],1,2,8,16);
        Assert.True(mask.Contains(0,1));
    }
}
