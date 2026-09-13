using System.IO;
using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class SaveQueueTests
{
    [Fact]
    public async Task Flush_waits_for_pending_write_and_persists_latest_request()
    {
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls=0;
        var queue=new SaveQueue(async _=>
        {
            calls++;
            if(calls==1) { started.SetResult(); await release.Task; }
        },_=>TimeSpan.Zero);
        queue.Request();
        await started.Task;

        var flushing=queue.FlushAsync();

        Assert.False(flushing.IsCompleted);
        release.SetResult();
        Assert.True(await flushing);
        Assert.Equal(2,calls);
        Assert.False(queue.IsDirty);
    }

    [Fact]
    public async Task Requests_during_a_write_are_coalesced_into_one_follow_up_write()
    {
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls=0;
        var queue=new SaveQueue(async _=>
        {
            calls++;
            if(calls==1) { started.SetResult(); await release.Task; }
        },_=>TimeSpan.Zero);
        queue.Request();
        await started.Task;
        queue.Request();
        queue.Request();

        release.SetResult();
        Assert.True(await queue.WaitForIdleAsync());
        Assert.Equal(2,calls);
    }

    [Fact]
    public async Task Terminal_failure_keeps_dirty_state_for_a_later_retry()
    {
        var failing=true;
        var calls=0;
        var queue=new SaveQueue(_=>
        {
            calls++;
            return failing?Task.FromException(new IOException("blocked")):Task.CompletedTask;
        },_=>TimeSpan.Zero);

        Assert.False(await queue.FlushAsync());
        Assert.True(queue.IsDirty);
        Assert.Equal(3,calls);

        failing=false;
        Assert.True(await queue.FlushAsync());
        Assert.False(queue.IsDirty);
    }
}
