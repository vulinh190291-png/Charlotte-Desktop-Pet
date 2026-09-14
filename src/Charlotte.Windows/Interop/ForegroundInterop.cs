using System.Runtime.InteropServices;
using Charlotte.Core.Geometry;
using Charlotte.Windows.Services;

namespace Charlotte.Windows.Interop;

internal static class ForegroundInterop
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);

    internal static bool IsFullscreenOn(PxRect monitorBounds,params nint[] excluded)
    {
        var foreground=GetForegroundWindow();
        if(foreground==0 || !WindowsInterop.GetWindowRect(foreground,out var rect)) return false;
        var isExcluded=foreground==GetShellWindow() || excluded.Contains(foreground);
        return FullscreenWindowPolicy.IsFullscreen(
            monitorBounds,rect.ToRect(),IsWindowVisible(foreground),IsIconic(foreground),isExcluded);
    }
}

internal sealed class ForegroundEventWatcher : IDisposable
{
    private const uint EventSystemForeground=0x0003;
    private const uint WineventOutOfContext=0;
    private const uint WineventSkipOwnProcess=2;
    private readonly WinEventProc callback;
    private readonly Action changed;
    private nint hook;
    private bool disposed;

    private ForegroundEventWatcher(Action changed)
    {
        this.changed=changed;
        callback=OnWinEvent;
        hook=SetWinEventHook(EventSystemForeground,EventSystemForeground,0,callback,0,0,
            WineventOutOfContext|WineventSkipOwnProcess);
    }

    internal static ForegroundEventWatcher? TryCreate(Action changed)
    {
        var watcher=new ForegroundEventWatcher(changed);
        if(watcher.hook!=0) return watcher;
        watcher.Dispose();
        return null;
    }

    private void OnWinEvent(nint eventHook,uint eventType,nint hwnd,int objectId,int childId,uint eventThread,uint eventTime)
    {
        if(!disposed) changed();
    }

    public void Dispose()
    {
        if(disposed) return;
        disposed=true;
        if(hook!=0) UnhookWinEvent(hook);
        hook=0;
    }

    private delegate void WinEventProc(nint eventHook,uint eventType,nint hwnd,int objectId,int childId,uint eventThread,uint eventTime);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint eventMin,uint eventMax,nint eventModule,WinEventProc callback,
        uint processId,uint threadId,uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(nint eventHook);
}
