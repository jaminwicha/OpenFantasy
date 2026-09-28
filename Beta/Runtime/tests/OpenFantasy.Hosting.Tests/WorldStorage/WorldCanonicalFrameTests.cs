using System.Reflection;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>
/// A prepared candidate carries the canonical bytes of exactly its own root. Every expectation is
/// taken from the unchanged snapshot encoder over actual fixture worlds and real accepted commands.
/// </summary>
public sealed class WorldCanonicalFrameTests
{
    [Fact]
    public void A_captured_frame_reproduces_the_unchanged_encoder_and_survives_mutated_exports()
    {
        var world = new CoordinatedRecraftJourney().Genesis;
        byte[] expected = WorldSnapshotCodec.Encode(world);
        var frame = WorldCanonicalFrame.Capture(world);

        // A consumer can change an export before the lazy digest is ever computed.
        byte[] firstExport = frame.ToArray();
        Array.Fill(firstExport, (byte)0xff);
        Assert.Equal(expected, frame.ToArray());
        Assert.Same(world, frame.World);
        Assert.Equal(expected.Length, frame.ByteLength);
        Assert.Equal(expected, frame.ToArray());
        Assert.True(frame.Matches(expected));
        Assert.Equal(ContentHash.Compute(expected), frame.ContentHash);
        Assert.Equal(world.ContentHash, frame.ContentHash);

        byte[] exported = frame.ToArray();
        Assert.NotSame(exported, frame.ToArray());
        for (int index = 0; index < exported.Length; index++) exported[index] ^= 0xff;
        Assert.False(frame.Matches(exported));
        Assert.Equal(expected, frame.ToArray());
        Assert.Equal(expected.Length, frame.ByteLength);
        Assert.Equal(ContentHash.Compute(expected), frame.ContentHash);
        Assert.Equal(expected, WorldSnapshotCodec.Encode(frame.World));
        Assert.Equal(expected, WorldCanonicalFrame.Capture(world).ToArray());
    }

