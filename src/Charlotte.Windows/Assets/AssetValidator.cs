using Charlotte.Core.Animation;
using System.IO;

namespace Charlotte.Windows.Assets;

public static class AssetValidator
{
    public static IReadOnlyList<AssetIssue> CheckClip(AnimationId id, IReadOnlyList<FrameSpec> frames)
    {
        var issues=new List<AssetIssue>();
        var exact=id switch
        {
            AnimationId.Idle or AnimationId.Walk or AnimationId.Rest or AnimationId.Sleep or AnimationId.ClickSoft => 8,
            AnimationId.ClickAnnoyed or AnimationId.Victory => 10,
            AnimationId.ClickWarning => 12,
            AnimationId.Battle => 14,
            AnimationId.DragStart or AnimationId.DragRelease => 1,
            _ => 0
        };
        if ((id==AnimationId.DragHold && frames.Count is <4 or >6) || (exact>0 && frames.Count!=exact))
            issues.Add(new("frame-count",$"{id} 帧数不符合约束。"));
        foreach(var frame in frames)
        {
            if(frame.DurationMs<=0) issues.Add(new("duration",$"{frame.Path} 时长必须为正数。"));
            if(Path.IsPathRooted(frame.Path) || frame.Path.Replace('\\','/').Split('/').Any(x=>x==".."))
                issues.Add(new("unsafe-path",$"{frame.Path} 不在资源根目录内。"));
        }
        var duration=frames.Sum(x=>(long)x.DurationMs);
        if ((id==AnimationId.Battle && duration is <1100 or >1500) || (id==AnimationId.Victory && duration is <800 or >1100))
            issues.Add(new("total-duration",$"{id} 总时长不符合约束。"));
        return issues;
    }
}
