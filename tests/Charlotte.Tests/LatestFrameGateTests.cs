using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public class LatestFrameGateTests
{
    [Fact]
    public async Task A_later_request_prevents_an_older_completion_from_applying()
    {
        using var gate=new LatestFrameGate<string>();
        var first=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var applied=new List<string>();

        var oldRequest=gate.TryLoadAsync(_=>first.Task,applied.Add);
        var newRequest=gate.TryLoadAsync(_=>second.Task,applied.Add);
        second.SetResult("new");
        Assert.True(await newRequest);
        first.SetResult("old");

        Assert.False(await oldRequest);
        Assert.Equal(["new"],applied);
    }

    [Fact]
    public async Task Hidden_gate_cancels_pending_work_and_rejects_new_loads()
    {
        using var gate=new LatestFrameGate<string>();
        var pending=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCount=0;
        var first=gate.TryLoadAsync(_=>{ loadCount++; return pending.Task; },_=>throw new InvalidOperationException());

        gate.SetHidden(true);
        pending.SetResult("ignored");
        var second=await gate.TryLoadAsync(_=>{ loadCount++; return Task.FromResult("unexpected"); },_=>throw new InvalidOperationException());

        Assert.False(await first);
        Assert.False(second);
        Assert.Equal(1,loadCount);
    }
}
