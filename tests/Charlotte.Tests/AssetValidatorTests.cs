using Charlotte.Core.Animation;
using Charlotte.Windows.Assets;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Charlotte.Tests;

public class AssetValidatorTests
{
    [Theory]
    [InlineData("Idle",8)]
    [InlineData("Walk",8)]
    [InlineData("Rest",8)]
    [InlineData("SleepEnter",5)]
    [InlineData("Sleep",8)]
    [InlineData("SleepExit",5)]
    [InlineData("ClickSoft",8)]
    [InlineData("ClickAnnoyed",10)]
    [InlineData("ClickWarning",12)]
    [InlineData("DragStart",4)]
    [InlineData("DragHold",4)]
    [InlineData("DragRelease",4)]
    [InlineData("Battle",13)]
    [InlineData("Victory",10)]
    public void Required_frame_count_is_enforced(string name,int count)
    {
        var id=Enum.Parse<AnimationId>(name);
        var valid=Enumerable.Range(1,count).Select(i=>new FrameSpec($"{id}/{i:00}.png",100)).ToArray();
        Assert.DoesNotContain(AssetValidator.CheckClip(id,valid),x=>x.Code=="frame-count");
        Assert.Contains(AssetValidator.CheckClip(id,valid[..^1]),x=>x.Code=="frame-count");
    }

    [Fact]
    public void Unsafe_or_nonpositive_frame_is_rejected()
    {
        var frames=Enumerable.Range(1,8).Select(i=>new FrameSpec(i==1?"../escape.png":$"idle/{i}.png",i==2?0:100)).ToArray();
        var issues=AssetValidator.CheckClip(AnimationId.Idle,frames);
        Assert.Contains(issues,x=>x.Code=="unsafe-path");
        Assert.Contains(issues,x=>x.Code=="duration");
    }

    [Fact]
    public void Battle_and_victory_duration_ranges_are_enforced()
    {
        Assert.Contains(AssetValidator.CheckClip(AnimationId.Battle,Enumerable.Range(0,13).Select(i=>new FrameSpec($"b/{i}.png",200)).ToArray()),x=>x.Code=="total-duration");
        Assert.Contains(AssetValidator.CheckClip(AnimationId.Victory,Enumerable.Range(0,10).Select(i=>new FrameSpec($"v/{i}.png",50)).ToArray()),x=>x.Code=="total-duration");
    }

    [Fact]
    public void Frame_with_wrong_canvas_is_rejected()
    {
        var path=WritePng(PixelFormats.Bgra32,2,2,new byte[16],8);
        try
        {
            Assert.Contains(AssetValidator.CheckFrame(path,480,600,16),x=>x.Code=="canvas");
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!,true); }
    }

    [Fact]
    public void Frame_without_alpha_channel_is_rejected()
    {
        var path=WritePng(PixelFormats.Bgr24,480,600,new byte[480*600*3],480*3);
        try
        {
            Assert.Contains(AssetValidator.CheckFrame(path,480,600,16),x=>x.Code=="pixel-format");
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!,true); }
    }

    [Fact]
    public void Fully_opaque_frame_is_rejected()
    {
        var pixels=new byte[480*600*4];
        for(var i=3;i<pixels.Length;i+=4) pixels[i]=255;
        var path=WritePng(PixelFormats.Bgra32,480,600,pixels,480*4);
        try
        {
            Assert.Contains(AssetValidator.CheckFrame(path,480,600,16),x=>x.Code=="alpha-content");
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!,true); }
    }

    [Fact]
    public void Reparse_point_cannot_escape_the_asset_root()
    {
        var root=FindProjectRoot();
        var directory=Path.Combine(root,"artifacts","test-data",Guid.NewGuid().ToString("N"));
        var assets=Path.Combine(directory,"assets");
        var outside=Path.Combine(directory,"outside");
        var linked=Path.Combine(assets,"linked");
        Directory.CreateDirectory(assets); Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside,"frame.png"),"not-an-image");
        using var junction=Process.Start(new ProcessStartInfo("cmd.exe",$"/d /c mklink /J \"{linked}\" \"{outside}\"")
        { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true })!;
        junction.WaitForExit();
        Assert.Equal(0,junction.ExitCode);
        try
        {
            Assert.False(AssetValidator.IsSafeAssetPath(assets,Path.Combine(linked,"frame.png")));
        }
        finally
        {
            if(Directory.Exists(linked)) Directory.Delete(linked,false);
            Directory.Delete(directory,true);
        }
    }

    private static string WritePng(PixelFormat format,int width,int height,byte[] pixels,int stride)
    {
        var root=FindProjectRoot();
        var directory=Path.Combine(root,"artifacts","test-data",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"frame.png");
        var source=BitmapSource.Create(width,height,96,96,format,null,pixels,stride);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream=File.Create(path); encoder.Save(stream);
        return path;
    }

    private static string FindProjectRoot()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"Charlotte.sln"))) directory=directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException();
    }
}
