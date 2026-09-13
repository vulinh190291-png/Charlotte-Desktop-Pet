using Charlotte.Core.Animation;

namespace Charlotte.Tests;

public class FrameTimelineTests
{
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
}
