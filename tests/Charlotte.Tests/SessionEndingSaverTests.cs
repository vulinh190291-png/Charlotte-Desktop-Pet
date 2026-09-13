using System.IO;
using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class SessionEndingSaverTests
{
    [Fact]
    public async Task Save_waits_for_the_background_writer_to_finish()
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saver=new SessionEndingSaver(async _=>
        {
            entered.SetResult();
            await release.Task;
        },TimeSpan.FromSeconds(2));

        var saving=Task.Run(saver.Save);
        await entered.Task;

        Assert.False(saving.IsCompleted);
        release.SetResult();
        var result=await saving;
        Assert.Equal(SessionEndingSaveStatus.Saved,result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Timeout_returns_promptly_and_cancels_the_writer()
    {
        var cancelled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saver=new SessionEndingSaver(async token=>
        {
            using var registration=token.Register(cancelled.SetResult);
            await Task.Delay(Timeout.InfiniteTimeSpan,token);
        },TimeSpan.FromMilliseconds(50));

        var result=saver.Save();

        Assert.Equal(SessionEndingSaveStatus.TimedOut,result.Status);
        Assert.Null(result.Error);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Writer_failure_is_returned_without_escaping_the_session_event()
    {
        var failure=new IOException("blocked");
        var saver=new SessionEndingSaver(_=>Task.FromException(failure),TimeSpan.FromSeconds(1));

        var result=saver.Save();

        Assert.Equal(SessionEndingSaveStatus.Failed,result.Status);
        Assert.Same(failure,result.Error);
    }
}
