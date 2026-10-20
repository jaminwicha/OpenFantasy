using OpenFantasy.Client.Presentation.Audio;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class SoundscapeCueDeriverTests
{
    private static readonly SoundscapeListener Listener = new(Id(1), Id(2), Id(10));
    private static readonly EntityId Walker = Id(10);

    [Fact]
    public void A_visit_starts_with_a_baseline_and_a_new_listener_or_reset_never_replays_earlier_changes()
    {
        var deriver = new SoundscapeCueDeriver();
        Assert.Empty(deriver.Observe(Walking(0, 1)));
        // A completely different position on the first observation is a baseline, never a walk.
        var other = new SoundscapeCueDeriver();
        Assert.Empty(other.Observe(Walking(10_000, 7)));
        Assert.Single(deriver.Observe(Walking(SoundscapeCueDeriver.StrideSubunits, 2)));
        var branch = new SoundscapeListener(Id(1), Id(3), Id(10));
        Assert.Empty(deriver.Observe(Walking(2 * SoundscapeCueDeriver.StrideSubunits, 3, branch)));
        Assert.Equal(branch, deriver.Listener);
        deriver.Reset();
        Assert.Null(deriver.Listener);
        Assert.Empty(deriver.Observe(Walking(3 * SoundscapeCueDeriver.StrideSubunits, 4, branch)));
    }

    [Fact]
    public void Committed_walking_accumulates_strides_and_one_observation_yields_at_most_one_footstep()
    {
        var deriver = new SoundscapeCueDeriver();
        deriver.Observe(Walking(0, 1));
        long position = 0;
        var footsteps = new List<SoundCue>();
        for (ulong revision = 2; revision <= 13; revision++)
        {
            position += WorldMovementInput.DisplacementPerStep;
            footsteps.AddRange(deriver.Observe(Walking(position, revision)));
            // The same presented view on a later frame derives nothing.
            Assert.Empty(deriver.Observe(Walking(position, revision)));
        }
        Assert.Equal((int)(position / SoundscapeCueDeriver.StrideSubunits), footsteps.Count);
        Assert.All(footsteps, cue => Assert.Equal((SoundCueKind.Footstep, SoundMaterial.Ground), (cue.Kind, cue.Material)));
        Assert.Equal([1UL, 2UL], footsteps.Select(cue => cue.Key.Ordinal));
        Assert.Equal(footsteps.Count, footsteps.Select(cue => cue.Key).Distinct().Count());
        // A delayed frame covering several strides still produces one footstep, not a burst.
        Assert.Single(deriver.Observe(Walking(position + 3 * SoundscapeCueDeriver.StrideSubunits, 14)));
    }

    [Fact]
    public void Relocation_incapacity_and_reappearance_are_not_walking()
    {
        var deriver = new SoundscapeCueDeriver();
        deriver.Observe(Walking(0, 1));
        Assert.Empty(deriver.Observe(Walking(SoundscapeCueDeriver.RelocationSubunits + 1, 2)));
        long resumed = SoundscapeCueDeriver.RelocationSubunits + 1;
        Assert.Empty(deriver.Observe(Walking(resumed + SoundscapeCueDeriver.StrideSubunits - 1, 3)));
        Assert.Single(deriver.Observe(Walking(resumed + SoundscapeCueDeriver.StrideSubunits, 4)));
        Assert.Empty(deriver.Observe(Walking(resumed + 3 * SoundscapeCueDeriver.StrideSubunits, 5, incapacitated: true)));
        Assert.Empty(deriver.Observe(Observation(actors: [])));
        Assert.Empty(deriver.Observe(Walking(0, 9)));
        Assert.Empty(deriver.Observe(Walking(SoundscapeCueDeriver.StrideSubunits - 1, 10)));
        Assert.Single(deriver.Observe(Walking(SoundscapeCueDeriver.StrideSubunits, 11)));
    }

    [Fact]
    public void Gathering_and_work_contacts_follow_committed_decreases_and_increases_only()
    {
        var deriver = new SoundscapeCueDeriver();
        EntityId oak = Id(500), berry = Id(501), guard = Id(600), wall = Id(601);
        SoundscapeObservation At(uint oakSwings, uint berrySwings, ulong built, ulong dug, ulong revision) => Observation(
            nodes: [new(oak, GatherNodeKind.Oak, new(256, 0, 256), revision, oakSwings),
                new(berry, GatherNodeKind.Berry, new(512, 0, 256), revision, berrySwings)],
            sites: [new(guard, SoundscapeWorkKind.Construction, new(768, 0, 256), built),
                new(wall, SoundscapeWorkKind.Excavation, new(1024, 0, 256), dug)]);
        Assert.Empty(deriver.Observe(At(5, 3, 0, 0, 1)));
        IReadOnlyList<SoundCue> contacts = deriver.Observe(At(4, 2, 10, 4, 2));
        Assert.Equal(
            [(SoundCueKind.GatherContact, SoundMaterial.Oak, oak), (SoundCueKind.GatherContact, SoundMaterial.Berry, berry),
                (SoundCueKind.ConstructionContact, SoundMaterial.Joinery, guard), (SoundCueKind.ExcavationContact, SoundMaterial.Earth, wall)],
            contacts.Select(cue => (cue.Kind, cue.Material, cue.Key.SourceId)));
        Assert.Equal(new SubunitPosition(256, 0, 256), contacts[0].Origin);
        // Regrowth and unchanged work make no contact sound.
        Assert.Empty(deriver.Observe(At(5, 3, 10, 4, 3)));
    }

    [Fact]
    public void Delivered_keys_are_bounded_and_a_repeated_key_is_delivered_once()
    {
        var deriver = new SoundscapeCueDeriver();
        EntityId oak = Id(500);
        SoundscapeObservation Oak(uint swings, ulong revision) => Observation(
            nodes: [new(oak, GatherNodeKind.Oak, new(256, 0, 256), revision, swings)]);
        deriver.Observe(Oak(5, 1));
        Assert.Single(deriver.Observe(Oak(4, 2)));
        // A regrown node struck again at an identical revision and count is the same committed contact key.
        deriver.Observe(Oak(5, 2));
        Assert.Empty(deriver.Observe(Oak(4, 2)));
        Assert.Single(deriver.Observe(Oak(3, 3)));
    }

    private static SoundscapeObservation Walking(long x, ulong revision, SoundscapeListener? listener = null,
        bool incapacitated = false) =>
        Observation(listener, actors: [new SoundscapeActor(Walker, new SubunitPosition(x, 0, 256), revision, incapacitated)]);

    internal static SoundscapeObservation Observation(SoundscapeListener? listener = null,
        IEnumerable<SoundscapeActor>? actors = null, IEnumerable<SoundscapeGatherNode>? nodes = null,
        IEnumerable<SoundscapeWorkSite>? sites = null, SoundscapeEncounterPhase encounter = SoundscapeEncounterPhase.None,
        bool conversing = false) =>
        new(listener ?? Listener, actors ?? [], nodes ?? [], sites ?? [], encounter, conversing);
}

