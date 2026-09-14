using Charlotte.Windows.Assets;
using Charlotte.Core.Geometry;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Charlotte.Tests;

public class FrameCacheTests
{
    [Fact]
    public async Task Decoded_frame_is_frozen_scaled_and_reused()
    {
        var root=FindProjectRoot(); var path=System.IO.Path.Combine(root,"assets","character","generated","idle","01.png");
        var cache=new FrameCache();
        var first=await cache.GetAsync(path,240,default); var second=await cache.GetAsync(path,240,default);
        Assert.True(first.IsFrozen); Assert.Equal(240,first.PixelWidth); Assert.Same(first,second);
    }

    [Fact]
    public async Task Builtin_idle_has_a_transparent_background_and_visible_pixels()
    {
        var bitmap=await new FrameCache().GetAsync("builtin:idle",240,default);
        var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];
        bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);

        Assert.True(bitmap.IsFrozen);
        Assert.Equal(240,bitmap.PixelWidth);
        Assert.Equal(300,bitmap.PixelHeight);
        Assert.Contains(Enumerable.Range(0,bitmap.PixelWidth*bitmap.PixelHeight),i=>pixels[i*4+3]==0);
        Assert.Contains(Enumerable.Range(0,bitmap.PixelWidth*bitmap.PixelHeight),i=>pixels[i*4+3]==255);
    }

    [Fact]
    public async Task Least_recently_used_frame_is_evicted_when_the_budget_is_exceeded()
    {
        var cache=new FrameCache(500_000);
        var first=await cache.GetAsync("builtin:idle",240,default);
        await cache.GetAsync("builtin:idle",200,default);

        var decodedAgain=await cache.GetAsync("builtin:idle",240,default);

        Assert.NotSame(first,decodedAgain);
    }

    [Fact]
    public async Task Frame_larger_than_the_budget_is_rejected()
    {
        var cache=new FrameCache(1_000);

        await Assert.ThrowsAsync<FrameBudgetExceededException>(
            ()=>cache.GetAsync("builtin:idle",240,default));
    }

    [Fact]
    public async Task Decoded_frame_caches_a_matching_alpha_mask()
    {
        var frame=await new FrameCache().GetFrameAsync("builtin:idle",240,16,default);

        Assert.Equal(frame.Bitmap.PixelWidth,frame.Mask.Width);
        Assert.Equal(frame.Bitmap.PixelHeight,frame.Mask.Height);
        Assert.False(frame.Mask.Contains(0,0));
        Assert.True(frame.Mask.Contains(120,150));
    }

    [Fact]
    public async Task Dpi_change_evicts_cached_widths_that_are_no_longer_used()
    {
        var cache=new FrameCache();
        var oldWidth=await cache.GetFrameAsync("builtin:idle",240,16,default);
        var kept=await cache.GetFrameAsync("builtin:idle",360,16,default);

        cache.EvictWidthsExcept(360);

        Assert.Same(kept,await cache.GetFrameAsync("builtin:idle",360,16,default));
        Assert.NotSame(oldWidth,await cache.GetFrameAsync("builtin:idle",240,16,default));
    }

    [Fact]
    public async Task Hiding_cancels_a_pending_decode()
    {
        var decoder=new BlockingFrameDecoder();
        var cache=new FrameCache(10_000_000,decoder);
        var loading=cache.GetFrameAsync("pending.frame",240,16,default);
        await decoder.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cache.CancelPending();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>loading);
    }

    [Fact]
    public async Task Decode_completed_after_its_waiter_cancels_is_still_counted_and_evictable()
    {
        var decoder=new DeferredFrameDecoder();
        var cache=new FrameCache(150,decoder);
        var abandonedPath=System.IO.Path.GetFullPath("abandoned.frame");
        var currentPath=System.IO.Path.GetFullPath("current.frame");
        using var abandonedCancellation=new CancellationTokenSource();
        var abandoned=cache.GetFrameAsync(abandonedPath,1,16,abandonedCancellation.Token);
        await decoder.WaitUntilStarted(abandonedPath);
        abandonedCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>abandoned);
        decoder.Complete(abandonedPath);

        var current=cache.GetFrameAsync(currentPath,1,16,default);
        await decoder.WaitUntilStarted(currentPath);
        decoder.Complete(currentPath);
        await current;

        using var retryCancellation=new CancellationTokenSource();
        var retry=cache.GetFrameAsync(abandonedPath,1,16,retryCancellation.Token);
        await decoder.WaitForDecodeCount(abandonedPath,2);
        retryCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>retry);
    }

    private sealed class BlockingFrameDecoder : IFrameDecoder
    {
        public TaskCompletionSource Started { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DecodedFrame> DecodeAsync(string path,int pixelWidth,byte alphaThreshold,CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);
            throw new InvalidOperationException();
        }
    }

    private sealed class DeferredFrameDecoder : IFrameDecoder
    {
        private readonly object sync=new();
        private readonly Dictionary<string,List<TaskCompletionSource<DecodedFrame>>> requests=[];

        public Task<DecodedFrame> DecodeAsync(string path,int pixelWidth,byte alphaThreshold,CancellationToken cancellationToken)
        {
            var completion=new TaskCompletionSource<DecodedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock(sync)
            {
                if(!requests.TryGetValue(path,out var items)) requests[path]=items=[];
                items.Add(completion);
            }
            return completion.Task;
        }

        public void Complete(string path)
        {
            TaskCompletionSource<DecodedFrame> completion;
            lock(sync) completion=requests[path][^1];
            var bitmap=BitmapSource.Create(1,1,96,96,PixelFormats.Pbgra32,null,new byte[] { 0,0,0,255 },4);
            bitmap.Freeze();
            completion.SetResult(new(bitmap,AlphaMask.Create([0,0,0,255],1,1,4,16),100));
        }

        public async Task WaitUntilStarted(string path)=>await WaitForDecodeCount(path,1);

        public async Task WaitForDecodeCount(string path,int expected)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while(true)
            {
                lock(sync)
                    if(requests.TryGetValue(path,out var items) && items.Count>=expected) return;
                await Task.Delay(5,timeout.Token);
            }
        }
    }
    private static string FindProjectRoot()
    {
        var directory=new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName,"Charlotte.sln"))) directory=directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException();
    }
}
