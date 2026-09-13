using System.Runtime.InteropServices;
using Charlotte.Core.Geometry;
namespace Charlotte.Windows.Interop;

internal static class WindowsInterop
{
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left,Top,Right,Bottom; public readonly PxRect ToRect() => new(Left,Top,Right-Left,Bottom-Top); }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct MONITORINFO
    {
        public int Size; public RECT Monitor,Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device;
    }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(POINT point,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor,ref MONITORINFO info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd,out RECT rect);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetWindowPos(nint hwnd,nint insertAfter,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] private static extern nint SetWindowLongPtr(nint hwnd,int index,nint value);
    internal static PxPoint Cursor { get { GetCursorPos(out var p); return new(p.X,p.Y); } }
    internal static MonitorSnapshot MonitorAt(PxPoint point)
    {
        var monitor = MonitorFromPoint(new POINT { X=(int)point.X,Y=(int)point.Y },2);
        var info = new MONITORINFO { Size=Marshal.SizeOf<MONITORINFO>(),Device=string.Empty };
        if (!GetMonitorInfo(monitor,ref info)) throw new System.ComponentModel.Win32Exception();
        uint dpi = 96;
        if (GetDpiForMonitor(monitor,0,out var x,out _) == 0) dpi = x;
        return new(info.Device,info.Monitor.ToRect(),info.Work.ToRect(),dpi);
    }
    internal static void ClickThrough(nint hwnd,bool enabled)
    {
        long style = GetWindowLongPtr(hwnd,-20).ToInt64();
        long next = enabled ? style|0x20 : style&~0x20;
        if (next != style) SetWindowLongPtr(hwnd,-20,(nint)next);
    }
    internal static PxRect Bounds(nint hwnd) { GetWindowRect(hwnd,out var rect); return rect.ToRect(); }
    internal static void Move(nint hwnd,PxPoint p) => SetWindowPos(hwnd,0,(int)Math.Round(p.X),(int)Math.Round(p.Y),0,0,0x0015);
}
