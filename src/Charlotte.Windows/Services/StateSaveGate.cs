using Charlotte.Core.Persistence;

namespace Charlotte.Windows.Services;

public sealed class StateSaveGate(IStateStore store)
{
    private readonly object sync=new();
    private Task tail=Task.CompletedTask;

    public Task SaveAsync(AppData data,AppSettings settings,CancellationToken cancellationToken)
    {
        lock(sync)
        {
            var previous=tail;
            var current=Task.Run(async() =>
            {
                try { await previous.ConfigureAwait(false); }
                catch { }
                await store.SaveAsync(data,settings,cancellationToken).ConfigureAwait(false);
            });
            tail=current;
            return current;
        }
    }
}
