using Charlotte.Core.Animation;
using Charlotte.Windows.Assets;
using System.IO;

namespace Charlotte.Tests;

public class ManifestLoaderTests
{
    [Fact]
    public void Project_manifest_loads_required_animation_contract()
    {
        var root=FindProjectRoot();
        var assets=ManifestLoader.Load(System.IO.Path.Combine(root,"assets"),System.IO.Path.Combine(root,"config","animations.json"));
        Assert.Equal(14,assets.Catalog.Get(AnimationId.Battle).Frames.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(900),assets.Catalog.Get(AnimationId.Victory).Duration);
        Assert.Equal(560,assets.FootAnchor.Y);
    }

    private static string FindProjectRoot()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName,"Charlotte.sln"))) directory=directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Project root not found");
    }
}
