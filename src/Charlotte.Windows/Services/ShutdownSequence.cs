namespace Charlotte.Windows.Services;

public sealed class ShutdownSequence
{
    private readonly Func<Task> flush;
    private readonly Action finalize;
    private readonly Lazy<Task> running;

    public ShutdownSequence(Func<Task> flush,Action finalize)
    {
        this.flush=flush;
        this.finalize=finalize;
        running=new(RunAsync);
    }

    public bool IsExiting=>running.IsValueCreated;

    public Task RequestAsync()=>running.Value;

    private async Task RunAsync()
    {
        await flush();
        finalize();
    }
}
