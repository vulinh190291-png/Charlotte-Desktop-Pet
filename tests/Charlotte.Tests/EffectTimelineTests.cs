using Charlotte.Core.Animation;

namespace Charlotte.Tests;

public class EffectTimelineTests
{
    [Fact]
    public void Sleep_alternates_one_bubble_per_lifecycle()
    {
        var first=EffectTimeline.Sample(AnimationId.Sleep,TimeSpan.Zero);
        var second=EffectTimeline.Sample(AnimationId.Sleep,TimeSpan.FromMilliseconds(1900));

        Assert.Single(first);
        Assert.Equal(EffectKind.SleepBubble,first[0].Kind);
        Assert.Equal("ZZZ",first[0].Text);
        Assert.Single(second);
        Assert.Equal("ZZ",second[0].Text);
    }

    [Fact]
    public void Battle_never_exceeds_the_confirmed_effect_counts()
    {
        var effects=EffectTimeline.Sample(AnimationId.Battle,TimeSpan.FromMilliseconds(700));

        Assert.Equal(1,effects.Count(x=>x.Kind==EffectKind.Rose));
        Assert.Equal(3,effects.Count(x=>x.Kind==EffectKind.Petal));
        Assert.Equal(1,effects.Count(x=>x.Kind==EffectKind.Sparkle));
    }

    [Fact]
    public void Victory_uses_only_one_sparkle()
    {
        var effects=EffectTimeline.Sample(AnimationId.Victory,TimeSpan.FromMilliseconds(500));

        Assert.Single(effects);
        Assert.Equal(EffectKind.Sparkle,effects[0].Kind);
    }

    [Fact]
    public void Idle_has_no_separate_effect_layer()
        => Assert.Empty(EffectTimeline.Sample(AnimationId.Idle,TimeSpan.FromSeconds(1)));

    [Fact]
    public void Drag_shows_only_one_bubble_during_start()
    {
        var start=EffectTimeline.Sample(AnimationId.DragStart,TimeSpan.FromMilliseconds(60));

        Assert.Single(start);
        Assert.Equal(EffectKind.DragBubble,start[0].Kind);
        Assert.Empty(EffectTimeline.Sample(AnimationId.DragHold,TimeSpan.FromMilliseconds(60)));
        Assert.Empty(EffectTimeline.Sample(AnimationId.DragRelease,TimeSpan.FromMilliseconds(60)));
    }
}
