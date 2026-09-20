using Charlotte.Core.Animation;

namespace Charlotte.Tests;

public class FrameTimelineTests
{
    [Fact]
    public void Catalog_enumerates_only_registered_clips()
    {
        var idle=new AnimationClip(AnimationId.Idle,[new("idle.png",100)],true,true,AnimationId.Idle,false);
        var rest=new AnimationClip(AnimationId.Rest,[new("rest.png",100)],true,true,AnimationId.Idle,false);
        var catalog=new AnimationCatalog([idle,rest]);

        Assert.Equal([AnimationId.Idle,AnimationId.Rest],catalog.Clips.Select(clip=>clip.Id));
    }

    [Fact]
    public void Loop_samples_elapsed_time_without_replaying_frames()
    {
        var clip=new AnimationClip(AnimationId.Walk,Enumerable.Range(0,8).Select(i=>new FrameSpec($"{i}.png",100)).ToArray(),true,true,AnimationId.Idle,false);
        Assert.Equal(2,FrameTimeline.IndexAt(clip,TimeSpan.FromMilliseconds(1050)));
    }

    [Fact]
    public void Nonlooping_clip_holds_last_frame()
    {
        var clip=new AnimationClip(AnimationId.Victory,[new("0",100),new("1",200)],false,false,AnimationId.Idle,false);
        Assert.Equal(1,FrameTimeline.IndexAt(clip,TimeSpan.FromSeconds(9)));
    }

    [Fact]
    public void Zero_duration_frame_is_rejected()
    {
        var clip=new AnimationClip(AnimationId.Idle,[new("0",0)],true,true,AnimationId.Idle,false);
        Assert.Throws<ArgumentException>(()=>FrameTimeline.IndexAt(clip,TimeSpan.Zero));
    }

    [Theory]
    [InlineData(50,50)]
    [InlineData(100,200)]
    [InlineData(350,50)]
    public void Loop_schedules_only_the_next_frame_boundary(int elapsedMs,int expectedDelayMs)
    {
        var clip=new AnimationClip(AnimationId.Idle,[new("0",100),new("1",200)],true,true,AnimationId.Idle,false);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedDelayMs),FrameTimeline.DelayToNextBoundary(clip,TimeSpan.FromMilliseconds(elapsedMs)));
    }

    [Fact]
    public void Completed_nonlooping_clip_has_no_later_frame_boundary()
    {
        var clip=new AnimationClip(AnimationId.Victory,[new("0",100),new("1",200)],false,false,AnimationId.Idle,false);

        Assert.Null(FrameTimeline.DelayToNextBoundary(clip,TimeSpan.FromMilliseconds(300)));
    }
}
