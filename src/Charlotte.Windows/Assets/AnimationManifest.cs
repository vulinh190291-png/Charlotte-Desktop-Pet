using Charlotte.Core.Animation;
using Charlotte.Core.Geometry;
using System.IO;
using System.Text.Json;

namespace Charlotte.Windows.Assets;

public sealed record AnimationAssets(AnimationCatalog Catalog,PxSize LogicalCanvas,PxSize DisplaySizeDip,PxPoint FootAnchor,byte AlphaThreshold,string AssetStage,BehaviorOptions Behavior);
public sealed record AssetLoadResult(AnimationAssets Assets,IReadOnlyList<AssetIssue> Issues);

public static class ManifestLoader
{
    private static readonly AnimationId[] RequiredAnimationIds=
    [
        AnimationId.Idle,AnimationId.Rest,AnimationId.SleepEnter,AnimationId.Sleep,AnimationId.SleepExit,
        AnimationId.DragStart,AnimationId.DragHold,AnimationId.DragRelease,AnimationId.Battle,AnimationId.Victory
    ];

    public static AnimationAssets Load(string assetsRoot, string manifestPath)
    {
        var root=NormalizeRoot(assetsRoot);
        var dto=ReadManifest(manifestPath);
        var clips=new List<AnimationClip>();
        foreach(var item in dto.Clips)
        {
            var clip=BuildClip(root,dto,item);
            if(clips.Any(x=>x.Id==clip.Id)) throw new InvalidDataException("动作标识重复。");
            clips.Add(clip);
        }
        foreach(var id in RequiredAnimationIds) if(clips.All(x=>x.Id!=id)) throw new InvalidDataException($"清单缺少 {id}。");
        return CreateAssets(dto,clips);
    }

    public static AssetLoadResult LoadResilient(string assetsRoot,string manifestPath)
    {
        try
        {
            var root=NormalizeRoot(assetsRoot);
            var dto=ReadManifest(manifestPath);
            var clips=new List<AnimationClip>();
            var issues=new List<AssetIssue>();
            foreach(var item in dto.Clips)
            {
                try
                {
                    var clip=BuildClip(root,dto,item);
                    if(clips.Any(x=>x.Id==clip.Id)) throw new InvalidDataException("动作标识重复。");
                    clips.Add(clip);
                }
                catch(Exception error) when(error is InvalidDataException or IOException or ArgumentException)
                {
                    issues.Add(new("clip-disabled",$"{item.Id} 已禁用并回退到 Idle。","warning"));
                }
            }
            if(clips.All(x=>x.Id!=AnimationId.Idle)) clips.Add(BuiltinIdle());
            return new(CreateAssets(dto,clips),issues);
        }
        catch(Exception error) when(error is InvalidDataException or IOException or JsonException or ArgumentException)
        {
            return new(BuiltinAssets(),[new("manifest-fallback","动画清单不可用，已启用内置 Idle。","warning")]);
        }
    }

    private static string NormalizeRoot(string assetsRoot)
        => Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;

    private static ManifestDto ReadManifest(string manifestPath)
    {
        var dto=JsonSerializer.Deserialize<ManifestDto>(File.ReadAllText(manifestPath),new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("动画清单为空。");
        if(dto.SchemaVersion!=1) throw new InvalidDataException($"不支持动画清单版本 {dto.SchemaVersion}。");
        ValidateManifest(dto);
        return dto;
    }

    private static void ValidateManifest(ManifestDto dto)
    {
        if(dto.AssetStage is not ("placeholder" or "formal" or "hybrid")) throw new InvalidDataException("素材阶段无效。");
        if(dto.LogicalCanvas is null || dto.LogicalCanvas.Width!=480 || dto.LogicalCanvas.Height!=600)
            throw new InvalidDataException("逻辑画布必须为 480×600。");
        if(dto.DisplaySizeDip is null || dto.DisplaySizeDip.Width<=0 || dto.DisplaySizeDip.Height<=0)
            throw new InvalidDataException("显示尺寸必须为正数。");
        if(dto.FootAnchor is null || dto.FootAnchor.X<0 || dto.FootAnchor.X>dto.LogicalCanvas.Width
            || dto.FootAnchor.Y<0 || dto.FootAnchor.Y>dto.LogicalCanvas.Height)
            throw new InvalidDataException("脚锚点必须位于逻辑画布内。");
        if(dto.BodyBounds is null || dto.BodyBounds.Left<0 || dto.BodyBounds.Top<0 || dto.BodyBounds.Width<=0 || dto.BodyBounds.Height<=0
            || dto.BodyBounds.Left+dto.BodyBounds.Width>dto.LogicalCanvas.Width
            || dto.BodyBounds.Top+dto.BodyBounds.Height>dto.LogicalCanvas.Height)
            throw new InvalidDataException("人物边界必须位于逻辑画布内。");
        if(dto.AlphaThreshold==0) throw new InvalidDataException("Alpha 阈值必须为正数。");
        if(dto.Clips is null) throw new InvalidDataException("动画动作列表缺失。");
        try { CreateBehavior(dto).Validate(); }
        catch(ArgumentException error) { throw new InvalidDataException("自动行为参数无效。",error); }
        var effects=dto.EffectConstraints;
        if(effects?.SleepBubble is null || effects.SleepBubble.MaxSimultaneous!=1 || effects.SleepBubble.LifecycleMs!=1900
            || effects.SleepBubble.Alternates is null || !effects.SleepBubble.Alternates.SequenceEqual(["ZZZ","ZZ"])
            || effects.Battle is null || effects.Battle.Roses!=1 || effects.Battle.Petals!=3 || effects.Battle.Sparkles!=1
            || effects.Victory is null || effects.Victory.Sparkles!=1
            || effects.Drag is null || effects.Drag.BubblesPerDrag!=1)
            throw new InvalidDataException("特效数量或生命周期不符合角色设定。");
    }

    private static AnimationClip BuildClip(string root,ManifestDto manifest,ClipDto item)
    {
        if(item is null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.ReturnTo)
            || item.Frames is null || item.Frames.Count==0
            || item.Frames.Any(frame=>frame is null || string.IsNullOrWhiteSpace(frame.Path)))
            throw new InvalidDataException("动画动作项缺少必要字段。");
        if(!Enum.TryParse<AnimationId>(item.Id,true,out var id) || !Enum.TryParse<AnimationId>(item.ReturnTo,true,out var back))
            throw new InvalidDataException($"未知动画 {item.Id}。");
        var relative=item.Frames.Select(x=>new FrameSpec(x.Path,x.DurationMs)).ToArray();
        var issues=AssetValidator.CheckClip(id,relative);
        if(issues.Any(x=>x.Severity=="error")) throw new InvalidDataException(string.Join("; ",issues.Select(x=>x.Message)));
        var resolved=relative.Select(x=>
        {
            var path=Path.GetFullPath(Path.Combine(root,x.Path));
            if(!File.Exists(path) || !AssetValidator.IsSafeAssetPath(root,path)) throw new InvalidDataException($"资源不存在或越界：{x.Path}");
            var frameIssues=AssetValidator.CheckFrame(path,(int)manifest.LogicalCanvas.Width,(int)manifest.LogicalCanvas.Height,manifest.AlphaThreshold);
            if(frameIssues.Any(x=>x.Severity=="error")) throw new InvalidDataException(string.Join("; ",frameIssues.Select(x=>x.Message)));
            return x with { Path=path };
        }).ToArray();
        return new(id,resolved,item.Loop,item.Interruptible,back,item.AllowMirror,item.OverlayEffects??true);
    }

