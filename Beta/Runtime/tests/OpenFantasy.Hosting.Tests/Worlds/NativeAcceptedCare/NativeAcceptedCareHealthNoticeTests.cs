using OpenFantasy.Client.Presentation;

namespace OpenFantasy.Hosting.Tests.Client.NativeAcceptedCare;

public sealed class NativeAcceptedCareHealthNoticeTests
{
    [Fact]
    public void Restoration_is_a_new_observation_once_while_sprite_feedback_remains_visible()
    {
        var feedback = new FarmHealthFeedback();
        Assert.Equal(FarmHealthChange.None, feedback.Observe(0));
        Assert.Equal(FarmHealthChange.Restoration, feedback.Observe(40));
        Assert.Equal(40, feedback.Amount);
        Assert.Equal(FarmHealthChange.None, feedback.Observe(40));
        Assert.True(feedback.IsVisible);
        Assert.Equal(FarmHealthChange.Restoration, feedback.Change);
        Assert.Equal(FarmHealthChange.Damage, feedback.Observe(32));
        Assert.Equal(FarmHealthChange.None, feedback.Observe(32));
    }

    [Fact]
    public void First_or_newly_visible_health_does_not_replay_an_old_restoration_notice()
    {
        var feedback = new FarmHealthFeedback();
        Assert.Equal(FarmHealthChange.None, feedback.Observe(40));
        Assert.Equal(FarmHealthChange.None, feedback.Observe(null));
        Assert.Equal(FarmHealthChange.None, feedback.Observe(60));
        Assert.False(feedback.IsVisible);
        Assert.Equal(FarmHealthChange.Restoration, feedback.Observe(65));
        Assert.Equal(5, feedback.Amount);
    }
}
