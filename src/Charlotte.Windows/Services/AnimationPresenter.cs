using System.Diagnostics;
using System.Windows.Threading;
using Charlotte.Core.Animation;
using Charlotte.Windows.Assets;
using Charlotte.Windows.Windows;

namespace Charlotte.Windows.Services;

public sealed class AnimationPresenter : IDisposable
{
    private readonly PetWindow window;
    private readonly EffectWindow effects;
    private readonly AnimationScheduler scheduler;
    private readonly AnimationCatalog catalog;
    private readonly BehaviorOptions behavior;
    private readonly FrameCache cache;
    private readonly LatestFrameGate<DecodedFrame> frameGate=new();
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(50) };
    private readonly long epoch=Stopwatch.GetTimestamp();
    private readonly byte alphaThreshold;
    private readonly double displayWidthDip;
    private int pixelWidth;
    private int requestedIndex=-1;
    private AnimationId requestedAnimation=(AnimationId)(-1);
    private TimeSpan? walkStartedAt;
    private double appliedWalkOffset;
    private bool hidden;

    public AnimationPresenter(PetWindow window,AnimationAssets assets,FrameCache cache)
    {
        this.window=window;
        this.cache=cache;
        catalog=assets.Catalog;
        behavior=assets.Behavior;
        alphaThreshold=assets.AlphaThreshold;
        displayWidthDip=assets.DisplaySizeDip.Width;
        pixelWidth=PixelWidthFor(window.CurrentDpi);
        scheduler=new(assets.Catalog,assets.Behavior);
        effects=new(window);
        timer.Tick+=OnTick;
        window.PhysicalDpiChanged+=OnDpiChanged;
    }

    public void Start()
    {
        timer.Start();
        RefreshPresentation(Now);
    }

    public void Request(AnimationRequest request)
    {
        var now=Now;
        ApplyWalk(now);
        UpdateWalkSpace();
        scheduler.Request(request,now);
        InvalidateRequestedFrame();
        RefreshPresentation(now);
    }

    public void NotifyInteraction()
    {
        var now=Now;
        ApplyWalk(now);
        scheduler.NotifyInteraction(now);
        InvalidateRequestedFrame();
        RefreshPresentation(now);
    }

    public void SetPanelOpen(bool open)
    {
        var now=Now;
        ApplyWalk(now);
        scheduler.SetPanelOpen(open,now);
        InvalidateRequestedFrame();
        RefreshPresentation(now);
    }

    public void SetHidden(bool value)
    {
        if(hidden==value) return;
        var now=Now;
        ApplyWalk(now);
        hidden=value;
        scheduler.SetHidden(value,now);
        frameGate.SetHidden(value);
        InvalidateRequestedFrame();
        if(value)
        {
            timer.Stop();
            cache.CancelPending();
            effects.Hide();
        }
        else
        {
            timer.Start();
            RefreshPresentation(now);
        }
    }

    public void OnDpiChanged(uint dpi)
    {
        var next=PixelWidthFor(dpi);
        if(next==pixelWidth) return;
        pixelWidth=next;
        frameGate.Invalidate();
        cache.CancelPending();
        cache.EvictWidthsExcept(pixelWidth);
        InvalidateRequestedFrame();
        if(!hidden) RefreshPresentation(Now);
    }

    private TimeSpan Now=>Stopwatch.GetElapsedTime(epoch);

    private void OnTick(object? sender,EventArgs e)
    {
        var now=Now;
        ApplyWalk(now);
        if(scheduler.NeedsWalkSpace(now)) UpdateWalkSpace();
        scheduler.Tick(now);
        ApplyWalk(now);
        RefreshPresentation(now);
    }

    private void RefreshPresentation(TimeSpan now)
    {
        if(hidden) return;
        RenderEffects(now);
        _=RenderFrameAsync(now);
        ScheduleNextTick(now);
    }

    private async Task RenderFrameAsync(TimeSpan now)
    {
        var id=scheduler.Current;
        var clip=catalog.Get(id);
        var elapsed=FrameElapsed(id,clip,now);
        var index=FrameTimeline.IndexAt(clip,elapsed);
        if(id==requestedAnimation && index==requestedIndex) return;
        requestedAnimation=id;
        requestedIndex=index;
        try
        {
            await frameGate.TryLoadAsync(
                token=>cache.GetFrameAsync(clip.Frames[index].Path,pixelWidth,alphaThreshold,token),
                window.ShowFrame);
        }
        catch(OperationCanceledException) { }
        catch
        {
            if(requestedAnimation==id && requestedIndex==index) InvalidateRequestedFrame();
            if(id!=AnimationId.Idle)
            {
                scheduler.Request(AnimationRequest.Panel(AnimationId.Idle),Now);
                RefreshPresentation(Now);
            }
        }
    }

    private void RenderEffects(TimeSpan now)
    {
        var cues=EffectTimeline.Sample(scheduler.Current,now-scheduler.StartedAt);
        effects.Render(cues,window.PixelBounds,window.CurrentDpi/96d);
    }

    private void ScheduleNextTick(TimeSpan now)
    {
        var id=scheduler.Current;
        var clip=catalog.Get(id);
        var effectCount=EffectTimeline.Sample(id,now-scheduler.StartedAt).Count;
        TimeSpan delay;
        if(scheduler.ActiveWalk is not null) delay=TimeSpan.FromMilliseconds(16);
        else if(effectCount>0) delay=TimeSpan.FromMilliseconds(33);
        else
        {
            var elapsed=FrameElapsed(id,clip,now);
            delay=FrameTimeline.DelayToNextBoundary(clip,elapsed)??TimeSpan.FromMilliseconds(250);
            if(delay>TimeSpan.FromMilliseconds(250)) delay=TimeSpan.FromMilliseconds(250);
            if(delay<TimeSpan.FromMilliseconds(16)) delay=TimeSpan.FromMilliseconds(16);
        }
        if(timer.Interval!=delay) timer.Interval=delay;
    }

    private TimeSpan FrameElapsed(AnimationId id,AnimationClip clip,TimeSpan now)
    {
        var elapsed=now-scheduler.StartedAt;
        return id==AnimationId.Idle
            ? IdleRhythm.AnimationElapsed(elapsed,clip.Duration,behavior.IdleStillRatio)
            : elapsed;
    }

    private int PixelWidthFor(uint dpi)
        => Math.Max(1,(int)Math.Round(displayWidthDip*Math.Max(96,dpi)/96d));

    private void InvalidateRequestedFrame()
    {
        requestedIndex=-1;
        requestedAnimation=(AnimationId)(-1);
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

    public void Dispose()
    {
        window.PhysicalDpiChanged-=OnDpiChanged;
        timer.Stop();
        frameGate.Dispose();
        cache.CancelPending();
        effects.Close();
    }
}
