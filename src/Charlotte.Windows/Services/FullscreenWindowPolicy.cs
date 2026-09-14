using Charlotte.Core.Geometry;

namespace Charlotte.Windows.Services;

public static class FullscreenWindowPolicy
{
    public static bool IsFullscreen(
        PxRect monitorBounds,
        PxRect windowBounds,
        bool visible,
        bool minimized,
        bool excluded)
    {
        if(!visible || minimized || excluded) return false;
        const double tolerance=2;
        return windowBounds.Left<=monitorBounds.Left+tolerance
            && windowBounds.Top<=monitorBounds.Top+tolerance
            && windowBounds.Right>=monitorBounds.Right-tolerance
            && windowBounds.Bottom>=monitorBounds.Bottom-tolerance;
    }
}
