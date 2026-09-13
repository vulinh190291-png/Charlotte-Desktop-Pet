namespace Charlotte.Windows.Services;

public enum SessionEndingSaveStatus
{
    Saved,
    TimedOut,
    Failed
}

public sealed record SessionEndingSaveResult(SessionEndingSaveStatus Status,Exception? Error=null);

public sealed class SessionEndingSaver(Func<CancellationToken,Task> save,TimeSpan timeout)
{
    public SessionEndingSaveResult Save()
    {
        using var cancellation=new CancellationTokenSource();
        var writing=Task.Run(()=>save(cancellation.Token));
        try
        {
            if(!writing.Wait(timeout))
            {
                cancellation.Cancel();
                return new(SessionEndingSaveStatus.TimedOut);
            }
            writing.GetAwaiter().GetResult();
            return new(SessionEndingSaveStatus.Saved);
        }
        catch(AggregateException error) when(error.InnerExceptions.Count==1)
        {
            return new(SessionEndingSaveStatus.Failed,error.InnerException);
        }
        catch(Exception error)
        {
            return new(SessionEndingSaveStatus.Failed,error);
        }
    }
}