public sealed class SoundscapeContextDirectorTests
{
    private static SoundscapeObservation Phase(SoundscapeEncounterPhase phase, bool conversing = false) =>
        SoundscapeCueDeriverTests.Observation(encounter: phase, conversing: conversing);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void A_new_visit_restores_the_current_context_without_a_transition(int phaseValue, int scoreValue)
    {
        var phase = (SoundscapeEncounterPhase)phaseValue;
        var score = (SoundscapeScore)scoreValue;
        Assert.True(Enum.IsDefined(phase) && Enum.IsDefined(score));
        var director = new SoundscapeContextDirector();
        SoundscapeContext restored = director.Observe(Phase(phase, conversing: true))!;
        Assert.Equal(new SoundscapeContext(score, SoundscapeAmbience.Outdoor, true, SoundscapeContextChange.Restoration, 0), restored);
        Assert.Null(director.Observe(Phase(phase, conversing: true)));
        director.Reset();
        Assert.Null(director.Current);
        Assert.Equal(SoundscapeContextChange.Restoration, director.Observe(Phase(phase))!.Change);
    }

    [Fact]
    public void Encounter_starts_immediately_dwells_through_aftermath_and_returns_to_exploration()
    {
        var director = new SoundscapeContextDirector();
        director.Observe(Phase(SoundscapeEncounterPhase.None));
        SoundscapeContext started = director.Observe(Phase(SoundscapeEncounterPhase.Engaged))!;
        Assert.Equal((SoundscapeScore.Encounter, SoundscapeContextChange.EncounterStarted, SoundscapeContextDirector.EncounterFadeSeconds),
            (started.Score, started.Change, started.FadeSeconds));
        // A very brief engagement holds its music for the minimum before the aftermath.
        Assert.Null(director.Observe(Phase(SoundscapeEncounterPhase.Concluded)));
        Assert.Null(director.Advance(SoundscapeContextDirector.MinimumEncounterSeconds - 0.1));
        SoundscapeContext ended = director.Advance(0.1)!;
        Assert.Equal((SoundscapeScore.Aftermath, SoundscapeContextChange.EncounterEnded), (ended.Score, ended.Change));
        // The aftermath persists while the outcome is still being read, however long that takes.
        Assert.Null(director.Advance(SoundscapeContextDirector.AftermathSeconds * 2));
        // Its dwell has already elapsed, so leaving the outcome starts the gentle return to exploration at once.
        SoundscapeContext explored = director.Observe(Phase(SoundscapeEncounterPhase.None))!;
        Assert.Equal((SoundscapeScore.Exploration, SoundscapeContextChange.AftermathElapsed, SoundscapeContextDirector.ExplorationFadeSeconds),
            (explored.Score, explored.Change, explored.FadeSeconds));
    }

