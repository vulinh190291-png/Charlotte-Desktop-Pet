using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length!=2)
        {
            Console.Error.WriteLine("Usage: Charlotte.FullscreenProbe <pet-executable> <test-data-root>");
            return 2;
        }

        Process? pet=null;
        try
        {
            var executable=Path.GetFullPath(args[0]);
            var dataRoot=Path.GetFullPath(args[1]);
            Directory.CreateDirectory(dataRoot);
            var startInfo=new ProcessStartInfo(executable) { UseShellExecute=false,WorkingDirectory=Path.GetTempPath() };
            startInfo.ArgumentList.Add("--data-dir");
            startInfo.ArgumentList.Add(dataRoot);
            startInfo.ArgumentList.Add("--diagnostic-shell");
            pet=Process.Start(startInfo) ?? throw new InvalidOperationException("Charlotte process did not start.");
            var petHandle=WaitForWindow(pet,TimeSpan.FromSeconds(5));
            var monitor=MonitorBounds(petHandle);
            var initiallyVisible=IsWindowVisible(petHandle);
            var result=RunProbe(petHandle,monitor,initiallyVisible);
            Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented=true }));
            if(!result.InitiallyVisible) return 1;
            if(!result.ProbeWasForeground) return 3;
            return result.Hidden && result.Restored?0:1;
        }
        catch(Exception error)
        {
            Console.Error.WriteLine($"Fullscreen probe error: {error.Message}");
            return 1;
        }
        finally
        {
            if(pet is not null)
            {
                pet.Refresh();
                if(!pet.HasExited)
                {
                    pet.CloseMainWindow();
                    if(!pet.WaitForExit(3000)) pet.Kill(true);
                }
                pet.Dispose();
            }
        }
    }

static nint WaitForWindow(Process process,TimeSpan timeout)
{
    var timer=Stopwatch.StartNew();
    while(timer.Elapsed<timeout)
    {
        process.Refresh();
        if(process.HasExited) throw new InvalidOperationException($"Charlotte exited during startup with code {process.ExitCode}.");
        if(process.MainWindowHandle!=0) return process.MainWindowHandle;
        Thread.Sleep(20);
    }
    throw new TimeoutException("Charlotte did not expose a window within five seconds.");
}

static ProbeResult RunProbe(nint petHandle,RECT monitor,bool initiallyVisible)
{
    var application=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
    ProbeResult? result=null;
    var window=new Window
    {
        Title="Charlotte Fullscreen Integration Probe",
        WindowStyle=WindowStyle.None,
        ResizeMode=ResizeMode.NoResize,
        ShowInTaskbar=true,
        Topmost=true,
        Background=Brushes.Black,
        WindowStartupLocation=WindowStartupLocation.Manual,
        Left=0,
        Top=0,
        Width=100,
        Height=100
    };
    nint probeHandle=0;
    Exception? probeError=null;
    window.SourceInitialized+=(_,_)=>probeHandle=new WindowInteropHelper(window).Handle;
    window.Loaded+=async (_,_) =>
    {
        try
        {
            var positioned=SetWindowPos(probeHandle,new(-1),monitor.Left,monitor.Top,monitor.Right-monitor.Left,monitor.Bottom-monitor.Top,0x0040);
            await Task.Delay(100);
            window.Activate();
            var foregroundRequested=ForceForeground(probeHandle);
            await Task.Delay(200);
            var foreground=GetForegroundWindow();
            GetWindowThreadProcessId(foreground,out var foregroundProcessId);
            var foregroundProcessName=ProcessName(foregroundProcessId);
            GetWindowRect(probeHandle,out var probeRect);
            var hiddenAt=await WaitForVisibilityAsync(petHandle,false,TimeSpan.FromSeconds(3));
            window.Hide();
            var restoredAt=await WaitForVisibilityAsync(petHandle,true,TimeSpan.FromSeconds(3));
            result=new(initiallyVisible,hiddenAt is not null,restoredAt is not null,hiddenAt,restoredAt,
                positioned,foregroundRequested,foreground==probeHandle,foregroundProcessId,foregroundProcessName,
                new(monitor.Left,monitor.Top,monitor.Right-monitor.Left,monitor.Bottom-monitor.Top),
                new(probeRect.Left,probeRect.Top,probeRect.Right-probeRect.Left,probeRect.Bottom-probeRect.Top));
        }
        catch(Exception error)
        {
            probeError=error;
        }
        finally
        {
            application.Shutdown();
        }
    };
    application.Run(window);
    if(probeError is not null) throw new InvalidOperationException("Fullscreen probe window failed.",probeError);
    return result ?? throw new InvalidOperationException("Fullscreen probe did not complete.");
}