    private static AnimationAssets CreateAssets(ManifestDto dto,IReadOnlyList<AnimationClip> clips)
        => new(new(clips),new(dto.LogicalCanvas.Width,dto.LogicalCanvas.Height),new(dto.DisplaySizeDip.Width,dto.DisplaySizeDip.Height),new(dto.FootAnchor.X,dto.FootAnchor.Y),dto.AlphaThreshold,dto.AssetStage,CreateBehavior(dto));

    private static BehaviorOptions CreateBehavior(ManifestDto dto)
    {
        var behavior=dto.Behavior??throw new InvalidDataException("自动行为参数缺失。");
        return new()
        {
            ClickWindow=TimeSpan.FromMilliseconds(behavior.ClickWindowMs),
            RestAfter=TimeSpan.FromMilliseconds(behavior.RestAfterMs),
            SleepAfter=TimeSpan.FromMilliseconds(behavior.SleepAfterMs),
            AutoWalkEnabled=behavior.AutoWalkEnabled,
            WalkDelayMinimum=TimeSpan.FromMilliseconds(behavior.WalkDelayMinimumMs),
            WalkDelayMaximum=TimeSpan.FromMilliseconds(behavior.WalkDelayMaximumMs),
            WalkDistanceMinimumDip=behavior.WalkDistanceMinimumDip,
            WalkDistanceMaximumDip=behavior.WalkDistanceMaximumDip,
            WalkSpeedDipPerSecond=behavior.WalkSpeedDipPerSecond,
            IdleStillRatio=behavior.IdleStillRatio
        };
    }

    private static AnimationClip BuiltinIdle()
        => new(AnimationId.Idle,[new("builtin:idle",1000)],true,true,AnimationId.Idle,false,false);

    private static AnimationAssets BuiltinAssets()
        => new(new([BuiltinIdle()]),new(480,600),new(240,300),new(240,560),16,"builtin",BehaviorOptions.Default);

    private sealed record SizeDto(double Width,double Height);
    private sealed record PointDto(double X,double Y);
    private sealed record RectDto(double Left,double Top,double Width,double Height);
    private sealed record FrameDto(string Path,int DurationMs);
    private sealed record ClipDto(string Id,bool Loop,bool Interruptible,string ReturnTo,bool AllowMirror,bool? OverlayEffects,List<FrameDto> Frames);
    private sealed record SleepEffectDto(int MaxSimultaneous,int LifecycleMs,List<string> Alternates);
    private sealed record BattleEffectDto(int Roses,int Petals,int Sparkles);
    private sealed record VictoryEffectDto(int Sparkles);
    private sealed record DragEffectDto(int BubblesPerDrag);
    private sealed record EffectConstraintsDto(SleepEffectDto SleepBubble,BattleEffectDto Battle,VictoryEffectDto Victory,DragEffectDto Drag);
    private sealed record BehaviorDto(int ClickWindowMs,int RestAfterMs,int SleepAfterMs,bool AutoWalkEnabled,int WalkDelayMinimumMs,int WalkDelayMaximumMs,double WalkDistanceMinimumDip,double WalkDistanceMaximumDip,double WalkSpeedDipPerSecond,double IdleStillRatio);
    private sealed record ManifestDto(int SchemaVersion,string AssetStage,SizeDto LogicalCanvas,SizeDto DisplaySizeDip,PointDto FootAnchor,RectDto BodyBounds,byte AlphaThreshold,BehaviorDto Behavior,List<ClipDto> Clips,EffectConstraintsDto EffectConstraints);
}
