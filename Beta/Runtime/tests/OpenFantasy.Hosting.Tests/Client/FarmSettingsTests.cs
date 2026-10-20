using OpenFantasy.Client;
using OpenFantasy.Client.Presentation.Audio;
using OpenFantasy.Client.Settings;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class FarmSettingsTests
{
    [Fact]
    public void Settings_round_trip_through_their_entries_and_clamp_levels()
    {
        FarmSettings changed = FarmSettings.Default with
        {
            Sound = SoundLevels.Full.With("Music", 0.35).With("Effects", 0).With("Voice", 4),
            Captions = true,
        };
        Assert.Equal((0.35, 0.0, 1.0), (changed.Sound.Music, changed.Sound.Effects, changed.Sound.Voice));
        IReadOnlyDictionary<string, string> entries = changed.ToEntries();
        Assert.Equal("1", entries["format"]);
        Assert.Equal(changed, FarmSettings.Parse(entries));
        Assert.Throws<ArgumentOutOfRangeException>(() => SoundLevels.Full.With("Thunder", 1));
        Assert.Equal(1.0, FarmSettings.Clamp(double.NaN));
    }

    [Fact]
    public void Missing_unknown_and_malformed_entries_fall_back_individually()
    {
        var entries = new Dictionary<string, string>
        {
            ["sound.Music"] = "0.5",
            ["sound.Ambience"] = "loud",
            ["sound.Effects"] = "-3",
            ["accessibility.captions"] = "perhaps",
            ["future.option"] = "kept out",
        };
        FarmSettings parsed = FarmSettings.Parse(entries);
        Assert.Equal(new SoundLevels(1, 0.5, 1, 0, 1), parsed.Sound);
        Assert.False(parsed.Captions);
        Assert.Equal(FarmSettings.Default, FarmSettings.Parse(new Dictionary<string, string>()));
    }

    [Fact]
    public void Key_bindings_cover_real_routed_actions_with_unique_assignable_defaults()
    {
        var walking = new[] { FarmInputActions.WalkUp, FarmInputActions.WalkLeft, FarmInputActions.WalkDown, FarmInputActions.WalkRight };
        Assert.All(KeyBindings.Actions, action => Assert.Contains(action.Action, FarmInputActions.Routed.Concat(walking)));
        Assert.DoesNotContain(KeyBindings.Actions, action => action.Action is FarmInputActions.Pause or FarmInputActions.Cancel);
        Assert.Equal(KeyBindings.Actions.Count, KeyBindings.Actions.Select(action => action.DefaultKey).Distinct().Count());
        Assert.All(KeyBindings.Actions, action => Assert.True(KeyBindings.IsAssignable(action.DefaultKey)));
        Assert.True(KeyBindings.Default.IsDefault);
    }

    [Fact]
    public void Assigning_a_used_key_swaps_and_reserved_keys_are_refused()
    {
        KeyBindings talkOnT = KeyBindings.Default.Assign("talk", 'T');
        Assert.Equal(('T', 'G'), (talkOnT["talk"], talkOnT["give_wood"]));
        KeyBindings swapped = talkOnT.Assign("gather", 'G');
        Assert.Equal(('G', 'F'), (swapped["gather"], swapped["give_wood"]));
        Assert.Equal(KeyBindings.Actions.Count, KeyBindings.Actions.Select(action => swapped[action.Action]).Distinct().Count());
        Assert.False(swapped.IsDefault);
        foreach (long reserved in new[] { KeyBindings.Escape, KeyBindings.Left, KeyBindings.Enter, KeyBindings.Space, KeyBindings.Tab, 0L })
            Assert.Throws<ArgumentException>(() => KeyBindings.Default.Assign("talk", reserved));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyBindings.Default.Assign("pause_world", 'P'));
    }

    [Fact]
    public void Saved_keys_round_trip_and_damaged_bindings_fall_back_to_defaults()
    {
        FarmSettings settings = FarmSettings.Default with { Keys = KeyBindings.Default.Assign("talk", 'T').Assign("gather", 'G') };
        Assert.Equal(settings, FarmSettings.Parse(settings.ToEntries()));
        var duplicate = new Dictionary<string, string>(settings.ToEntries()) { ["keys.history"] = ((long)'T').ToString() };
        Assert.Equal(KeyBindings.Default, FarmSettings.Parse(duplicate).Keys);
        var reserved = new Dictionary<string, string> { ["keys.talk"] = KeyBindings.Escape.ToString(), ["keys.gather"] = "-4" };
        Assert.Equal(KeyBindings.Default, FarmSettings.Parse(reserved).Keys);
    }

    [Fact]
    public void Captions_show_notable_sounds_only_when_enabled_and_never_stack_or_reveal_withheld_sources()
    {
        AcousticIntentCatalog catalog = AcousticIntentCatalog.Default;
        AcousticIntent footsteps = catalog.Entries.Single(intent => intent.Key == "cue.footstep.ground");
        AcousticIntent oak = catalog.Entries.Single(intent => intent.Key == "cue.gather.oak");
        AcousticIntent joinery = catalog.Entries.Single(intent => intent.Key == "cue.construction.joinery");
        Assert.False(footsteps.IsCaptioned);
        Assert.True(oak.IsCaptioned);
        Assert.All(catalog.Entries.Where(intent => intent.Bus is AcousticBus.Music or AcousticBus.Ambience),
            intent => Assert.False(intent.IsCaptioned));

        var feed = new CaptionFeed();
        feed.Offer(oak, AcousticPlayback.AwaitingCreatorAsset);
        Assert.Empty(feed.Visible);
        feed.SetEnabled(true);
        feed.Offer(footsteps, AcousticPlayback.AwaitingCreatorAsset);
        feed.Offer(oak, AcousticPlayback.Withheld);
        Assert.Empty(feed.Visible);
        feed.Offer(oak, AcousticPlayback.AwaitingCreatorAsset);
        feed.Advance(1);
        feed.Offer(oak, AcousticPlayback.Spaced);
        CaptionLine refreshed = Assert.Single(feed.Visible);
        Assert.Equal(("Chopping wood", 0.0), (refreshed.Text, refreshed.AgeSeconds));
        feed.Offer(joinery, AcousticPlayback.AwaitingCreatorAsset);
        feed.Advance(CaptionFeed.LifetimeSeconds - 0.5);
        Assert.Equal(["Chopping wood", "Joinery work"], feed.Visible.Select(line => line.Text));
        feed.Advance(0.5);
        Assert.Empty(feed.Visible);
        feed.Offer(oak, AcousticPlayback.AwaitingCreatorAsset);
        feed.SetEnabled(false);
        Assert.Empty(feed.Visible);
    }
}
