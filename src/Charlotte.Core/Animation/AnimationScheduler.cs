namespace Charlotte.Core.Animation;

public sealed class AnimationScheduler
{
    private readonly AnimationCatalog catalog;
    private readonly BehaviorOptions options;
    private readonly IRandomSource random;
    private bool pendingVictory;
    private AnimationId? pendingPanel;
    private bool hidden;
    private bool panelOpen;
    private TimeSpan? lastClick;
    private TimeSpan lastInteraction;
    private TimeSpan? nextWalkAt;
    private int clickLevel;
    private double walkSpaceLeft;
    private double walkSpaceRight;

    public AnimationScheduler(AnimationCatalog catalog)
        : this(catalog,BehaviorOptions.Default,new SystemRandomSource()) { }

    public AnimationScheduler(AnimationCatalog catalog,BehaviorOptions options,IRandomSource? random=null)
    {
        this.catalog=catalog;
        this.options=options;
        this.random=random??new SystemRandomSource();
        options.Validate();
        ScheduleNextWalk(TimeSpan.Zero);
    }

    public AnimationId Current { get; private set; }=AnimationId.Idle;
    public TimeSpan StartedAt { get; private set; }
    public WalkPlan? ActiveWalk { get; private set; }

    public void SetWalkSpace(double leftDip,double rightDip)
    {
        walkSpaceLeft=NormalizeSpace(leftDip);
        walkSpaceRight=NormalizeSpace(rightDip);
    }

    public double WalkOffsetAt(TimeSpan now)
        => ActiveWalk?.OffsetAt(now-StartedAt)??0;

    public bool NeedsWalkSpace(TimeSpan now)
        => !hidden && Current==AnimationId.Idle && !panelOpen && options.AutoWalkEnabled
            && nextWalkAt is TimeSpan due && now>=due;

    public void Request(AnimationRequest request,TimeSpan now)
    {
        if(request.Kind is AnimationRequestKind.Panel or AnimationRequestKind.Victory or AnimationRequestKind.Click or AnimationRequestKind.DragStart)
            NotifyInteraction(now);
        if(hidden)
        {
            if(request.Kind==AnimationRequestKind.Victory) pendingVictory=true;
            if(request.Kind==AnimationRequestKind.Panel) pendingPanel=request.Animation;
            return;
        }
        if(request.Kind==AnimationRequestKind.DragStart) { Start(AnimationId.DragStart,now); return; }
        if(request.Kind==AnimationRequestKind.DragEnd && Current is AnimationId.DragStart or AnimationId.DragHold)
        { Start(AnimationId.DragRelease,now); return; }

        var uninterruptible=!catalog.Get(Current).Interruptible;
        switch(request.Kind)
        {
            case AnimationRequestKind.Victory:
                if(uninterruptible) pendingVictory=true; else Start(AnimationId.Victory,now);
                break;
            case AnimationRequestKind.Panel when request.Animation is AnimationId id:
                if(uninterruptible) pendingPanel=id; else StartRequested(id,now);
                break;
            case AnimationRequestKind.Click when !uninterruptible:
                RequestClick(now);
                break;
        }
    }

    public void Tick(TimeSpan now)
    {
        if(hidden) return;
        var idleFor=now-lastInteraction;
        if(Current is AnimationId.Idle or AnimationId.Rest or AnimationId.Walk)
        {
            if(idleFor>=options.SleepAfter) { Start(AnimationId.Sleep,now); return; }
            if(idleFor>=options.RestAfter && Current is AnimationId.Idle or AnimationId.Walk) { Start(AnimationId.Rest,now); return; }
        }
        if(Current==AnimationId.Walk && ActiveWalk is WalkPlan walk)
        {
            if(now-StartedAt>=walk.Duration)
            {
                Start(AnimationId.Idle,now);
                ScheduleNextWalk(now);
            }
            return;
        }

        if(NeedsWalkSpace(now))
        {
            if(!TryStartWalk(now)) ScheduleNextWalk(now);
            return;
        }

        var clip=catalog.Get(Current);
        if(clip.Loop || now-StartedAt<clip.Duration) return;
        if(Current==AnimationId.DragStart) Start(AnimationId.DragHold,now);
        else StartNext(now);
    }

