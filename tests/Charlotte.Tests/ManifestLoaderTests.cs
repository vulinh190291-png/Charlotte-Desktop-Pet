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
        Assert.Equal("placeholder",assets.AssetStage);
    }

    [Fact]
    public void Malformed_manifest_uses_the_builtin_idle_fallback()
    {
        var root=FindProjectRoot();
        var manifest=WriteManifest(root,"{broken");
        try
        {
            var loaded=ManifestLoader.LoadResilient(Path.Combine(root,"assets"),manifest);

            Assert.StartsWith("builtin:",loaded.Assets.Catalog.Get(AnimationId.Idle).Frames[0].Path);
            Assert.Equal(loaded.Assets.Catalog.Get(AnimationId.Idle),loaded.Assets.Catalog.Get(AnimationId.Battle));
            Assert.Contains(loaded.Issues,x=>x.Code=="manifest-fallback");
        }
        finally { Directory.Delete(Path.GetDirectoryName(manifest)!,true); }
    }

    [Fact]
    public void Missing_battle_frame_disables_only_battle()
    {
        var root=FindProjectRoot();
        var source=File.ReadAllText(Path.Combine(root,"config","animations.json"));
        var manifest=WriteManifest(root,source.Replace(
            "character/generated/battle/01.png","character/generated/battle/missing.png"));
        try
        {
            var loaded=ManifestLoader.LoadResilient(Path.Combine(root,"assets"),manifest);

            Assert.Equal(loaded.Assets.Catalog.Get(AnimationId.Idle),loaded.Assets.Catalog.Get(AnimationId.Battle));
            Assert.Equal(10,loaded.Assets.Catalog.Get(AnimationId.Victory).Frames.Count);
            Assert.Contains(loaded.Issues,x=>x.Code=="clip-disabled" && x.Message.Contains("Battle"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(manifest)!,true); }
    }

    [Fact]
    public void Foot_anchor_outside_the_canvas_is_rejected()
    {
        var root=FindProjectRoot();
        var source=File.ReadAllText(Path.Combine(root,"config","animations.json"));
        var manifest=WriteManifest(root,source.Replace("\"y\": 560","\"y\": 601"));
        try
        {
            Assert.Throws<InvalidDataException>(()=>ManifestLoader.Load(Path.Combine(root,"assets"),manifest));
        }
        finally { Directory.Delete(Path.GetDirectoryName(manifest)!,true); }
    }

    [Fact]
    public void Effect_counts_must_match_the_character_contract()
    {
        var root=FindProjectRoot();
        var source=File.ReadAllText(Path.Combine(root,"config","animations.json"));
        var manifest=WriteManifest(root,source.Replace("\"roses\": 1","\"roses\": 2"));
        try
        {
            Assert.Throws<InvalidDataException>(()=>ManifestLoader.Load(Path.Combine(root,"assets"),manifest));
        }
        finally { Directory.Delete(Path.GetDirectoryName(manifest)!,true); }
    }

    private static string WriteManifest(string root,string content)
    {
        var directory=Path.Combine(root,"artifacts","test-data",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"animations.json");
        File.WriteAllText(path,content);
        return path;
    }

    private static string FindProjectRoot()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName,"Charlotte.sln"))) directory=directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Project root not found");
    }
}
