namespace Charlotte.Core.Geometry;
public readonly record struct PxPoint(double X, double Y);
public readonly record struct PxSize(double Width, double Height);
public readonly record struct PxRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}
public record MonitorSnapshot(string Id, PxRect Bounds, PxRect WorkArea, uint Dpi);
public static class PositionPolicy
{
    public static double RestoreX(double? ratio,double left,double width,double petWidth)
        => left + Math.Max(0,width-petWidth) * (ratio is double r && double.IsFinite(r) ? Math.Clamp(r,0,1) : .5);
    public static double SaveRatio(double x,double left,double width,double petWidth)
        => width <= petWidth || !double.IsFinite(x) ? .5 : Math.Clamp((x-left)/(width-petWidth),0,1);
    public static PxPoint Clamp(PxPoint origin,PxSize size,PxRect area)
        => new(Math.Clamp(origin.X,area.Left,Math.Max(area.Left,area.Right-size.Width)),
            Math.Clamp(origin.Y,area.Top,Math.Max(area.Top,area.Bottom-size.Height)));
    public static PxPoint PlacePanel(PxRect pet,PxSize panel,PxRect area,double gap)
    {
        double x = pet.Right+gap+panel.Width <= area.Right ? pet.Right+gap : pet.Left-gap-panel.Width;
        return Clamp(new(x,pet.Bottom-panel.Height),panel,area);
    }
    public static PxPoint? ClampPanelIfOutside(PxRect panel,PxRect area)
    {
        if(panel.Left>=area.Left && panel.Top>=area.Top && panel.Right<=area.Right && panel.Bottom<=area.Bottom) return null;
        return Clamp(new(panel.Left,panel.Top),new(panel.Width,panel.Height),area);
    }
}
public enum PlacementMode { GroundAnchored, FreePlaced }
public record PetPlacement(PxPoint Origin,double Baseline,PlacementMode Mode,string MonitorId)
{
    public PetPlacement Reflow(PxRect area,PxSize size,double footOffsetY)
    {
        var proposed = Mode == PlacementMode.GroundAnchored ? Origin with { Y = area.Bottom-footOffsetY } : Origin;
        var origin = PositionPolicy.Clamp(proposed,new(size.Width,footOffsetY),area);
        return this with { Origin = origin, Baseline = origin.Y+footOffsetY };
    }
}
