using Charlotte.Core.Persistence;
using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class StateSaveGateTests
{
    [Fact]
    public async Task Concurrent_saves_are_serialized_in_request_order()
    {
        var store=new BlockingStateStore();
        var gate=new StateSaveGate(store);
        var day=new DateOnly(2026,9,13);

        var first=gate.SaveAsync(AppData.Empty(day) with { NextOrder=1 },AppSettings.Default,default);
        await store.FirstStarted.Task;
        var second=gate.SaveAsync(AppData.Empty(day) with { NextOrder=2 },AppSettings.Default,default);

        Assert.Equal([1],store.StartedOrders);
        Assert.False(second.IsCompleted);
        store.ReleaseFirst.SetResult();
        await Task.WhenAll(first,second);
        Assert.Equal([1,2],store.StartedOrders);
        Assert.Equal(1,store.MaximumConcurrentWrites);
    }

    private sealed class BlockingStateStore : IStateStore
    {
        private readonly object sync=new();
        private readonly List<long> startedOrders=[];
        private int activeWrites;
        public TaskCompletionSource FirstStarted { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<long> StartedOrders { get { lock(sync) return [..startedOrders]; } }
        public int MaximumConcurrentWrites { get; private set; }

        public Task<LoadResult> LoadAsync(CancellationToken cancellationToken)=>throw new NotSupportedException();

        public async Task SaveAsync(AppData data,AppSettings settings,CancellationToken cancellationToken)
        {
            var active=Interlocked.Increment(ref activeWrites);
            MaximumConcurrentWrites=Math.Max(MaximumConcurrentWrites,active);
            lock(sync) startedOrders.Add(data.NextOrder);
            try
            {
                if(data.NextOrder==1)
                {
                    FirstStarted.SetResult();
                    await ReleaseFirst.Task.WaitAsync(cancellationToken);
                }
            }
            finally { Interlocked.Decrement(ref activeWrites); }
        }
    }
}
