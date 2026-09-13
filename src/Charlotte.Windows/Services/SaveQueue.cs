namespace Charlotte.Windows.Services;

public sealed class SaveQueue
{
    private readonly Func<CancellationToken,Task> writer;
    private readonly Func<int,TimeSpan> retryDelay;
    private readonly Action<Exception,int>? failedAttempt;
    private readonly object gate=new();
    private Task worker=Task.CompletedTask;
    private bool dirty;

    public SaveQueue(Func<CancellationToken,Task> writer,Func<int,TimeSpan> retryDelay,Action<Exception,int>? failedAttempt=null)
    {
        this.writer=writer;
        this.retryDelay=retryDelay;
        this.failedAttempt=failedAttempt;
    }

    public bool IsDirty { get { lock(gate) return dirty; } }

    public void Request()
    {
        lock(gate)
        {
            dirty=true;
            if(worker.IsCompleted) worker=RunAsync();
        }
    }

    public async Task<bool> FlushAsync(CancellationToken cancellationToken=default)
    {
        Request();
        return await WaitForIdleAsync(cancellationToken);
    }

    public async Task<bool> WaitForIdleAsync(CancellationToken cancellationToken=default)
    {
        while(true)
        {
            Task observed;
            lock(gate) observed=worker;
            await observed.WaitAsync(cancellationToken);
            lock(gate)
            {
                if(ReferenceEquals(observed,worker)) return !dirty;
            }
        }
    }

    private async Task RunAsync()
    {
        await Task.Yield();
        while(true)
        {
            lock(gate)
            {
                if(!dirty) return;
                dirty=false;
            }

            var saved=false;
            for(var attempt=1;attempt<=3;attempt++)
            {
                try
                {
                    await writer(CancellationToken.None);
                    saved=true;
                    break;
                }
                catch(Exception error)
                {
                    failedAttempt?.Invoke(error,attempt);
                    if(attempt<3)
                    {
                        var delay=retryDelay(attempt);
                        if(delay>TimeSpan.Zero) await Task.Delay(delay);
                    }
                }
            }
            if(saved) continue;
            lock(gate) dirty=true;
            return;
        }
    }
}