    public void SetPanelOpen(bool value,TimeSpan now)
    {
        if(panelOpen==value) return;
        panelOpen=value;
        if(value)
        {
            NotifyInteraction(now);
            nextWalkAt=null;
            if(Current==AnimationId.Walk && ActiveWalk is not null) Start(AnimationId.Idle,now);
        }
        else ScheduleNextWalk(now);
    }

    public void SetHidden(bool value,TimeSpan now)
    {
        if(value)
        {
            hidden=true;
            lastClick=null;
            clickLevel=0;
            Start(AnimationId.Idle,now);
            return;
        }
        if(!hidden) return;
        hidden=false;
        StartNext(now);
        ScheduleNextWalk(now);
    }

    public void NotifyInteraction(TimeSpan now)
    {
        lastInteraction=now;
        ScheduleNextWalk(now);
        if(!hidden && Current is AnimationId.Rest or AnimationId.Sleep) Start(AnimationId.Idle,now);
    }

    private void RequestClick(TimeSpan now)
    {
        var inside=lastClick is TimeSpan previous && now-previous<options.ClickWindow;
        if(!inside) clickLevel=0;
        if(inside && clickLevel>=3) return;
        clickLevel++;
        lastClick=now;
        Start(clickLevel switch { 1=>AnimationId.ClickSoft,2=>AnimationId.ClickAnnoyed,_=>AnimationId.ClickWarning },now);
    }

    private void StartNext(TimeSpan now)
    {
        if(pendingVictory) { pendingVictory=false; Start(AnimationId.Victory,now); return; }
        if(pendingPanel is AnimationId panel) { pendingPanel=null; StartRequested(panel,now); return; }
        Start(catalog.Get(Current).ReturnTo,now);
    }

    private void StartRequested(AnimationId id,TimeSpan now)
    {
        if(id==AnimationId.Walk)
        {
            if(!TryStartWalk(now)) Start(AnimationId.Idle,now);
            return;
        }
        Start(id,now);
    }

    private bool TryStartWalk(TimeSpan now)
    {
        if(!catalog.Contains(AnimationId.Walk)) return false;
        var direction=SampleUnit()<.5?-1:1;
        var distance=Sample(options.WalkDistanceMinimumDip,options.WalkDistanceMaximumDip);
        var available=direction<0?walkSpaceLeft:walkSpaceRight;
        if(available<=0)
        {
            direction=-direction;
            available=direction<0?walkSpaceLeft:walkSpaceRight;
        }
        distance=Math.Min(distance,available);
        if(distance<=0) return false;
        ActiveWalk=new(direction,distance,options.WalkSpeedDipPerSecond);
        Current=AnimationId.Walk;
        StartedAt=now;
        nextWalkAt=null;
        return true;
    }

    private void ScheduleNextWalk(TimeSpan now)
    {
        nextWalkAt=options.AutoWalkEnabled && !panelOpen
            ? now+TimeSpan.FromTicks((long)Math.Round(Sample(options.WalkDelayMinimum.Ticks,options.WalkDelayMaximum.Ticks)))
            : null;
    }

    private double Sample(double minimum,double maximum)=>minimum+(maximum-minimum)*SampleUnit();
    private double SampleUnit()=>Math.Clamp(random.NextUnit(),0,1);
    private static double NormalizeSpace(double value)=>double.IsFinite(value)?Math.Max(0,value):0;

    private void Start(AnimationId id,TimeSpan now)
    {
        if(id!=AnimationId.Walk) ActiveWalk=null;
        Current=id;
        StartedAt=now;
    }
}

public static class FrameTimeline
{
    public static int IndexAt(AnimationClip clip,TimeSpan elapsed)
    {
        if(clip.Frames.Count==0 || clip.Frames.Any(x=>x.DurationMs<=0))
            throw new ArgumentException("Animation frames require positive durations",nameof(clip));
        long total=clip.Frames.Sum(x=>(long)x.DurationMs);
        long milliseconds=Math.Max(0,(long)Math.Floor(elapsed.TotalMilliseconds));
        long position=clip.Loop?milliseconds%total:Math.Min(milliseconds,total-1);
        long edge=0;
        for(var i=0;i<clip.Frames.Count;i++)
        {
            edge+=clip.Frames[i].DurationMs;
            if(position<edge) return i;
        }
        return clip.Frames.Count-1;
    }
}