    [Fact]
    public void A_legacy_candidate_without_a_frame_uses_the_original_durable_encoding_path()
    {
        var world = WorldServiceFixture.Genesis();
        var batch = WorldServiceFixture.Step(world, actions: [WorldServiceFixture.Give(world)]);
        var prepared = new WorldCommitPreparation().Prepare(world, world.IdentityIndex,
            world.ContentHash, batch, _ => false);

        Assert.Null(prepared.Decision.CandidateFrame);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(prepared.Decision.Receipts).Status);
        byte[] expected = WorldSnapshotCodec.Encode(prepared.Decision.Candidate);
        Assert.Equal(expected, prepared.CandidateBytes);
        Assert.Equal(ContentHash.Compute(expected), prepared.Stored.AfterHash);

        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, world))
        {
            var committed = authority.Commit(batch);
            Assert.Null(committed.CandidateFrame);
            Assert.Equal(expected, WorldSnapshotCodec.Encode(committed.Candidate));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(expected, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    [Fact]
    public void A_frame_binds_only_its_exact_root_and_a_decision_refuses_the_frame_of_an_equal_twin()
    {
        var world = new CoordinatedRecraftJourney().Genesis;
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        var twin = WorldSnapshotCodec.DecodeCoordinated(bytes, null);
        Assert.NotSame(world, twin);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(twin));

        var frame = WorldCanonicalFrame.Capture(world);
        Assert.True(frame.IsBoundTo(world));
        // Identical canonical content and hash still name a different root.
        Assert.Equal(twin.ContentHash, frame.ContentHash);
        Assert.False(frame.IsBoundTo(twin));
        Assert.False(WorldCanonicalFrame.Capture(twin).IsBoundTo(world));

        var constructor = Assert.Single(typeof(WorldStepDecision).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic));
        object?[] Arguments(WorldSnapshot candidate) => [candidate, Array.Empty<WorldActionReceipt>(),
            Array.Empty<WorldCommittedIdentity>(), null, null, null, frame];
        var accepted = Assert.IsType<WorldStepDecision>(constructor.Invoke(Arguments(world)));
        Assert.Same(frame, accepted.CandidateFrame);
        Assert.Same(world, accepted.Candidate);
        var refusal = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(Arguments(twin)));
        Assert.IsType<InvalidOperationException>(refusal.InnerException);
    }

    [Fact]
    public void Commit_preparation_stores_the_captured_bytes_and_owns_a_copy_independent_of_the_frame()
    {
        var journey = new CoordinatedRecraftJourney();
        var state = journey.World;
        var command = journey.Craft(journey.Actors[0]);
        var prepared = new WorldCommitPreparation().Prepare(state, journey.Genesis.IdentityIndex,
            state.ContentHash, journey.Batch(command), _ => false);

        var decision = prepared.Decision;
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
        var frame = AssertExactFrame(decision);
        byte[] expected = WorldSnapshotCodec.Encode(decision.Candidate);
        Assert.Equal(expected, prepared.CandidateBytes);
        Assert.Equal(state.ContentHash, prepared.Stored.BeforeHash);
        Assert.Equal(ContentHash.Compute(expected), prepared.Stored.AfterHash);
        Assert.Equal(decision.Candidate.Revision, prepared.Stored.Sequence);

        // Neither owner can reach the other's bytes: an exported copy cannot alter the stored row,
        // and the mutable stored row cannot alter a later export or the frame's digest.
        byte[] exported = frame.ToArray();
        Array.Fill(exported, (byte)0);
        Assert.Equal(expected, prepared.CandidateBytes);
        Array.Fill(prepared.CandidateBytes, (byte)0xff);
        Assert.Equal(expected, frame.ToArray());
        Assert.True(frame.Matches(expected));
        Assert.Equal(ContentHash.Compute(expected), frame.ContentHash);
        Assert.Equal(ContentHash.Compute(expected), prepared.Stored.AfterHash);
    }

    [Fact]
    public void An_automatic_resource_archive_keeps_the_candidate_frame_through_publication_and_replay()
    {
        var fixture = new OrdinaryCareArchiveFixture();
        using var files = new StorageFiles();
        byte[] expected;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var before = fixture.Prepare255(new CareWorldSession(authority));
            var aid = fixture.Journey.Command(before, fixture.Journey.Helper, fixture.Aid(before));
            var unowned = fixture.Journey.Command(before, fixture.Journey.Player, fixture.UnownedSelfDose(before));
            var decision = authority.Commit(fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [aid, unowned]));

            var segment = Assert.IsType<WorldResourceArchiveSegment>(decision.SealedResourceSegment);
            Assert.Equal(CommandReceiptStatus.Applied, decision.Receipts.Single(value => value.CommandId == aid.CommandId).Status);
            var frame = AssertExactFrame(decision);
            expected = WorldSnapshotCodec.Encode(decision.Candidate);
            // The wrapper added the closing root beside the candidate; the frame still names the
            // continued candidate alone, never the sealed boundary it was prepared from.
            Assert.Equal(WorldSnapshotCodec.Encode(before), segment.Encode());
            Assert.False(frame.IsBoundTo(before));
            Assert.False(frame.Matches(segment.Encode()));
            Assert.Same(decision.Candidate, authority.ReadStateForHost());

            byte[] exported = frame.ToArray();
            Array.Fill(exported, (byte)0);
            Assert.Equal(expected, frame.ToArray());
            Assert.Equal(ContentHash.Compute(expected), frame.ContentHash);
        }

        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(expected, database.ReadHead().State);
            Assert.Equal(ContentHash.Compute(expected), database.ReadJournal().Last().AfterHash);
            Assert.Single(database.ReadResourceArchives());
        }
        // Reopening replays every journal entry, including all earlier framed candidates.
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(expected, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        }
    }

    [Fact]
    public void An_automatic_joint_archive_keeps_the_frame_of_its_continued_candidate()
    {
        var journey = new CoordinatedRecraftJourney();
        var actor = journey.Actors[0];
        WorldStepDecision? cut = null;
        WorldSnapshot? closing = null;
        for (int operation = 0; operation < 300 && cut is null; operation++)
        {
            var before = journey.World;
            var command = journey.Craft(actor);
            var decision = journey.Commit(journey.Batch(command));
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
            var carried = Assert.IsType<WorldCanonicalFrame>(decision.CandidateFrame);
            Assert.True(carried.IsBoundTo(decision.Candidate));
            if (decision.SealedMaterialCareSegment is not null)
            {
                cut = decision;
                closing = before;
            }
        }

        Assert.NotNull(cut);
        Assert.NotNull(closing);
        var segment = Assert.IsType<WorldMaterialCareArchiveSegment>(cut.SealedMaterialCareSegment);
        Assert.Null(cut.SealedResourceSegment);
        var frame = AssertExactFrame(cut);
        Assert.Equal(WorldSnapshotCodec.Encode(closing), segment.Encode());
        Assert.False(frame.IsBoundTo(closing));
        Assert.False(frame.IsBoundTo(segment.Continued));
        Assert.False(frame.Matches(segment.Encode()));
        Assert.NotNull(cut.Candidate.Coordination!.Anchor);
    }

    [Fact]
    public void A_reused_movement_root_carries_its_own_frame_and_never_an_earlier_preflight()
    {
        var fixture = new CareWorldFixture();
        var state = fixture.Genesis;
        var processor = new WorldStepProcessor();
        ulong identity = 1;
        WorldStepDecision Step(params WorldMovementInput[] movement) => processor.Decide(state,
            new WorldStepBatch(new EntityId(0xf4a3e00000000000, identity++), state.WorldId, state.BranchId,
                state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, movement, [],
                state.ProfileVersion), _ => false);
        WorldMovementInput FirstAdmitted(EntityId actor)
        {
            (long X, long Y)[] directions = [(32, 0), (-32, 0), (0, 32), (0, -32)];
            foreach (var (x, y) in directions)
            {
                var input = new WorldMovementInput(fixture.DeliveryFor(actor), actor, x, y);
                if (Step(input).Candidate.Space.GetActor(actor).Feet != state.Space.GetActor(actor).Feet) return input;
            }
            throw new InvalidOperationException("The fixture actor has no admitted cardinal step.");
        }

        // No movement: the final root is constructed and encoded once at the end of the step.
        var still = Step();
        AssertExactFrame(still);
        Assert.Equal(state.Space.Actors.Select(actor => actor.Feet), still.Candidate.Space.Actors.Select(actor => actor.Feet));

        var helper = FirstAdmitted(fixture.Helper);
        var player = FirstAdmitted(fixture.Player);
        var helperOnly = Step(helper);
        var playerOnly = Step(player);
        var helperFrame = AssertExactFrame(helperOnly);
        var playerFrame = AssertExactFrame(playerOnly);

        // Two admitted moves: the first preflight root is superseded by the second, whose exact
        // admitted root becomes the final candidate together with its own frame.
        var both = Step(helper, player);
        var frame = AssertExactFrame(both);
        Assert.NotEqual(state.Space.GetActor(fixture.Helper).Feet, both.Candidate.Space.GetActor(fixture.Helper).Feet);
        Assert.NotEqual(state.Space.GetActor(fixture.Player).Feet, both.Candidate.Space.GetActor(fixture.Player).Feet);
        Assert.False(frame.Matches(helperFrame.ToArray()));
        Assert.False(frame.Matches(playerFrame.ToArray()));
        Assert.False(frame.IsBoundTo(helperOnly.Candidate));
        Assert.False(frame.IsBoundTo(playerOnly.Candidate));
        Assert.False(helperFrame.IsBoundTo(both.Candidate));
        Assert.False(frame.IsBoundTo(state));
    }

    [Fact]
    public void Movement_lifecycle_encounter_and_care_decisions_carry_the_frame_of_their_final_candidate()
    {
        var fixture = new CareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        var defeated = fixture.PrepareDefeat(session);

        // A stale acknowledgment is refused: the published root is the unchanged-care fallback.
        var stale = fixture.Command(defeated, fixture.Player, new AcknowledgeCareIntent(fixture.Agreement,
            defeated.EncounterActions!.Scope.EncounterId, defeated.EncounterLifecycle!.Revision + 1,
            defeated.EncounterActions.ContentHash));
        var refused = session.Commit(fixture.Batch(defeated, WorldBatchKind.Care, [stale]));
        var refusal = Assert.Single(refused.Receipts);
        Assert.Equal(CommandReceiptStatus.Rejected, refusal.Status);
        Assert.Equal(WorldActionRejection.RevisionConflict, refusal.Rejection);
        var refusedFrame = AssertExactFrame(refused);
        Assert.False(refusedFrame.IsBoundTo(defeated));
        Assert.Equal(defeated.Revision + 1, refused.Candidate.Revision);
        Assert.Equal(defeated.Care!.ContentHash, refused.Candidate.Care!.ContentHash);
        Assert.Null(refused.Candidate.Care.Pending);

        var ready = session.ReadStateForHost();
        var acknowledged = session.Commit(fixture.Batch(ready, WorldBatchKind.Care, [fixture.Acknowledge(ready)]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(acknowledged.Receipts).Status);
        AssertExactFrame(acknowledged);
        Assert.NotNull(acknowledged.Candidate.Care!.Pending);

        var pending = session.ReadStateForHost();
        var treated = session.Commit(fixture.Batch(pending, WorldBatchKind.Care, [fixture.Administer(pending)]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(treated.Receipts).Status);
        var treatedFrame = AssertExactFrame(treated);
        Assert.Null(treated.Candidate.Care!.Pending);
        Assert.Single(treated.Candidate.Care.Treatments);
        Assert.False(treatedFrame.IsBoundTo(pending));

        // Replay the actual accepted journey: every kind of boundary prepares one exact frame.
        var processor = new WorldStepProcessor();
        HashSet<EntityId> occupied = [];
        HashSet<WorldBatchKind> kinds = [];
        bool moved = false;
        var state = fixture.Genesis;
        foreach (var batch in session.Batches)
        {
            var decision = processor.Decide(state, batch, occupied.Contains);
            AssertExactFrame(decision);
            foreach (var reserved in decision.Identities) Assert.True(occupied.Add(reserved.Identity));
            kinds.Add(batch.Kind);
            moved |= batch.Movement.Count != 0;
            state = decision.Candidate;
        }
        Assert.True(moved);
        Assert.Contains(WorldBatchKind.AdvancingStep, kinds);
        Assert.Contains(WorldBatchKind.EncounterLifecycle, kinds);
        Assert.Contains(WorldBatchKind.EncounterDecision, kinds);
        Assert.Contains(WorldBatchKind.EncounterResolution, kinds);
        Assert.Contains(WorldBatchKind.Care, kinds);
        Assert.Equal(WorldSnapshotCodec.Encode(session.ReadStateForHost()), WorldSnapshotCodec.Encode(state));
    }

    private static WorldCanonicalFrame AssertExactFrame(WorldStepDecision decision)
    {
        var frame = Assert.IsType<WorldCanonicalFrame>(decision.CandidateFrame);
        Assert.True(frame.IsBoundTo(decision.Candidate));
        Assert.Same(decision.Candidate, frame.World);
        byte[] expected = WorldSnapshotCodec.Encode(decision.Candidate);
        Assert.Equal(expected.Length, frame.ByteLength);
        Assert.True(frame.Matches(expected));
        Assert.Equal(expected, frame.ToArray());
        Assert.Equal(ContentHash.Compute(expected), frame.ContentHash);
        return frame;
    }
}
