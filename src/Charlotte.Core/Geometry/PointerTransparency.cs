namespace Charlotte.Core.Geometry;

public static class PointerTransparency
{
    public static bool ShouldClickThrough(AlphaMask? mask,PxRect windowBounds,PxPoint cursor,double scale,bool hasMouseCapture)
    {
        if(mask is null || hasMouseCapture || !double.IsFinite(scale) || scale<=0) return false;
        if(cursor.X<windowBounds.Left || cursor.X>=windowBounds.Right
            || cursor.Y<windowBounds.Top || cursor.Y>=windowBounds.Bottom) return false;
        var x=(int)Math.Floor((cursor.X-windowBounds.Left)/scale);
        var y=(int)Math.Floor((cursor.Y-windowBounds.Top)/scale);
        return !mask.Contains(x,y);
    }
}