    [Fact]
    public void A_short_outcome_still_receives_the_full_quiet_aftermath()
    {
        var director = new SoundscapeContextDirector();
        director.Observe(Phase(SoundscapeEncounterPhase.Engaged));
        director.Observe(Phase(SoundscapeEncounterPhase.None));
        Assert.Equal(SoundscapeScore.Aftermath, director.Advance(SoundscapeContextDirector.MinimumEncounterSeconds)!.Score);
        Assert.Null(director.Advance(SoundscapeContextDirector.AftermathSeconds - 0.1));
        Assert.Equal(SoundscapeScore.Exploration, director.Advance(0.1)!.Score);
    }

    [Fact]
    public void A_new_engagement_during_aftermath_is_immediate_and_conversation_ducks_both_beds()
    {
        var director = new SoundscapeContextDirector();
        director.Observe(Phase(SoundscapeEncounterPhase.Engaged));
        director.Observe(Phase(SoundscapeEncounterPhase.None));
        Assert.Equal(SoundscapeScore.Aftermath, director.Advance(SoundscapeContextDirector.MinimumEncounterSeconds)!.Score);
        Assert.Null(director.Advance(SoundscapeContextDirector.AftermathSeconds / 2));
        Assert.Equal(SoundscapeScore.Encounter, director.Observe(Phase(SoundscapeEncounterPhase.Engaged))!.Score);
        SoundscapeContext ducked = director.Observe(Phase(SoundscapeEncounterPhase.Engaged, conversing: true))!;
        Assert.Equal((true, SoundscapeContextChange.Ducking, SoundscapeScore.Encounter), (ducked.IsDucked, ducked.Change, ducked.Score));
        Assert.False(director.Observe(Phase(SoundscapeEncounterPhase.Engaged))!.IsDucked);
        Assert.Throws<ArgumentOutOfRangeException>(() => director.Advance(double.NaN));
    }
}

