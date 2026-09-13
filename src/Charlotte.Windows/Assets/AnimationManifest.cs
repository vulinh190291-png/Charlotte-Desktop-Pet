using Charlotte.Core.Animation;
using Charlotte.Core.Geometry;
using System.IO;
using System.Text.Json;

namespace Charlotte.Windows.Assets;

public sealed record AnimationAssets(AnimationCatalog Catalog, PxSize LogicalCanvas, PxSize DisplaySizeDip, PxPoint FootAnchor, byte AlphaThreshold);

public static class ManifestLoader
{
    public static AnimationAssets Load(string assetsRoot, string manifestPath)
    {
        var root=Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var dto=JsonSerializer.Deserialize<ManifestDto>(File.ReadAllText(manifestPath),new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("动画清单为空。");
        if(dto.SchemaVersion!=1) throw new InvalidDataException($"不支持动画清单版本 {dto.SchemaVersion}。");
        var clips=new List<AnimationClip>();
        foreach(var item in dto.Clips)
        {
            if(!Enum.TryParse<AnimationId>(item.Id,true,out var id) || !Enum.TryParse<AnimationId>(item.ReturnTo,true,out var back))
                throw new InvalidDataException($"未知动画 {item.Id}。");
            var relative=item.Frames.Select(x=>new FrameSpec(x.Path,x.DurationMs)).ToArray();
            var issues=AssetValidator.CheckClip(id,relative);
            if(issues.Any(x=>x.Severity=="error")) throw new InvalidDataException(string.Join("; ",issues.Select(x=>x.Message)));
            var resolved=relative.Select(x=>
            {
                var path=Path.GetFullPath(Path.Combine(root,x.Path));
                if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) throw new InvalidDataException($"资源不存在或越界：{x.Path}");
                return x with { Path=path };
            }).ToArray();
            clips.Add(new(id,resolved,item.Loop,item.Interruptible,back,item.AllowMirror));
        }
        foreach(var id in Enum.GetValues<AnimationId>()) if(clips.All(x=>x.Id!=id)) throw new InvalidDataException($"清单缺少 {id}。");
        return new(new(clips),new(dto.LogicalCanvas.Width,dto.LogicalCanvas.Height),new(dto.DisplaySizeDip.Width,dto.DisplaySizeDip.Height),new(dto.FootAnchor.X,dto.FootAnchor.Y),dto.AlphaThreshold);
    }

    private sealed record SizeDto(double Width,double Height);
    private sealed record PointDto(double X,double Y);
    private sealed record FrameDto(string Path,int DurationMs);
    private sealed record ClipDto(string Id,bool Loop,bool Interruptible,string ReturnTo,bool AllowMirror,List<FrameDto> Frames);
    private sealed record ManifestDto(int SchemaVersion,SizeDto LogicalCanvas,SizeDto DisplaySizeDip,PointDto FootAnchor,byte AlphaThreshold,List<ClipDto> Clips);
}
