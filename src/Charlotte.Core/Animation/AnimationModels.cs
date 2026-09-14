namespace Charlotte.Core.Animation;

public enum AnimationId { Idle, Walk, Rest, Sleep, ClickSoft, ClickAnnoyed, ClickWarning, DragStart, DragHold, DragRelease, Battle, Victory }
public sealed record FrameSpec(string Path, int DurationMs);
public sealed record AnimationClip(AnimationId Id, IReadOnlyList<FrameSpec> Frames, bool Loop, bool Interruptible, AnimationId ReturnTo, bool AllowMirror)
{
    public TimeSpan Duration => TimeSpan.FromMilliseconds(Frames.Sum(x=>x.DurationMs));
}

public sealed class AnimationCatalog(IEnumerable<AnimationClip> clips)
{
    private readonly IReadOnlyDictionary<AnimationId,AnimationClip> items = clips.ToDictionary(x=>x.Id);
    public bool Contains(AnimationId id)=>items.ContainsKey(id);
    public AnimationClip Get(AnimationId id) => items.TryGetValue(id,out var clip) ? clip : items[AnimationId.Idle];
}

public enum AnimationRequestKind { Panel, Victory, Click, DragStart, DragEnd }
public sealed record AnimationRequest(AnimationRequestKind Kind, AnimationId? Animation = null)
{
    public static AnimationRequest Panel(AnimationId id) => new(AnimationRequestKind.Panel,id);
    public static AnimationRequest Victory() => new(AnimationRequestKind.Victory);
    public static AnimationRequest Click() => new(AnimationRequestKind.Click);
    public static AnimationRequest DragStart() => new(AnimationRequestKind.DragStart);
    public static AnimationRequest DragEnd() => new(AnimationRequestKind.DragEnd);
}