public sealed class AcousticIntentCatalogTests
{
    [Fact]
    public void Every_derivable_cue_and_context_has_declared_intent_and_none_claims_finished_sound()
    {
        AcousticIntentCatalog catalog = AcousticIntentCatalog.Default;
        foreach ((SoundCueKind kind, SoundMaterial material) in AcousticIntentCatalog.DerivablePairings)
        {
            AcousticIntent intent = catalog.For(new SoundCue(new SoundCueKey(kind, Id(1), 1, 0), material, default));
            Assert.Equal(AcousticBus.Effects, intent.Bus);
            Assert.False(string.IsNullOrWhiteSpace(intent.Caption));
        }
        foreach (SoundscapeScore score in Enum.GetValues<SoundscapeScore>())
            Assert.Equal((AcousticBus.Music, (string?)null), (catalog.For(score).Bus, catalog.For(score).Caption));
        foreach (SoundscapeAmbience ambience in Enum.GetValues<SoundscapeAmbience>())
            Assert.Equal(AcousticBus.Ambience, catalog.For(ambience).Bus);
        Assert.All(catalog.Entries, intent => Assert.Equal(AcousticIntentStatus.AwaitingCreatorAsset, intent.Status));
        Assert.Equal(catalog.Entries.Count, catalog.Entries.Select(intent => intent.Key).Distinct().Count());
        Assert.Throws<InvalidOperationException>(() =>
            catalog.For(new SoundCue(new SoundCueKey(SoundCueKind.Footstep, Id(1), 1, 0), SoundMaterial.Oak, default)));
    }

    [Fact]
    public void Construction_rejects_missing_contexts_wrong_buses_and_mismatched_stream_claims()
    {
        var footstep = new AcousticIntent("cue.footstep.ground", AcousticBus.Effects, AcousticIntentStatus.AwaitingCreatorAsset,
            null, "Footsteps", 0.1, 2);
        var scores = Enum.GetValues<SoundscapeScore>().ToDictionary(score => score, score =>
            new AcousticIntent($"score.{score}", AcousticBus.Music, AcousticIntentStatus.IntentionallySilent, null, null, 0, 1));
        var ambiences = new Dictionary<SoundscapeAmbience, AcousticIntent>
        {
            [SoundscapeAmbience.Outdoor] = new("ambience.outdoor", AcousticBus.Ambience, AcousticIntentStatus.AwaitingCreatorAsset, null, null, 0, 1),
        };
        var cues = new Dictionary<(SoundCueKind, SoundMaterial), AcousticIntent> { [(SoundCueKind.Footstep, SoundMaterial.Ground)] = footstep };
        Assert.NotNull(new AcousticIntentCatalog(cues, scores, ambiences));
        Assert.Throws<ArgumentException>(() => new AcousticIntentCatalog(cues,
            scores.Where(pair => pair.Key != SoundscapeScore.Aftermath).ToDictionary(), ambiences));
        Assert.Throws<ArgumentException>(() => new AcousticIntentCatalog(
            new Dictionary<(SoundCueKind, SoundMaterial), AcousticIntent> { [(SoundCueKind.Footstep, SoundMaterial.Ground)] = footstep with { Bus = AcousticBus.Music } },
            scores, ambiences));
        Assert.Throws<ArgumentException>(() => new AcousticIntentCatalog(
            new Dictionary<(SoundCueKind, SoundMaterial), AcousticIntent> { [(SoundCueKind.Footstep, SoundMaterial.Ground)] = footstep with { Status = AcousticIntentStatus.Mapped } },
            scores, ambiences));
        Assert.Throws<ArgumentException>(() => new AcousticIntentCatalog(
            new Dictionary<(SoundCueKind, SoundMaterial), AcousticIntent> { [(SoundCueKind.Footstep, SoundMaterial.Ground)] = footstep with { StreamPath = "res://x.ogg" } },
            scores, ambiences));
    }

