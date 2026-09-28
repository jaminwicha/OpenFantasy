using OpenFantasy.Client.Presentation;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class FarmHealthFeedbackTests
{
    [Fact]
    public void Admission_and_reappearance_establish_baselines_without_replaying_unobserved_health_changes()
    {
        var view = new FarmHealthFeedback();
        view.Observe(60);
        Assert.False(view.IsVisible);
        view.Observe(0);
        Assert.Equal(FarmHealthChange.Damage, view.Change);
        view.Observe(null);
        Assert.False(view.IsVisible);
        view.Observe(40);
        Assert.False(view.IsVisible);
        var reopened = new FarmHealthFeedback();
        reopened.Observe(40);
        Assert.False(reopened.IsVisible);
    }

    [Fact]
    public void Actual_restoration_replaces_damage_and_repeated_observation_does_not_restart_the_cue()
    {
        var view = new FarmHealthFeedback();
        view.Observe(2);
        view.Observe(0);
        view.Advance(0.1);
        view.Observe(40);
        Assert.Equal(FarmHealthChange.Restoration, view.Change);
        Assert.Equal(40, view.Amount);
        Assert.Equal(0, view.AgeSeconds);
        view.Advance(0.4);
        view.Observe(40);
        Assert.Equal(0.4, view.AgeSeconds);
        Assert.True(view.IsVisible);
        view.Advance(0.4);
        Assert.False(view.IsVisible);
        view.Observe(38);
        Assert.Equal(FarmHealthChange.Damage, view.Change);
        Assert.Equal(2, view.Amount);
    }
}
