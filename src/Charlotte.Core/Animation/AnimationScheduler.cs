namespace Charlotte.Core.Animation;

public sealed class AnimationScheduler(AnimationCatalog catalog)
{
    private bool pendingVictory;
    private AnimationId? pendingPanel;
    private bool hidden;
    private TimeSpan? lastClick;
    private int clickLevel;
    public AnimationId Current { get; private set; } = AnimationId.Idle;
    public TimeSpan StartedAt { get; private set; }

    public void Request(AnimationRequest request, TimeSpan now)
    {
        if (hidden)
        {
            if (request.Kind == AnimationRequestKind.Victory) pendingVictory = true;
            if (request.Kind == AnimationRequestKind.Panel) pendingPanel = request.Animation;
            return;
        }
        if (request.Kind == AnimationRequestKind.DragStart) { Start(AnimationId.DragStart,now); return; }
        if (request.Kind == AnimationRequestKind.DragEnd && Current is AnimationId.DragStart or AnimationId.DragHold)
        { Start(AnimationId.DragRelease,now); return; }

        var uninterruptible = !catalog.Get(Current).Interruptible;
        switch (request.Kind)
        {
            case AnimationRequestKind.Victory:
                if (uninterruptible) pendingVictory=true; else Start(AnimationId.Victory,now);
                break;
            case AnimationRequestKind.Panel when request.Animation is AnimationId id:
                if (uninterruptible) pendingPanel=id; else Start(id,now);
                break;
            case AnimationRequestKind.Click when !uninterruptible:
                RequestClick(now);
                break;
        }
    }

    public void Tick(TimeSpan now)
    {
        if (hidden) return;
        var clip=catalog.Get(Current);
        if (clip.Loop || now-StartedAt < clip.Duration) return;
        if (Current == AnimationId.DragStart) Start(AnimationId.DragHold,now);
        else StartNext(now);
    }

    public void SetHidden(bool value, TimeSpan now)
    {
        if (value)
        {
            hidden=true; lastClick=null; clickLevel=0; Current=AnimationId.Idle; StartedAt=now;
            return;
        }
        if (!hidden) return;
        hidden=false; StartNext(now);
    }

    private void RequestClick(TimeSpan now)
    {
        var inside=lastClick is TimeSpan previous && now-previous < TimeSpan.FromMilliseconds(1500);
        if (!inside) clickLevel=0;
        if (inside && clickLevel >= 3) return;
        clickLevel++;
        lastClick=now;
        Start(clickLevel switch { 1=>AnimationId.ClickSoft, 2=>AnimationId.ClickAnnoyed, _=>AnimationId.ClickWarning },now);
    }

    private void StartNext(TimeSpan now)
    {
        if (pendingVictory) { pendingVictory=false; Start(AnimationId.Victory,now); return; }
        if (pendingPanel is AnimationId panel) { pendingPanel=null; Start(panel,now); return; }
        Start(catalog.Get(Current).ReturnTo,now);
    }

    private void Start(AnimationId id,TimeSpan now) { Current=id; StartedAt=now; }
}

public static class FrameTimeline
{
    public static int IndexAt(AnimationClip clip, TimeSpan elapsed)
    {
        if (clip.Frames.Count == 0 || clip.Frames.Any(x=>x.DurationMs<=0))
            throw new ArgumentException("Animation frames require positive durations",nameof(clip));
        long total=clip.Frames.Sum(x=>(long)x.DurationMs);
        long milliseconds=Math.Max(0,(long)Math.Floor(elapsed.TotalMilliseconds));
        long position=clip.Loop ? milliseconds%total : Math.Min(milliseconds,total-1);
        long edge=0;
        for(var i=0;i<clip.Frames.Count;i++)
        {
            edge+=clip.Frames[i].DurationMs;
            if(position<edge) return i;
        }
        return clip.Frames.Count-1;
    }
}