    [Fact]
    public void Playback_gate_reports_unmapped_and_silent_intents_and_bounds_spacing_and_voices()
    {
        var gate = new AcousticPlaybackGate();
        var awaiting = new AcousticIntent("a", AcousticBus.Effects, AcousticIntentStatus.AwaitingCreatorAsset, null, "A", 0, 1);
        var silent = awaiting with { Key = "s", Status = AcousticIntentStatus.IntentionallySilent };
        var mapped = new AcousticIntent("m", AcousticBus.Effects, AcousticIntentStatus.Mapped, "res://m.ogg", "M", 0.1, 2);
        Assert.Equal(AcousticPlayback.AwaitingCreatorAsset, gate.Admit(awaiting, 0));
        Assert.Equal(AcousticPlayback.IntentionallySilent, gate.Admit(silent, 0));
        Assert.Equal(AcousticPlayback.Played, gate.Admit(mapped, 1));
        Assert.Equal(AcousticPlayback.Spaced, gate.Admit(mapped, 1));
        gate.Advance(0.1);
        Assert.Equal(AcousticPlayback.Played, gate.Admit(mapped, 1));
        gate.Advance(0.1);
        Assert.Equal(AcousticPlayback.VoiceLimited, gate.Admit(mapped, 1));
        gate.Advance(0.8);
        Assert.Equal(AcousticPlayback.Played, gate.Admit(mapped, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => gate.Advance(-1));
    }

    [Fact]
    public void Diagnostics_count_every_outcome_and_retain_a_bounded_recent_window()
    {
        var diagnostics = new SoundscapeDiagnostics(capacity: 2);
        AcousticIntent intent = AcousticIntentCatalog.Default.Entries.First(entry => entry.Key == "cue.footstep.ground");
        for (ulong ordinal = 1; ordinal <= 3; ordinal++)
            diagnostics.Record(new SoundCue(new SoundCueKey(SoundCueKind.Footstep, Id(10), ordinal, ordinal), SoundMaterial.Ground, default),
                intent, AcousticPlayback.AwaitingCreatorAsset);
        diagnostics.RecordVisit();
        Assert.Equal((3L, 3L, 1L), (diagnostics.Derived(SoundCueKind.Footstep), diagnostics.Total(AcousticPlayback.AwaitingCreatorAsset), diagnostics.Visits));
        Assert.Equal([2UL, 3UL], diagnostics.RecentCues.Select(record => record.Key.Ordinal));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoundscapeDiagnostics(0));
    }
}