static async Task<double?> WaitForVisibilityAsync(nint hwnd,bool expected,TimeSpan timeout)
{
    var timer=Stopwatch.StartNew();
    while(timer.Elapsed<timeout)
    {
        if(IsWindowVisible(hwnd)==expected) return Math.Round(timer.Elapsed.TotalMilliseconds,2);
        await Task.Delay(20);
    }
    return null;
}

static RECT MonitorBounds(nint hwnd)
{
    var monitor=MonitorFromWindow(hwnd,2);
    var info=new MONITORINFO { Size=Marshal.SizeOf<MONITORINFO>() };
    if(!GetMonitorInfo(monitor,ref info)) throw new Win32Exception();
    return info.Monitor;
}

static bool ForceForeground(nint hwnd)
{
    var foreground=GetForegroundWindow();
    var foregroundThread=GetWindowThreadProcessId(foreground,out _);
    var currentThread=GetCurrentThreadId();
    var attached=foregroundThread!=0 && foregroundThread!=currentThread
        && AttachThreadInput(currentThread,foregroundThread,true);
    try
    {
        BringWindowToTop(hwnd);
        if(SetForegroundWindow(hwnd)) return true;
        SwitchToThisWindow(hwnd,true);
        return GetForegroundWindow()==hwnd;
    }
    finally
    {
        if(attached) AttachThreadInput(currentThread,foregroundThread,false);
    }
}

static string? ProcessName(uint processId)
{
    try
    {
        using var process=Process.GetProcessById((int)processId);
        return process.ProcessName;
    }
    catch
    {
        return null;
    }
}

[DllImport("user32.dll")] static extern bool IsWindowVisible(nint hwnd);
[DllImport("user32.dll")] static extern nint GetForegroundWindow();
[DllImport("user32.dll")] static extern bool GetWindowRect(nint hwnd,out RECT rect);
[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd,out uint processId);
[DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
[DllImport("user32.dll")] static extern bool AttachThreadInput(uint attach,uint attachTo,bool value);
[DllImport("user32.dll")] static extern bool BringWindowToTop(nint hwnd);
[DllImport("user32.dll")] static extern void SwitchToThisWindow(nint hwnd,bool altTab);
[DllImport("user32.dll")] static extern nint MonitorFromWindow(nint hwnd,uint flags);
[DllImport("user32.dll")] static extern bool GetMonitorInfo(nint monitor,ref MONITORINFO info);
[DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(nint hwnd,nint insertAfter,int x,int y,int width,int height,uint flags);
[DllImport("user32.dll")] static extern bool SetForegroundWindow(nint hwnd);
}

record ProbeResult(bool InitiallyVisible,bool Hidden,bool Restored,double? HiddenAfterMilliseconds,double? RestoredAfterMilliseconds,
    bool Positioned,bool ForegroundRequested,bool ProbeWasForeground,uint ForegroundProcessId,string? ForegroundProcessName,
    Bounds Monitor,Bounds Probe);
record Bounds(int Left,int Top,int Width,int Height);

[StructLayout(LayoutKind.Sequential)]
struct RECT { public int Left,Top,Right,Bottom; }
[StructLayout(LayoutKind.Sequential)]
struct MONITORINFO { public int Size; public RECT Monitor,Work; public uint Flags; }
