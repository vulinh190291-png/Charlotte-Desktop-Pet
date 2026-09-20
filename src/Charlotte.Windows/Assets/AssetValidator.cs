using Charlotte.Core.Animation;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Charlotte.Windows.Assets;

public static class AssetValidator
{
    public static bool IsSafeAssetPath(string assetsRoot,string candidatePath)
    {
        try
        {
            var root=Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            var candidate=Path.GetFullPath(candidatePath);
            var prefix=root+Path.DirectorySeparatorChar;
            if(!candidate.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) return false;
            var relative=Path.GetRelativePath(root,candidate);
            var current=root;
            foreach(var segment in relative.Split([Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar],StringSplitOptions.RemoveEmptyEntries))
            {
                current=Path.Combine(current,segment);
                if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) return false;
            }
            return true;
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public static IReadOnlyList<AssetIssue> CheckFrame(string path,int expectedWidth,int expectedHeight,byte alphaThreshold)
    {
        var issues=new List<AssetIssue>();
        try
        {
            using var stream=File.OpenRead(path);
            var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            var bitmap=decoder.Frames[0];
            if(bitmap.PixelWidth!=expectedWidth || bitmap.PixelHeight!=expectedHeight)
                issues.Add(new("canvas",$"{Path.GetFileName(path)} 画布尺寸不符合约束。"));
            if(bitmap.Format!=PixelFormats.Bgra32 && bitmap.Format!=PixelFormats.Pbgra32)
            {
                issues.Add(new("pixel-format",$"{Path.GetFileName(path)} 必须是 32 位 Alpha PNG。"));
                return issues;
            }
            var stride=bitmap.PixelWidth*4;
            var pixels=new byte[stride*bitmap.PixelHeight];
            bitmap.CopyPixels(pixels,stride,0);
            var hasTransparent=false;
            var hasVisible=false;
            for(var offset=3;offset<pixels.Length;offset+=4)
            {
                hasTransparent|=pixels[offset]<alphaThreshold;
                hasVisible|=pixels[offset]>=alphaThreshold;
                if(hasTransparent && hasVisible) break;
            }
            if(!hasTransparent || !hasVisible)
                issues.Add(new("alpha-content",$"{Path.GetFileName(path)} 必须同时包含透明背景和可见像素。"));
        }
        catch(Exception error) when(error is IOException or NotSupportedException or FileFormatException)
        {
            issues.Add(new("frame-decode",$"{Path.GetFileName(path)} 无法解码。"));
        }
        return issues;
    }

    public static IReadOnlyList<AssetIssue> CheckClip(AnimationId id, IReadOnlyList<FrameSpec> frames)
    {
        var issues=new List<AssetIssue>();
        var exact=id switch
        {
            AnimationId.Idle or AnimationId.Walk or AnimationId.Rest or AnimationId.Sleep or AnimationId.ClickSoft => 8,
            AnimationId.SleepEnter or AnimationId.SleepExit => 5,
            AnimationId.ClickAnnoyed or AnimationId.Victory => 10,
            AnimationId.ClickWarning => 12,
            AnimationId.Battle => 13,
            AnimationId.DragStart or AnimationId.DragHold or AnimationId.DragRelease => 4,
            _ => 0
        };
        if(exact>0 && frames.Count!=exact)
            issues.Add(new("frame-count",$"{id} 帧数不符合约束。"));
        foreach(var frame in frames)
        {
            if(frame.DurationMs<=0) issues.Add(new("duration",$"{frame.Path} 时长必须为正数。"));
            if(Path.IsPathRooted(frame.Path) || frame.Path.Replace('\\','/').Split('/').Any(x=>x==".."))
                issues.Add(new("unsafe-path",$"{frame.Path} 不在资源根目录内。"));
        }
        var duration=frames.Sum(x=>(long)x.DurationMs);
        if ((id==AnimationId.Battle && duration is <2300 or >2800) || (id==AnimationId.Victory && duration is <1800 or >2300))
            issues.Add(new("total-duration",$"{id} 总时长不符合约束。"));
        return issues;
    }
}
