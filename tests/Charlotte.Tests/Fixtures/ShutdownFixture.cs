using Charlotte.Windows.Services;

namespace Charlotte.Tests.Fixtures;

internal sealed class ShutdownFixture
{
    private readonly TaskCompletionSource releaseWriter=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource writerStarted=new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ShutdownFixture()
    {
        var writes=0;
        var queue=new SaveQueue(async _=>
        {
            writes++;
            if(writes==1)
            {
                writerStarted.SetResult();
                await releaseWriter.Task;
            }
        },_=>TimeSpan.Zero);
        queue.Request();
        Coordinator=new(async()=>{ await queue.FlushAsync(); },()=>ResourcesDisposed=true);
    }

    public ShutdownSequence Coordinator { get; }
    public bool ResourcesDisposed { get; private set; }
    public Task WriterStarted=>writerStarted.Task;
    public void ReleaseWriter()=>releaseWriter.TrySetResult();

    public static ShutdownFixture CreateWithBlockedWriter()=>new();
}
