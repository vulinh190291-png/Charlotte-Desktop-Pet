using Charlotte.Windows.Assets;

namespace Charlotte.Tests;

public class FrameCacheTests
{
    [Fact]
    public async Task Decoded_frame_is_frozen_scaled_and_reused()
    {
        var root=FindProjectRoot(); var path=System.IO.Path.Combine(root,"assets","character","generated","idle","01.png");
        var cache=new FrameCache();
        var first=await cache.GetAsync(path,240,default); var second=await cache.GetAsync(path,240,default);
        Assert.True(first.IsFrozen); Assert.Equal(240,first.PixelWidth); Assert.Same(first,second);
    }

    [Fact]
    public async Task Builtin_idle_has_a_transparent_background_and_visible_pixels()
    {
        var bitmap=await new FrameCache().GetAsync("builtin:idle",240,default);
        var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];
        bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);

        Assert.True(bitmap.IsFrozen);
        Assert.Equal(240,bitmap.PixelWidth);
        Assert.Equal(300,bitmap.PixelHeight);
        Assert.Contains(Enumerable.Range(0,bitmap.PixelWidth*bitmap.PixelHeight),i=>pixels[i*4+3]==0);
        Assert.Contains(Enumerable.Range(0,bitmap.PixelWidth*bitmap.PixelHeight),i=>pixels[i*4+3]==255);
    }

    [Fact]
    public async Task Least_recently_used_frame_is_evicted_when_the_budget_is_exceeded()
    {
        var cache=new FrameCache(300_000);
        var first=await cache.GetAsync("builtin:idle",240,default);
        await cache.GetAsync("builtin:idle",200,default);

        var decodedAgain=await cache.GetAsync("builtin:idle",240,default);

        Assert.NotSame(first,decodedAgain);
    }

    [Fact]
    public async Task Frame_larger_than_the_budget_is_rejected()
    {
        var cache=new FrameCache(1_000);

        await Assert.ThrowsAsync<FrameBudgetExceededException>(
            ()=>cache.GetAsync("builtin:idle",240,default));
    }
    private static string FindProjectRoot()
    {
        var directory=new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName,"Charlotte.sln"))) directory=directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException();
    }
}
