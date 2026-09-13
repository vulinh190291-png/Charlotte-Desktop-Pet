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
    private const long WsExTransparent=0x20;
    private static readonly nint HwndTopmost=new(-1);

    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length!=2)
        {
            Console.Error.WriteLine("Usage: Charlotte.WindowInputProbe <pet-executable> <test-data-root>");
            return 2;
        }

        Process? pet=null;
        try
        {
            var executable=Path.GetFullPath(args[0]);
            var dataRoot=Path.GetFullPath(args[1]);
            Directory.CreateDirectory(dataRoot);
            var cursorAvailable=GetCursorPos(out var cursor);
            var monitor=MonitorBounds(cursor);
            var targetBounds=TargetBounds(cursor,monitor);
            var application=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
            var target=new Window
            {
                Title="Charlotte cross-process input target",
                WindowStyle=WindowStyle.None,
                ResizeMode=ResizeMode.NoResize,
                ShowInTaskbar=true,
                Topmost=true,
                Background=Brushes.DodgerBlue,
                Width=100,
                Height=100,
                WindowStartupLocation=WindowStartupLocation.Manual
            };
            nint targetHandle=0;
            ProbeResult? result=null;
            Exception? probeError=null;
            target.SourceInitialized+=(_,_)=>targetHandle=new WindowInteropHelper(target).Handle;
            target.Loaded+=async (_,_)=>
            {
                try
                {
                    SetWindowPos(targetHandle,HwndTopmost,targetBounds.Left,targetBounds.Top,targetBounds.Width,targetBounds.Height,0x0040);
                    await Task.Delay(100);
                    pet=StartPet(executable,dataRoot);
                    var petHandle=await WaitForWindowAsync(pet,TimeSpan.FromSeconds(5));
                    if(!GetWindowRect(petHandle,out var petRect)) throw new Win32Exception();
                    var width=petRect.Right-petRect.Left;
                    var height=petRect.Bottom-petRect.Top;
                    var transparentLocal=new POINT { X=Math.Max(1,width/40),Y=Math.Max(1,height/40) };
                    var opaqueLocal=new POINT { X=width/2,Y=(int)Math.Round(height*0.45) };

                    MovePetPointToCursor(petHandle,cursor,transparentLocal);
                    var transparent=await WaitForHitAsync(petHandle,targetHandle,cursor,true,TimeSpan.FromSeconds(2));
                    MovePetPointToCursor(petHandle,cursor,opaqueLocal);
                    var opaque=await WaitForHitAsync(petHandle,targetHandle,cursor,false,TimeSpan.FromSeconds(2));
                    result=new(transparent.HitExpected,opaque.HitExpected,transparent.TransparentStyle,opaque.TransparentStyle,
                        cursorAvailable,new(cursor.X,cursor.Y),new(width,height),transparent.HitWindow.ToInt64(),opaque.HitWindow.ToInt64(),
                        targetHandle.ToInt64(),petHandle.ToInt64());
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
            application.Run(target);
            if(probeError is not null) throw new InvalidOperationException("Window input probe failed.",probeError);
            var completed=result ?? throw new InvalidOperationException("Window input probe did not complete.");
            Console.WriteLine(JsonSerializer.Serialize(completed,new JsonSerializerOptions { WriteIndented=true }));
            return completed.TransparentHitTarget && completed.OpaqueHitPet
                && completed.TransparentStyle && !completed.OpaqueStyle?0:1;
        }
        catch(Exception error)
        {
            Console.Error.WriteLine($"Window input probe error: {error}");
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

    private static Process StartPet(string executable,string dataRoot)
    {
        var startInfo=new ProcessStartInfo(executable) { UseShellExecute=false,WorkingDirectory=Path.GetTempPath() };
        startInfo.ArgumentList.Add("--data-dir");
        startInfo.ArgumentList.Add(dataRoot);
        startInfo.ArgumentList.Add("--diagnostic-shell");
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Charlotte process did not start.");
    }

    private static async Task<nint> WaitForWindowAsync(Process process,TimeSpan timeout)
    {
        var timer=Stopwatch.StartNew();
        while(timer.Elapsed<timeout)
        {
            process.Refresh();
            if(process.HasExited) throw new InvalidOperationException($"Charlotte exited during startup with code {process.ExitCode}.");
            if(process.MainWindowHandle!=0) return process.MainWindowHandle;
            await Task.Delay(20);
        }
        throw new TimeoutException("Charlotte did not expose a window within five seconds.");
    }

    private static async Task<HitResult> WaitForHitAsync(nint pet,nint target,POINT point,bool expectTransparent,TimeSpan timeout)
    {
        var timer=Stopwatch.StartNew();
        HitResult current=default;
        while(timer.Elapsed<timeout)
        {
            var style=(GetWindowLongPtr(pet,-20).ToInt64()&WsExTransparent)!=0;
            var hit=WindowFromPoint(point);
            current=new(expectTransparent?hit==target:hit==pet,style,hit);
            if(current.HitExpected && style==expectTransparent) return current;
            await Task.Delay(16);
        }
        return current;
    }

    private static void MovePetPointToCursor(nint pet,POINT cursor,POINT local)
        => SetWindowPos(pet,HwndTopmost,cursor.X-local.X,cursor.Y-local.Y,0,0,0x0015);

    private static RECT MonitorBounds(POINT point)
    {
        var monitor=MonitorFromPoint(point,2);
        var info=new MONITORINFO { Size=Marshal.SizeOf<MONITORINFO>() };
        if(!GetMonitorInfo(monitor,ref info)) throw new Win32Exception();
        return info.Monitor;
    }

    private static Bounds TargetBounds(POINT cursor,RECT monitor)
    {
        var width=Math.Min(800,monitor.Right-monitor.Left);
        var height=Math.Min(700,monitor.Bottom-monitor.Top);
        var left=Math.Clamp(cursor.X-width/2,monitor.Left,monitor.Right-width);
        var top=Math.Clamp(cursor.Y-height/2,monitor.Top,monitor.Bottom-height);
        return new(left,top,width,height);
    }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(POINT point,uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor,ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd,out RECT rect);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(POINT point);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd,int index);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetWindowPos(nint hwnd,nint insertAfter,int x,int y,int width,int height,uint flags);
}

internal readonly record struct HitResult(bool HitExpected,bool TransparentStyle,nint HitWindow);
internal sealed record ProbeResult(bool TransparentHitTarget,bool OpaqueHitPet,bool TransparentStyle,bool OpaqueStyle,
    bool CursorAvailable,CursorPoint Cursor,Bounds PetSize,long TransparentHitWindow,long OpaqueHitWindow,long TargetWindow,long PetWindow);
internal readonly record struct CursorPoint(int X,int Y);
internal readonly record struct Bounds(int Left,int Top,int Width,int Height)
{
    public Bounds(int width,int height):this(0,0,width,height) { }
}
[StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X,Y; }
[StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left,Top,Right,Bottom; }
[StructLayout(LayoutKind.Sequential)] internal struct MONITORINFO { public int Size; public RECT Monitor,Work; public uint Flags; }
