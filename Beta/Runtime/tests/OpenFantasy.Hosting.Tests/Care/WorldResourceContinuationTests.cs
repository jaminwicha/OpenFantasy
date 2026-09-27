using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class WorldResourceContinuationTests
{
    [Fact]
    public void Actual_combat_crosses_two_segments_with_exact_retrieval_and_deterministic_replay()
    {
        var fixture = new CareWorldFixture(rewardExperience: 1000, strikeMana: 1, enduranceProfile: true);
        var session = new CareWorldSession(fixture.Genesis);
        var rewarded = fixture.CompleteQuest(session);
        var close = fixture.Command(rewarded, fixture.Player,
            new CloseDialogueIntent(rewarded.Dialogues.Single().DialogueId));
        CareWorldFixture.Commit(session, fixture.Batch(rewarded, WorldBatchKind.HeldInteraction, [close]));
        var state = fixture.BeginCombat(session);
        for (int step = 0; state.Progression!.Revision < 520; step++)
        {
            Assert.True(step < 4000, "Actual attack/readiness boundaries must remain within the fixed stress journey bound.");
            state = fixture.NextCombatBoundary(session, playerAttacks: true);
        }
        Assert.Equal(2, session.Archives.Count);
        Assert.Equal(8, state.ResourceHistory!.History.Batches.Count);
        Assert.Equal(512UL, state.ResourceHistory.History.Basis.Revision);
        Assert.Equal(rewarded.Quests!.ContentHash, state.Quests!.ContentHash);
        Assert.True(state.Progression.GetActor(fixture.Player).CurrentHp < rewarded.Progression!.GetActor(fixture.Player).CurrentHp);
        Assert.True(state.Progression.GetActor(fixture.Player).CurrentMp < rewarded.Progression.GetActor(fixture.Player).CurrentMp);

        WorldResourceArchiveAnchor? anchor = null;
        foreach (var segment in session.Archives)
        {
            var restored = WorldResourceArchiveSegment.Decode(segment.Reference, segment.Encode(), anchor);
            var closing = WorldSnapshotCodec.Decode(restored.Encode(), anchor);
            Assert.Equal(ActorResourceHistory.MaximumBatches, closing.ResourceHistory!.History.Batches.Count);
            Assert.Equal(closing.ContentHash, restored.Reference.BoundaryHash);
            anchor = restored.Anchor;
        }
        byte[] final = WorldSnapshotCodec.Encode(state);
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(final));
        Assert.Equal(final, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(final, anchor)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(final, session.Archives[0].Anchor));
        Assert.Throws<FormatException>(() => WorldResourceArchiveSegment.Decode(session.Archives[1].Reference,
            session.Archives[1].Encode()));

        var firstBoundary = WorldSnapshotCodec.Decode(session.Archives[0].Encode());
        var oldAttack = firstBoundary.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>().First();
        Assert.False(state.IdentityIndex.Contains(oldAttack.CommandId));
        var control = state.Inventory.Controls.Single(value => value.ActorId == oldAttack.Request.ActorId);
        var reused = new WorldActionCommand(oldAttack.CommandId, control.PrincipalId, control.SessionId,
            control.ActorId, state.WorldId, state.BranchId, state.Space.GetActor(control.ActorId).Revision,
            new QueueEncounterWaitIntent(state.EncounterActions!.Scope.EncounterId, state.Pacing!.Encounter!.Revision));
        var rejectedReuse = fixture.Batch(state, WorldBatchKind.EncounterDecision, [reused]);
        Assert.Contains("already occupied", Assert.Throws<InvalidOperationException>(() => session.Commit(rejectedReuse)).Message);
        Assert.Equal(final, WorldSnapshotCodec.Encode(session.ReadStateForHost()));
        Assert.Equal(2, session.Archives.Count);

        var actions = state.EncounterActions!;
        Assert.True(actions.Random.NextCounter > 1);
        var rewound = new EncounterActionSnapshot(actions.Scope, actions.Seed, actions.Revision, actions.Abilities,
            actions.Combatants, actions.Pending,
            new RandomCounterState(actions.Random.AlgorithmVersion, actions.Random.StreamFingerprint, 1, false), actions.LastResolution);
        var lifecycle = state.EncounterLifecycle!;
        var changedLifecycle = new EncounterLifecycleSnapshot(lifecycle.WorldId, lifecycle.BranchId, lifecycle.Seed,
            lifecycle.Revision, lifecycle.ExplorationSteps, lifecycle.TacticalTicks, lifecycle.EntryTacticalTicks,
            lifecycle.EncounterCount, lifecycle.Phase, lifecycle.Abilities, lifecycle.Scenarios, lifecycle.Debts,
            lifecycle.ScenarioId, rewound);
        var changedFields = WorldResourceJourneyTests.ReadFrame(final);
        changedFields[21] = EncounterLifecycleCodecV1.Encode(changedLifecycle);
        var badRandom = WorldResourceJourneyTests.WriteFrame(changedFields);
        var randomError = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(badRandom, anchor));
        Assert.Contains("randomness", randomError.InnerException!.Message);

        changedFields = WorldResourceJourneyTests.ReadFrame(final);
        changedFields[14] = QuestCodecV1.Encode(new QuestSnapshot(state.WorldId, state.BranchId, 0, state.Quests.Definitions, []));
        var missingReward = WorldResourceJourneyTests.WriteFrame(changedFields);
        var rewardError = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(missingReward, anchor));
        Assert.Contains("quest", rewardError.InnerException!.Message, StringComparison.OrdinalIgnoreCase);

        var replay = new CareWorldSession(fixture.Genesis);
        foreach (var batch in session.Batches) replay.Commit(batch);
        Assert.Equal(final, WorldSnapshotCodec.Encode(replay.ReadStateForHost()));
        Assert.Equal(session.Archives.Select(value => value.Reference), replay.Archives.Select(value => value.Reference));
        for (int index = 0; index < session.Archives.Count; index++)
            Assert.Equal(session.Archives[index].Encode(), replay.Archives[index].Encode());

        if (Environment.GetEnvironmentVariable("OPENFANTASY_RESOURCE_ARCHIVE_EVIDENCE") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "genesis.ofc"), WorldSnapshotCodec.Encode(fixture.Genesis));
            for (int index = 0; index < session.Archives.Count; index++)
                File.WriteAllBytes(Path.Combine(directory, $"segment-{index}.ofc"), session.Archives[index].Encode());
            File.WriteAllBytes(Path.Combine(directory, "continued.ofc"), final);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_care_and_later_treatment_preserve_original_defeat_across_retrieved_boundaries(bool crafted)
    {
        var fixture = new CareWorldFixture(crafted);
        var session = new CareWorldSession(fixture.Genesis);
        var defeated = fixture.PrepareDefeat(session);
        var acknowledged = CareWorldFixture.Commit(session, fixture.Batch(defeated, WorldBatchKind.Care,
            [fixture.Acknowledge(defeated)])).Candidate;
        var first = WorldResourceArchiveSegment.Seal(acknowledged);
        var firstRetrieved = WorldResourceArchiveSegment.Decode(first.Reference, first.Encode());
        var continued = Continue(acknowledged, firstRetrieved.Anchor);
        var restored = WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(continued), firstRetrieved.Anchor);
        Assert.Equal(acknowledged.Care!.Pending!.AcknowledgmentId, restored.Care!.Pending!.AcknowledgmentId);
        var afterCut = new CareWorldSession(restored, session.OccupiedIdentities);
        var treated = CareWorldFixture.Commit(afterCut, fixture.Batch(restored, WorldBatchKind.Care,
            [fixture.Administer(restored)])).Candidate;
        Assert.Equal(40, treated.Progression!.GetActor(fixture.Player).CurrentHp);
        Assert.Single(treated.ResourceHistory!.History.Batches);
        Assert.Single(treated.Production!.ConsumedItems);
        Assert.Equal(0U, treated.EncounterActions!.LastResolution!.TargetHpAfter);

        var second = WorldResourceArchiveSegment.Seal(treated);
        var secondRetrieved = WorldResourceArchiveSegment.Decode(second.Reference, second.Encode(), firstRetrieved.Anchor);
        var twiceContinued = Continue(treated, secondRetrieved.Anchor);
        var resumed = new CareWorldSession(twiceContinued, afterCut.OccupiedIdentities);
        var after = twiceContinued;
        for (int step = 0; after.ResourceHistory!.History.Batches.Count == 0; step++)
        {
            Assert.True(step < 128);
            after = fixture.NextCombatBoundary(resumed, playerAttacks: true);
        }
        Assert.Equal(treated.Care!.ContentHash, after.Care!.ContentHash);
        Assert.Equal(treated.Production.ContentHash, after.Production!.ContentHash);
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(
            WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(after), secondRetrieved.Anchor)));
    }

    [Fact]
    public void Archive_references_reject_wrong_scope_ordinal_sequence_hash_and_altered_bytes()
    {
        var fixture = new CareWorldFixture();
        Assert.Throws<ArgumentException>(() => WorldResourceArchiveSegment.Seal(fixture.Genesis));
        var world = fixture.PrepareVictory(new(fixture.Genesis));
        var segment = WorldResourceArchiveSegment.Seal(world);
        var original = segment.Reference;
        foreach (var reference in new[]
        {
            new WorldResourceArchiveReference(new EntityId(0xabc, 1), original.BranchId, 0, original.BoundarySequence, original.BoundaryHash),
            new WorldResourceArchiveReference(original.WorldId, new EntityId(0xabc, 2), 0, original.BoundarySequence, original.BoundaryHash),
            new WorldResourceArchiveReference(original.WorldId, original.BranchId, 1, original.BoundarySequence, original.BoundaryHash),
            new WorldResourceArchiveReference(original.WorldId, original.BranchId, 0, original.BoundarySequence + 1, original.BoundaryHash),
            new WorldResourceArchiveReference(original.WorldId, original.BranchId, 0, original.BoundarySequence, ContentHash.Compute("wrong"u8))
        }) Assert.Throws<FormatException>(() => WorldResourceArchiveSegment.Decode(reference, segment.Encode()));
        byte[] changed = segment.Encode();
        changed[^1] ^= 1;
        Assert.Throws<FormatException>(() => WorldResourceArchiveSegment.Decode(original, changed));
        Assert.Equal(original.BoundaryHash, ContentHash.Compute(segment.Encode()));
        Assert.Throws<FormatException>(() => WorldResourceArchiveSegment.Decode(original, segment.Encode().AsSpan(0, 10)));

        var continued = Continue(world, segment.Anchor);
        Assert.Throws<ArgumentException>(() => WorldResourceHistoryCodecV1.Encode(continued.ResourceHistory!));
        Assert.Throws<InvalidOperationException>(() => new WorldStepProcessor().Decide(continued,
            fixture.Batch(continued, WorldBatchKind.AdvancingStep)));
    }

    private static WorldSnapshot Continue(WorldSnapshot world, WorldResourceArchiveAnchor anchor) =>
        WorldResourceJourneyTests.Copy(world, new WorldResourceHistory(world.AdmissionVersion,
            new ActorResourceHistory(world.Progression!, world.Revision, []), [], anchor));
}
