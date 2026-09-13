using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace Charlotte.Windows.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly string mutexName;
    private readonly string pipeName;
    private readonly CancellationTokenSource stop=new();
    private Mutex? mutex;
    private bool ownsMutex;
    private Task? listener;
    public event Action? WakeRequested;

    public SingleInstanceService()
    {
        var sid=WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var session=Process.GetCurrentProcess().SessionId;
        var identity=$"CharlotteDesktopPet-{sid.Replace('\\','-')}-{session}";
        mutexName=$"Local\\{identity}";
        pipeName=identity;
    }

    public bool TryAcquire()
    {
        if(mutex is not null) return ownsMutex;
        mutex=new Mutex(false,mutexName);
        try { ownsMutex=mutex.WaitOne(TimeSpan.Zero); }
        catch(AbandonedMutexException) { ownsMutex=true; }
        return ownsMutex;
    }

    public void StartListening()
    {
        if(!ownsMutex) throw new InvalidOperationException("Only the primary instance can listen.");
        listener ??=ListenAsync(stop.Token);
    }

    public async Task<bool> NotifyExistingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client=new NamedPipeClientStream(".",pipeName,PipeDirection.Out,PipeOptions.Asynchronous);
            await client.ConnectAsync(1500,cancellationToken);
            await client.WriteAsync(new byte[] { 1 },cancellationToken);
            await client.FlushAsync(cancellationToken);
            return true;
        }
        catch(IOException) { return false; }
        catch(TimeoutException) { return false; }
        catch(OperationCanceledException) { return false; }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while(!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server=new NamedPipeServerStream(pipeName,PipeDirection.In,1,
                    PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken);
                var message=new byte[1];
                if(await server.ReadAsync(message,cancellationToken)==1 && message[0]==1)
                    WakeRequested?.Invoke();
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) { break; }
            catch(IOException) when(!cancellationToken.IsCancellationRequested) { await Task.Delay(100,cancellationToken); }
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        if(ownsMutex) mutex?.ReleaseMutex();
        mutex?.Dispose();
        stop.Dispose();
    }
}
