using System.Diagnostics;
using System.Windows.Threading;
using Charlotte.Core.Animation;
using Charlotte.Windows.Assets;
using Charlotte.Windows.Windows;

namespace Charlotte.Windows.Services;

public sealed class AnimationPresenter : IDisposable
{
    private readonly PetWindow window;
    private readonly AnimationScheduler scheduler;
    private readonly AnimationCatalog catalog;
    private readonly BehaviorOptions behavior;
    private readonly FrameCache cache;
    private readonly DispatcherTimer timer = new() { Interval=TimeSpan.FromMilliseconds(16) };
    private readonly long epoch=Stopwatch.GetTimestamp();
    private int renderedIndex=-1;
    private AnimationId renderedAnimation=(AnimationId)(-1);
    private int generation;
    private TimeSpan? walkStartedAt;
    private double appliedWalkOffset;

    public AnimationPresenter(PetWindow window,AnimationAssets assets,FrameCache cache)
    {
        this.window=window; catalog=assets.Catalog; behavior=assets.Behavior; scheduler=new(assets.Catalog,assets.Behavior); this.cache=cache;
        timer.Tick+=OnTick;
    }

    public void Start() { timer.Start(); _=RenderAsync(); }

    public void Request(AnimationRequest request)
    {
        ApplyWalk(Now);
        UpdateWalkSpace();
        scheduler.Request(request,Now); renderedIndex=-1; generation++; _=RenderAsync();
    }

    public void NotifyInteraction()
    {
        ApplyWalk(Now);
        scheduler.NotifyInteraction(Now); renderedIndex=-1; generation++; _=RenderAsync();
    }

    public void SetPanelOpen(bool open)
    {
        ApplyWalk(Now);
        scheduler.SetPanelOpen(open,Now); renderedIndex=-1; generation++; _=RenderAsync();
    }

    public void SetHidden(bool hidden)
    {
        ApplyWalk(Now);
        scheduler.SetHidden(hidden,Now); generation++;
        if(hidden) timer.Stop(); else { renderedIndex=-1; timer.Start(); _=RenderAsync(); }
    }

    private TimeSpan Now=>Stopwatch.GetElapsedTime(epoch);

    private void OnTick(object? sender,EventArgs e)
    {
        var now=Now;
        ApplyWalk(now);
        if(scheduler.NeedsWalkSpace(now)) UpdateWalkSpace();
        scheduler.Tick(now);
        ApplyWalk(now);
        _=RenderAsync();
    }

    private async Task RenderAsync()
    {
        var id=scheduler.Current; var clip=catalog.Get(id); var elapsed=Now-scheduler.StartedAt;
        if(id==AnimationId.Idle) elapsed=IdleRhythm.AnimationElapsed(elapsed,clip.Duration,behavior.IdleStillRatio);
        var index=FrameTimeline.IndexAt(clip,elapsed);
        if(id==renderedAnimation && index==renderedIndex) return;
        var myGeneration=++generation;
        try
        {
            var frame=await cache.GetAsync(clip.Frames[index].Path,480,CancellationToken.None);
            if(myGeneration!=generation) return;
            renderedAnimation=id; renderedIndex=index; window.ShowFrame(frame);
        }
        catch { if(id!=AnimationId.Idle) scheduler.Request(AnimationRequest.Panel(AnimationId.Idle),Now); }
    }

    private void UpdateWalkSpace()
    {
        var space=window.HorizontalWalkSpaceDip;
        scheduler.SetWalkSpace(space.Left,space.Right);
    }

    private void ApplyWalk(TimeSpan now)
    {
        if(scheduler.ActiveWalk is null)
        {
            walkStartedAt=null;
            appliedWalkOffset=0;
            return;
        }
        if(walkStartedAt!=scheduler.StartedAt)
        {
            walkStartedAt=scheduler.StartedAt;
            appliedWalkOffset=0;
        }
        var offset=scheduler.WalkOffsetAt(now);
        window.MoveAmbientBy(offset-appliedWalkOffset);
        appliedWalkOffset=offset;
    }

    public void Dispose() => timer.Stop();
}
