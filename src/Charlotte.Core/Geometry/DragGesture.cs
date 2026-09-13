namespace Charlotte.Core.Geometry;

public static class DragGesture
{
    public static bool HasCrossedThreshold(PxPoint start, PxPoint current, double horizontalThreshold, double verticalThreshold)
        => Math.Abs(current.X - start.X) > horizontalThreshold || Math.Abs(current.Y - start.Y) > verticalThreshold;

    public static PxPoint WindowOrigin(PxPoint eventScreenPoint, PxPoint grabOffsetDip, double scale)
    {
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale));
        return new(eventScreenPoint.X - grabOffsetDip.X * scale, eventScreenPoint.Y - grabOffsetDip.Y * scale);
    }
}
