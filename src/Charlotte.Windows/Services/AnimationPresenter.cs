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
    private readonly FrameCache cache;
    private readonly DispatcherTimer timer = new() { Interval=TimeSpan.FromMilliseconds(16) };
    private readonly long epoch=Stopwatch.GetTimestamp();
    private int renderedIndex=-1;
    private AnimationId renderedAnimation=(AnimationId)(-1);
    private int generation;

    public AnimationPresenter(PetWindow window,AnimationAssets assets,FrameCache cache)
    {
        this.window=window; catalog=assets.Catalog; scheduler=new(assets.Catalog); this.cache=cache;
        timer.Tick+=OnTick;
    }

    public void Start() { timer.Start(); _=RenderAsync(); }

    public void Request(AnimationRequest request)
    {
        scheduler.Request(request,Now); renderedIndex=-1; generation++; _=RenderAsync();
    }

    public void SetHidden(bool hidden)
    {
        scheduler.SetHidden(hidden,Now); generation++;
        if(hidden) timer.Stop(); else { renderedIndex=-1; timer.Start(); _=RenderAsync(); }
    }

    private TimeSpan Now=>Stopwatch.GetElapsedTime(epoch);

    private void OnTick(object? sender,EventArgs e)
    {
        scheduler.Tick(Now);
        _=RenderAsync();
    }

    private async Task RenderAsync()
    {
        var id=scheduler.Current; var clip=catalog.Get(id); var index=FrameTimeline.IndexAt(clip,Now-scheduler.StartedAt);
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

    public void Dispose() => timer.Stop();
}
