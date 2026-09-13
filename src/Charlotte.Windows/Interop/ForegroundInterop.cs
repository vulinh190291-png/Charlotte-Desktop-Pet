using System.Runtime.InteropServices;
using Charlotte.Core.Geometry;

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
        if(foreground==0 || foreground==GetShellWindow() || excluded.Contains(foreground)) return false;
        if(!IsWindowVisible(foreground) || IsIconic(foreground) || !WindowsInterop.GetWindowRect(foreground,out var rect)) return false;
        const double tolerance=2;
        return rect.Left<=monitorBounds.Left+tolerance && rect.Top<=monitorBounds.Top+tolerance
            && rect.Right>=monitorBounds.Right-tolerance && rect.Bottom>=monitorBounds.Bottom-tolerance;
    }
}
