namespace Charlotte.Windows.Services;

public sealed class ShutdownSequence
{
    private readonly Func<Task> flush;
    private readonly Action finalize;
    private readonly Lazy<Task> running;
    private readonly object gate=new();

    public ShutdownSequence(Func<Task> flush,Action finalize)
    {
        this.flush=flush;
        this.finalize=finalize;
        running=new(RunAsync);
    }

    public bool IsExiting { get { lock(gate) return running.IsValueCreated; } }

    public Task RequestAsync()
    {
        lock(gate) return running.Value;
    }

    public bool TryRun(Action intent)
    {
        lock(gate)
        {
            if(running.IsValueCreated) return false;
            intent();
            return true;
        }
    }

    private async Task RunAsync()
    {
        try { await flush(); }
        finally { finalize(); }
    }
}
