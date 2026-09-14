namespace Charlotte.Windows.Services;

public sealed class LatestFrameGate<T> : IDisposable
{
    private readonly object sync=new();
    private CancellationTokenSource? active;
    private int generation;
    private bool hidden;
    private bool disposed;

    public async Task<bool> TryLoadAsync(Func<CancellationToken,Task<T>> loader,Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(apply);
        CancellationToken token;
        int requestedGeneration;
        lock(sync)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(hidden) return false;
            active?.Cancel();
            active?.Dispose();
            active=new();
            token=active.Token;
            requestedGeneration=++generation;
        }
        T value;
        try { value=await loader(token); }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { return false; }
        lock(sync)
        {
            if(disposed || hidden || token.IsCancellationRequested || requestedGeneration!=generation) return false;
            apply(value);
            return true;
        }
    }

    public void SetHidden(bool value)
    {
        lock(sync)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(hidden==value) return;
            hidden=value;
            generation++;
            if(value) active?.Cancel();
        }
    }

    public void Invalidate()
    {
        lock(sync)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            generation++;
            active?.Cancel();
        }
    }

    public void Dispose()
    {
        lock(sync)
        {
            if(disposed) return;
            disposed=true;
            generation++;
            active?.Cancel();
            active?.Dispose();
            active=null;
        }
    }
}
