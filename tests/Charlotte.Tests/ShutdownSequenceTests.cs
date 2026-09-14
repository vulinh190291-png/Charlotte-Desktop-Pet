using System.IO;
using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class ShutdownSequenceTests
{
    [Fact]
    public async Task Finalization_waits_until_flush_finishes()
    {
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalized=false;
        var shutdown=new ShutdownSequence(()=>release.Task,()=>finalized=true);

        var exiting=shutdown.RequestAsync();

        Assert.True(shutdown.IsExiting);
        Assert.False(exiting.IsCompleted);
        Assert.False(finalized);
        release.SetResult();
        await exiting;
        Assert.True(finalized);
    }

    [Fact]
    public async Task Repeated_requests_share_one_flush_and_one_finalization()
    {
        var flushes=0;
        var finalizations=0;
        var shutdown=new ShutdownSequence(
            ()=>{ flushes++; return Task.CompletedTask; },
            ()=>finalizations++);

        await Task.WhenAll(shutdown.RequestAsync(),shutdown.RequestAsync(),shutdown.RequestAsync());

        Assert.Equal(1,flushes);
        Assert.Equal(1,finalizations);
    }

    [Fact]
    public async Task Finalization_still_runs_when_flush_throws()
    {
        var failure=new IOException("write failed");
        var finalized=false;
        var shutdown=new ShutdownSequence(
            ()=>Task.FromException(failure),
            ()=>finalized=true);

        var actual=await Assert.ThrowsAsync<IOException>(()=>shutdown.RequestAsync());

        Assert.Same(failure,actual);
        Assert.True(finalized);
    }

    [Fact]
    public async Task New_intents_are_rejected_after_shutdown_begins()
    {
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted=0;
        var shutdown=new ShutdownSequence(()=>release.Task,()=>{});
        Assert.True(shutdown.TryRun(()=>accepted++));

        var exiting=shutdown.RequestAsync();

        Assert.False(shutdown.TryRun(()=>accepted++));
        Assert.Equal(1,accepted);
        release.SetResult();
        await exiting;
    }
}
