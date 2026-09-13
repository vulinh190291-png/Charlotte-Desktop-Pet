using Charlotte.Core.Animation;
using Charlotte.Windows.Assets;

namespace Charlotte.Tests;

public class AssetValidatorTests
{
    [Theory]
    [InlineData(AnimationId.Idle,8)]
    [InlineData(AnimationId.Walk,8)]
    [InlineData(AnimationId.Rest,8)]
    [InlineData(AnimationId.ClickSoft,8)]
    [InlineData(AnimationId.ClickAnnoyed,10)]
    [InlineData(AnimationId.ClickWarning,12)]
    [InlineData(AnimationId.Battle,14)]
    [InlineData(AnimationId.Victory,10)]
    public void Required_frame_count_is_enforced(AnimationId id,int count)
    {
        var valid=Enumerable.Range(1,count).Select(i=>new FrameSpec($"{id}/{i:00}.png",100)).ToArray();
        Assert.DoesNotContain(AssetValidator.CheckClip(id,valid),x=>x.Code=="frame-count");
        Assert.Contains(AssetValidator.CheckClip(id,valid[..^1]),x=>x.Code=="frame-count");
    }

    [Fact]
    public void Unsafe_or_nonpositive_frame_is_rejected()
    {
        var frames=Enumerable.Range(1,8).Select(i=>new FrameSpec(i==1?"../escape.png":$"idle/{i}.png",i==2?0:100)).ToArray();
        var issues=AssetValidator.CheckClip(AnimationId.Idle,frames);
        Assert.Contains(issues,x=>x.Code=="unsafe-path");
        Assert.Contains(issues,x=>x.Code=="duration");
    }

    [Fact]
    public void Battle_and_victory_duration_ranges_are_enforced()
    {
        Assert.Contains(AssetValidator.CheckClip(AnimationId.Battle,Enumerable.Range(0,14).Select(i=>new FrameSpec($"b/{i}.png",200)).ToArray()),x=>x.Code=="total-duration");
        Assert.Contains(AssetValidator.CheckClip(AnimationId.Victory,Enumerable.Range(0,10).Select(i=>new FrameSpec($"v/{i}.png",50)).ToArray()),x=>x.Code=="total-duration");
    }
}
