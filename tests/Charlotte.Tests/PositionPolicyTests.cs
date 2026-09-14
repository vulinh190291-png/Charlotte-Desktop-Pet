using Charlotte.Core.Geometry;
namespace Charlotte.Tests;
public class PositionPolicyTests
{
    [Theory]
    [InlineData(null,0,1920,240,840)]
    [InlineData(1.5,-1920,1920,240,-240)]
    [InlineData(.5,100,180,240,100)]
    [InlineData(-1.0,0,1920,240,0)]
    [InlineData(double.NaN,0,1920,240,840)]
    public void Restore_maps_only_available_horizontal_span(double? ratio,double left,double width,double petWidth,double expected)
        => Assert.Equal(expected,PositionPolicy.RestoreX(ratio,left,width,petWidth));
    [Fact]
    public void Saving_on_negative_monitor_roundtrips()
        => Assert.Equal(.5,PositionPolicy.SaveRatio(-1080,-1920,1920,240));
    [Fact]
    public void Oversized_window_avoids_division_by_zero()
        => Assert.Equal(.5,PositionPolicy.SaveRatio(100,100,180,240));
    [Fact]
    public void Panel_flips_to_left_and_clamps_bottom()
        => Assert.Equal(new PxPoint(580,600),PositionPolicy.PlacePanel(new(900,850,100,150),new(300,400),new(0,0,1000,1000),20));
    [Fact]
    public void Visible_panel_does_not_move_during_topology_refresh()
        => Assert.Null(PositionPolicy.ClampPanelIfOutside(new(100,200,300,400),new(0,0,1000,800)));
    [Fact]
    public void Panel_outside_new_work_area_returns_only_the_clamped_origin()
        => Assert.Equal(new PxPoint(700,400),PositionPolicy.ClampPanelIfOutside(new(850,500,300,400),new(0,0,1000,800)));
    [Fact]
    public void Free_placement_preserves_local_baseline()
    {
        var p = new PetPlacement(new(100,200),480,PlacementMode.FreePlaced,"A");
        var result = p.Reflow(new(0,0,1920,1000),new(240,300),280);
        Assert.Equal(200,result.Origin.Y); Assert.Equal(480,result.Baseline);
    }
    [Fact]
    public void Ground_placement_uses_foot_anchor_not_window_bottom()
    {
        var p = new PetPlacement(new(100,200),480,PlacementMode.GroundAnchored,"A");
        var result = p.Reflow(new(0,0,1920,1000),new(240,300),280);
        Assert.Equal(720,result.Origin.Y); Assert.Equal(1000,result.Baseline);
    }
    [Fact]
    public void Disconnect_clamps_free_placement_back_into_work_area()
    {
        var p = new PetPlacement(new(-1800,1800),2080,PlacementMode.FreePlaced,"A");
        Assert.Equal(new PxPoint(0,720),p.Reflow(new(0,0,1920,1000),new(240,300),280).Origin);
    }
}
