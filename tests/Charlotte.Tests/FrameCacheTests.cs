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
    private static string FindProjectRoot()
    {
        var directory=new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName,"Charlotte.sln"))) directory=directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException();
    }
}