/// <summary>The soundscape against real saved worlds and their permitted views, not hand-built state.</summary>
public sealed class SoundscapeSessionTests
{
    [Fact]
    public async Task Real_walking_derives_footsteps_and_a_reopened_save_replays_none()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Genesis();
        var time = new ManualSessionTimeProvider();
        var deriver = new SoundscapeCueDeriver();
        var footsteps = new List<SoundCue>();
        long travelled;
        await using (var session = new LocalWorldSession(SqliteWorldAuthority.CreateNew(files.Path, genesis), Id(10), Delivery, time))
        {
            SoundscapeObservation first = SoundscapeObservation.From(session.ReadView());
            Assert.Equal(new SoundscapeListener(genesis.WorldId, genesis.BranchId, Id(10)), first.Listener);
            Assert.Empty(deriver.Observe(first));
            // Wren stands two cells east, so the walk goes along the other axis.
            long start = session.ReadView().Actors.Single(actor => actor.ActorId == Id(10)).Feet.Y;
            session.SetReady(true);
            for (int step = 0; step < 14; step++)
            {
                session.SetMovement(0, WorldMovementInput.DisplacementPerStep);
                await Tick(time);
                footsteps.AddRange(deriver.Observe(SoundscapeObservation.From(session.ReadView())));
            }
            session.SetMovement(0, 0);
            await Tick(time);
            Assert.Empty(deriver.Observe(SoundscapeObservation.From(session.ReadView())));
            travelled = session.ReadView().Actors.Single(actor => actor.ActorId == Id(10)).Feet.Y - start;
            await session.StopAsync();
        }
        Assert.True(travelled >= 2 * SoundscapeCueDeriver.StrideSubunits, $"The fixture walked only {travelled} subunits.");
        Assert.Equal((int)(travelled / SoundscapeCueDeriver.StrideSubunits), footsteps.Count);
        Assert.All(footsteps, cue => Assert.Equal(Id(10), cue.Key.SourceId));
        await using var reopened = new LocalWorldSession(SqliteWorldAuthority.OpenExisting(files.Path), Id(10), Delivery,
            new ManualSessionTimeProvider());
        var visit = new SoundscapeCueDeriver();
        Assert.Empty(visit.Observe(SoundscapeObservation.From(reopened.ReadView())));
        Assert.Empty(visit.Observe(SoundscapeObservation.From(reopened.ReadView())));
    }

    [Fact]
    public async Task A_real_committed_swing_sounds_once_at_its_visible_source()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = GatheringWorldFixture.Create();
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(SqliteWorldAuthority.CreateNew(files.Path, genesis), Id(10), Delivery, time);
        var deriver = new SoundscapeCueDeriver();
        Assert.Empty(deriver.Observe(SoundscapeObservation.From(session.ReadView())));
        session.SetReady(true);
        Task<WorldActionReceipt> swing = session.SubmitAction(GatheringWorldFixture.Swing(genesis));
        // A submitted, not yet committed action has no sound.
        Assert.Empty(deriver.Observe(SoundscapeObservation.From(session.ReadView())));
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await swing).Status);
        LocalWorldView struck = session.ReadView();
        SoundCue contact = Assert.Single(deriver.Observe(SoundscapeObservation.From(struck)));
        LocalGatherNodeView node = struck.GatheringNodes.Single();
        Assert.Equal((SoundCueKind.GatherContact, SoundMaterial.Oak, node.NodeId, node.Revision, node.Feet),
            (contact.Kind, contact.Material, contact.Key.SourceId, contact.Key.SourceRevision, contact.Origin));
        Assert.Empty(deriver.Observe(SoundscapeObservation.From(session.ReadView())));
    }

    [Fact]
    public async Task Real_conversation_ducks_and_a_real_encounter_entry_changes_the_score()
    {
        using (var files = new SessionFiles())
        {
            WorldSnapshot genesis = Genesis();
            var time = new ManualSessionTimeProvider();
            await using var session = new LocalWorldSession(SqliteWorldAuthority.CreateNew(files.Path, genesis), Id(10), Delivery, time);
            var director = new SoundscapeContextDirector();
            Assert.False(director.Observe(SoundscapeObservation.From(session.ReadView()))!.IsDucked);
            session.SetReady(true);
            Task<WorldActionReceipt> opening = session.SubmitAction(Speak(genesis));
            await Tick(time);
            Assert.Equal(CommandReceiptStatus.Applied, (await opening).Status);
            SoundscapeContext ducked = director.Observe(SoundscapeObservation.From(session.ReadView()))!;
            Assert.Equal((true, SoundscapeContextChange.Ducking), (ducked.IsDucked, ducked.Change));
        }
        using var storage = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        var clock = new ManualSessionTimeProvider();
        await using var encounter = new LocalWorldSession(SqliteWorldAuthority.CreateNew(storage.Path, fixture.Genesis),
            fixture.Player, fixture.Delivery, clock);
        var score = new SoundscapeContextDirector();
        SoundscapeObservation exploring = SoundscapeObservation.From(encounter.ReadView());
        Assert.Equal(SoundscapeEncounterPhase.None, exploring.Encounter);
        Assert.Equal(SoundscapeScore.Exploration, score.Observe(exploring)!.Score);
        encounter.SetReady(true);
        await Tick(clock);
        Task<WorldActionReceipt> entry = encounter.SubmitAction(fixture.Enter(fixture.Genesis, fixture.FirstScenario));
        await Tick(clock);
        Assert.Equal(CommandReceiptStatus.Applied, (await entry).Status);
        SoundscapeObservation engaged = SoundscapeObservation.From(encounter.ReadView());
        Assert.Equal(SoundscapeEncounterPhase.Engaged, engaged.Encounter);
        Assert.Equal((SoundscapeScore.Encounter, SoundscapeContextChange.EncounterStarted),
            (score.Observe(engaged)!.Score, score.Current!.Change));
    }
}
