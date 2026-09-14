using Charlotte.Core.Animation;

namespace Charlotte.Tests;

public class AnimationSchedulerTests
{
    private static AnimationScheduler Create(BehaviorOptions? options=null,IRandomSource? random=null)
    {
        AnimationClip Clip(AnimationId id,int total,bool loop=false,bool interruptible=true,AnimationId back=AnimationId.Idle)
            => new(id,[new($"{id}.png",total)],loop,interruptible,back,false);
        return new(new AnimationCatalog([
            Clip(AnimationId.Idle,2000,true), Clip(AnimationId.Walk,800,true), Clip(AnimationId.Sleep,2000,true), Clip(AnimationId.Rest,2000,true),
            Clip(AnimationId.Battle,1400,false,false), Clip(AnimationId.Victory,900,false,false),
            Clip(AnimationId.ClickSoft,560), Clip(AnimationId.ClickAnnoyed,700), Clip(AnimationId.ClickWarning,840),
            Clip(AnimationId.DragStart,120,false,false,AnimationId.DragHold), Clip(AnimationId.DragHold,600,true,false), Clip(AnimationId.DragRelease,160,false,false)
        ]),options??(BehaviorOptions.Default with { AutoWalkEnabled=false }),random??new SequenceRandomSource(.5));
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

    [Fact]
    public void Automatic_walk_starts_at_sampled_deadline_and_stops_after_bounded_travel()
    {
        var options=BehaviorOptions.Default with
        {
            WalkDelayMinimum=TimeSpan.FromSeconds(20),
            WalkDelayMaximum=TimeSpan.FromSeconds(40),
            WalkDistanceMinimumDip=24,
            WalkDistanceMaximumDip=72,
            WalkSpeedDipPerSecond=24
        };
        var scheduler=Create(options,new SequenceRandomSource(0,1,1,0));
        scheduler.SetWalkSpace(10,40);

        scheduler.Tick(TimeSpan.FromSeconds(20)-TimeSpan.FromMilliseconds(1));
        Assert.Equal(AnimationId.Idle,scheduler.Current);
        scheduler.Tick(TimeSpan.FromSeconds(20));

        Assert.Equal(AnimationId.Walk,scheduler.Current);
        Assert.Equal(new WalkPlan(1,40,24),scheduler.ActiveWalk);
        Assert.Equal(24,scheduler.WalkOffsetAt(TimeSpan.FromSeconds(21)),6);
        scheduler.Tick(TimeSpan.FromSeconds(20)+TimeSpan.FromSeconds(40d/24d));
        Assert.Equal(AnimationId.Idle,scheduler.Current);
    }

    [Fact]
    public void Automatic_walk_uses_the_other_side_when_the_sampled_side_is_blocked()
    {
        var scheduler=Create(BehaviorOptions.Default,new SequenceRandomSource(0,0,0,0));
        scheduler.SetWalkSpace(0,50);

        scheduler.Tick(TimeSpan.FromSeconds(20));

        Assert.Equal(new WalkPlan(1,24,24),scheduler.ActiveWalk);
    }

    [Fact]
    public void Panel_open_suppresses_walk_and_closing_restarts_the_delay()
    {
        var scheduler=Create(BehaviorOptions.Default,new SequenceRandomSource(0,0,1,0));
        scheduler.SetWalkSpace(100,100);
        scheduler.SetPanelOpen(true,TimeSpan.FromSeconds(10));

        scheduler.Tick(TimeSpan.FromMinutes(2));
        Assert.Equal(AnimationId.Idle,scheduler.Current);

        scheduler.SetPanelOpen(false,TimeSpan.FromMinutes(2));
        scheduler.Tick(TimeSpan.FromSeconds(140)-TimeSpan.FromMilliseconds(1));
        Assert.Equal(AnimationId.Idle,scheduler.Current);
        scheduler.Tick(TimeSpan.FromSeconds(140));
        Assert.Equal(AnimationId.Walk,scheduler.Current);
    }

    [Fact]
    public void Missing_walk_clip_does_not_start_ambient_movement()
    {
        AnimationClip Clip(AnimationId id,int duration,bool loop=true)
            => new(id,[new($"{id}.png",duration)],loop,true,AnimationId.Idle,false);
        var scheduler=new AnimationScheduler(
            new AnimationCatalog([Clip(AnimationId.Idle,2000)]),
            BehaviorOptions.Default,
            new SequenceRandomSource(0,1,1));
        scheduler.SetWalkSpace(100,100);

        scheduler.Tick(TimeSpan.FromSeconds(20));

        Assert.Equal(AnimationId.Idle,scheduler.Current);
        Assert.Null(scheduler.ActiveWalk);
    }

    [Fact]
    public void Rest_deadline_interrupts_an_automatic_walk()
    {
        var options=BehaviorOptions.Default with
        {
            WalkDelayMinimum=TimeSpan.FromSeconds(179),
            WalkDelayMaximum=TimeSpan.FromSeconds(179)
        };
        var scheduler=Create(options,new SequenceRandomSource(0,1,1));
        scheduler.SetWalkSpace(100,100);
        scheduler.Tick(TimeSpan.FromSeconds(179));
        Assert.Equal(AnimationId.Walk,scheduler.Current);

        scheduler.Tick(TimeSpan.FromMinutes(3));

        Assert.Equal(AnimationId.Rest,scheduler.Current);
        Assert.Null(scheduler.ActiveWalk);
    }

    [Fact]
    public void Walk_space_is_only_requested_when_an_automatic_walk_is_due()
    {
        var scheduler=Create(BehaviorOptions.Default,new SequenceRandomSource(0));

        Assert.False(scheduler.NeedsWalkSpace(TimeSpan.FromSeconds(20)-TimeSpan.FromMilliseconds(1)));
        Assert.True(scheduler.NeedsWalkSpace(TimeSpan.FromSeconds(20)));
        scheduler.SetPanelOpen(true,TimeSpan.FromSeconds(20));
        Assert.False(scheduler.NeedsWalkSpace(TimeSpan.FromMinutes(1)));
    }

    private sealed class SequenceRandomSource(params double[] values) : IRandomSource
    {
        private int index;
        public double NextUnit()=>values[Math.Min(index++,values.Length-1)];
    }
}
