using Charlotte.Core.Animation;

namespace Charlotte.Tests;

public class AnimationSchedulerTests
{
    private static AnimationScheduler Create()
    {
        AnimationClip Clip(AnimationId id,int total,bool loop=false,bool interruptible=true,AnimationId back=AnimationId.Idle)
            => new(id,[new($"{id}.png",total)],loop,interruptible,back,false);
        return new(new AnimationCatalog([
            Clip(AnimationId.Idle,2000,true), Clip(AnimationId.Sleep,2000,true), Clip(AnimationId.Rest,2000,true),
            Clip(AnimationId.Battle,1400,false,false), Clip(AnimationId.Victory,900,false,false),
            Clip(AnimationId.ClickSoft,560), Clip(AnimationId.ClickAnnoyed,700), Clip(AnimationId.ClickWarning,840),
            Clip(AnimationId.DragStart,120,false,false,AnimationId.DragHold), Clip(AnimationId.DragHold,600,true,false), Clip(AnimationId.DragRelease,160,false,false)
        ]));
    }

    [Fact]
    public void Battle_coalesces_victory_and_keeps_latest_panel_action()
    {
        var s=Create();
        s.Request(AnimationRequest.Panel(AnimationId.Battle),TimeSpan.Zero);
        s.Request(AnimationRequest.Victory(),TimeSpan.FromMilliseconds(100));
        s.Request(AnimationRequest.Victory(),TimeSpan.FromMilliseconds(110));
        s.Request(AnimationRequest.Panel(AnimationId.Rest),TimeSpan.FromMilliseconds(120));
        s.Request(AnimationRequest.Panel(AnimationId.Sleep),TimeSpan.FromMilliseconds(130));
        s.Tick(TimeSpan.FromMilliseconds(1400)); Assert.Equal(AnimationId.Victory,s.Current);
        s.Tick(TimeSpan.FromMilliseconds(2300)); Assert.Equal(AnimationId.Sleep,s.Current);
    }

    [Fact]
    public void Click_escalates_inside_window_and_resets_at_boundary()
    {
        var s=Create();
        s.Request(AnimationRequest.Click(),TimeSpan.Zero); Assert.Equal(AnimationId.ClickSoft,s.Current);
        s.Request(AnimationRequest.Click(),TimeSpan.FromMilliseconds(1499)); Assert.Equal(AnimationId.ClickAnnoyed,s.Current);
        s.Request(AnimationRequest.Click(),TimeSpan.FromMilliseconds(2998)); Assert.Equal(AnimationId.ClickWarning,s.Current);
        s.Request(AnimationRequest.Click(),TimeSpan.FromMilliseconds(4498)); Assert.Equal(AnimationId.ClickSoft,s.Current);
    }

    [Fact]
    public void Drag_interrupts_battle_and_release_returns_to_pending_victory()
    {
        var s=Create();
        s.Request(AnimationRequest.Panel(AnimationId.Battle),TimeSpan.Zero);
        s.Request(AnimationRequest.Victory(),TimeSpan.FromMilliseconds(10));
        s.Request(AnimationRequest.DragStart(),TimeSpan.FromMilliseconds(20));
        Assert.Equal(AnimationId.DragStart,s.Current);
        s.Tick(TimeSpan.FromMilliseconds(140)); Assert.Equal(AnimationId.DragHold,s.Current);
        s.Request(AnimationRequest.DragEnd(),TimeSpan.FromMilliseconds(200));
        s.Tick(TimeSpan.FromMilliseconds(360)); Assert.Equal(AnimationId.Victory,s.Current);
    }

    [Fact]
    public void Hidden_state_keeps_only_victory_and_latest_panel_action()
    {
        var s=Create(); s.SetHidden(true,TimeSpan.Zero);
        s.Request(AnimationRequest.Click(),TimeSpan.FromMilliseconds(10));
        s.Request(AnimationRequest.Victory(),TimeSpan.FromMilliseconds(20));
        s.Request(AnimationRequest.Panel(AnimationId.Rest),TimeSpan.FromMilliseconds(30));
        s.Request(AnimationRequest.Panel(AnimationId.Sleep),TimeSpan.FromMilliseconds(40));
        s.SetHidden(false,TimeSpan.FromSeconds(2)); Assert.Equal(AnimationId.Victory,s.Current);
        s.Tick(TimeSpan.FromMilliseconds(2900)); Assert.Equal(AnimationId.Sleep,s.Current);
    }

    [Fact]
    public void Idle_enters_rest_at_three_minutes_and_sleep_at_ten()
    {
        var s=Create();

        s.Tick(TimeSpan.FromMinutes(3)-TimeSpan.FromMilliseconds(1));
        Assert.Equal(AnimationId.Idle,s.Current);
        s.Tick(TimeSpan.FromMinutes(3));
        Assert.Equal(AnimationId.Rest,s.Current);
        s.Tick(TimeSpan.FromMinutes(10));
        Assert.Equal(AnimationId.Sleep,s.Current);
    }

    [Fact]
    public void Interaction_wakes_pet_and_restarts_idle_clock()
    {
        var s=Create();
        s.Tick(TimeSpan.FromMinutes(3));

        s.NotifyInteraction(TimeSpan.FromMinutes(4));
        s.Tick(TimeSpan.FromMinutes(7)-TimeSpan.FromMilliseconds(1));

        Assert.Equal(AnimationId.Idle,s.Current);
        s.Tick(TimeSpan.FromMinutes(7));
        Assert.Equal(AnimationId.Rest,s.Current);
    }
}
