using System.Buffers.Binary;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class EventSettlementWorldTests
{
    [Fact]
    public void Fresh_admission_selects_an_owned_new_interpretation_without_changing_previous_world_bytes()
    {
        WorldSnapshot common = EventSettlementFixture.Create17();
        byte[] previousBytes = WorldSnapshotCodec.Encode(common);
        WorldSnapshot events = WorldEventSettlementAdmission.CreateGenesis(common);

        Assert.Equal((ushort)17, common.ProfileVersion);
        Assert.Equal(ConstructionDescentMode.CommonTrajectory, common.DescentMode);
        Assert.Equal((ushort)18, events.ProfileVersion);
        Assert.Equal(ConstructionDescentMode.EventOrdered, events.DescentMode);
        Assert.NotEqual(common.AdmissionVersion, events.AdmissionVersion);
        Assert.NotEqual(common.ContentHash, events.ContentHash);
        Assert.Same(common.Inventory, events.Inventory);
        Assert.Same(common.ConstructionPhysical, events.ConstructionPhysical);
        Assert.Same(common.StructuralPolicy, events.StructuralPolicy);
        Assert.Same(common.CoupledSettlementPolicy, events.CoupledSettlementPolicy);
        Assert.Equal(0UL, events.Revision);
        Assert.Equal(0UL, events.AdvancingSteps);
        Assert.Equal(previousBytes, WorldSnapshotCodec.Encode(common));
        Assert.Equal(previousBytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(previousBytes)));
    }

    [Fact]
    public void Admission_rejects_played_held_older_and_already_selected_worlds()
    {
        WorldSnapshot common = EventSettlementFixture.Create17();
        WorldSnapshot played = new WorldStepProcessor().Decide(common, EventSettlementFixture.Step(common, 1)).Candidate;
        var dialogue = new DialogueSession(EventSettlementFixture.Id(30), EventSettlementFixture.Player,
            AdmissionFixture.Id(11), AdmissionFixture.Id(92), EventSettlementFixture.Id(31), common.Calendar);
        WorldSnapshot held = EventSettlementFixture.Copy(common, [dialogue]);
        WorldSnapshot older = EventSettlementFixture.Copy(common, coupled: false);

        Assert.Equal((ushort)16, older.ProfileVersion);
        Assert.True(held.HasReadingHold);
        foreach (WorldSnapshot rejected in new[] { played, held, older, StepFixture.Create(),
                     WorldEventSettlementAdmission.CreateGenesis(common) })
        {
            Assert.Throws<ArgumentException>(() => WorldEventSettlementAdmission.CreateGenesis(rejected));
        }
    }

    [Fact]
    public void Event_mode_cannot_exist_without_the_coupled_policy_or_as_an_undefined_enum_value()
    {
        WorldSnapshot common = EventSettlementFixture.Create17();
        Assert.Throws<ArgumentException>(() => EventSettlementFixture.Copy(common,
            mode: ConstructionDescentMode.EventOrdered, coupled: false));
        Assert.Throws<ArgumentException>(() => EventSettlementFixture.Copy(common, mode: (ConstructionDescentMode)0));
        Assert.Throws<ArgumentException>(() => EventSettlementFixture.Copy(common, mode: (ConstructionDescentMode)3));
    }

    [Fact]
    public void Version18_roundtrips_and_real_ordinary_movement_keeps_the_selected_mode_and_pacing()
    {
        WorldSnapshot state = WorldEventSettlementAdmission.CreateGenesis(EventSettlementFixture.Create17());
        var initialCalendar = state.Calendar;
        for (ulong ordinal = 1; ordinal <= 2; ordinal++)
        {
            byte[] snapshot = WorldSnapshotCodecV18.Encode(state);
            state = WorldSnapshotCodec.Decode(snapshot);
            Assert.Equal(snapshot, WorldSnapshotCodecV18.Encode(state));
            Assert.Equal(ConstructionDescentMode.EventOrdered, state.DescentMode);
            Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV17.Encode(state));

            WorldStepBatch step = EventSettlementFixture.Step(state, ordinal, move: true);
            byte[] batch = WorldStepCodecV18.Encode(step);
            Assert.Equal(batch, WorldStepCodec.Encode(WorldStepCodec.Decode(batch)));
            Assert.Throws<ArgumentException>(() => WorldStepCodecV17.Encode(step));
            WorldStepDecision decision = new WorldStepProcessor().Decide(state, WorldStepCodec.Decode(batch));
            Assert.Empty(decision.Receipts);
            state = decision.Candidate;
            Assert.Equal((ushort)18, state.ProfileVersion);
            Assert.Equal(ConstructionDescentMode.EventOrdered, state.DescentMode);
            Assert.Equal(ordinal, state.Revision);
            Assert.Equal(ordinal, state.AdvancingSteps);
            Assert.Equal(128L + (long)ordinal * 32, state.Space.GetActor(EventSettlementFixture.Player).Feet.X);
            Assert.Equal(ordinal, state.Space.GetActor(EventSettlementFixture.Player).Revision);
            Assert.Null(state.PendingExcavation);
        }
        Assert.Equal(initialCalendar.AddMilliseconds(3600), state.Calendar);
        Assert.Equal(state.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(state)).ContentHash);
    }

    [Fact]
    public void Saved_version18_requires_its_exact_mode_field_and_cannot_be_downgraded_by_retagging()
    {
        WorldSnapshot world = WorldEventSettlementAdmission.CreateGenesis(EventSettlementFixture.Create17());
        byte[] bytes = WorldSnapshotCodecV18.Encode(world);
        foreach (ulong mode in new[] { 0UL, 1UL, 3UL, ulong.MaxValue })
        {
            byte[] changed = NumberField(bytes, 23, mode);
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(changed));
            Assert.Throws<FormatException>(() => WorldSnapshotCodecV18.Decode(changed));
        }
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(NumberField(bytes, 1, 17)));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
    }

    [Fact]
    public void Event_cursors_require_the_new_codec_and_cannot_be_reinterpreted_as_legacy_common_motion()
    {
        WorldSnapshot common = EventSettlementFixture.Create17();
        WorldSnapshot events = WorldEventSettlementAdmission.CreateGenesis(common);
        TerrainRegionSnapshot[] regions = EventSettlementFixture.CandidateRegions(events);
        ConstructionSettlementResolution cursor = ConstructionSettlementResolution.BeginEventOrdered(events, regions);
        ConstructionSettlementResolution legacy = ConstructionSettlementResolution.BeginCoupled(common, regions);

        Assert.Throws<ArgumentException>(() => ConstructionSettlementResolutionCodecV1.Encode(cursor));
        Assert.Throws<ArgumentException>(() => ConstructionSettlementResolutionCodecV2.Encode(cursor));
        Assert.Throws<ArgumentException>(() => ConstructionSettlementResolutionCodecV3.Encode(legacy));
        Assert.Throws<ArgumentException>(() => ConstructionSettlementResolution.BeginCoupled(events, regions));
        Assert.Throws<ArgumentException>(() => ConstructionSettlementResolution.BeginEventOrdered(common, regions));
        Assert.Throws<FormatException>(() => Decode(ConstructionSettlementResolutionCodecV3.Encode(cursor), common, regions));

        byte[] previous = ConstructionSettlementResolutionCodecV2.Encode(legacy);
        Assert.Equal(previous, ConstructionSettlementResolutionCodecV2.Encode(ConstructionSettlementResolutionCodecV2.Decode(
            previous, common, regions, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public void Every_new_cursor_prefix_resumes_the_known_actor_drop_without_publishing_the_candidate_terrain()
    {
        WorldSnapshot basis = WorldEventSettlementAdmission.CreateGenesis(EventSettlementFixture.Create17());
        TerrainRegionSnapshot[] regions = EventSettlementFixture.CandidateRegions(basis);
        ConstructionSettlementResolution state = ConstructionSettlementResolution.BeginEventOrdered(basis, regions);
        Assert.False(state.IsComplete);
        byte[] expected = ConstructionSettlementResolutionCodecV3.Encode(Finish(state));
        int prefixes = 0;
        while (true)
        {
            Assert.InRange(prefixes++, 0, state.MaximumTotalWorkUnits + 1);
            byte[] bytes = ConstructionSettlementResolutionCodecV3.Encode(state);
            ConstructionSettlementResolution restored = Decode(bytes, basis, regions);
            Assert.Equal(bytes, ConstructionSettlementResolutionCodecV3.Encode(restored));
            Assert.Equal(expected, ConstructionSettlementResolutionCodecV3.Encode(Finish(restored)));
            if (state.IsComplete) { break; }
            Assert.Throws<InvalidOperationException>(() => state.Result);
            state = restored.Advance(1);
            Assert.Equal(bytes, ConstructionSettlementResolutionCodecV3.Encode(restored));
        }
        Assert.False(state.IsFailed, state.FailureReason);
        Assert.Equal(0L, state.Result.Space.GetActor(EventSettlementFixture.Player).Feet.Z);
        Assert.Equal(1UL, state.Result.Space.GetActor(EventSettlementFixture.Player).Revision);
        Assert.Same(basis.ConstructionPhysical, state.Result.Physical);
        Assert.Equal(256L, basis.Space.GetActor(EventSettlementFixture.Player).Feet.Z);
        Assert.False(Assert.Single(basis.Space.Regions).GetCell(EventSettlementFixture.Removed).IsExcavated);
        Assert.Equal(0UL, Assert.Single(basis.Excavation!.Sites).CompletedWork);
        Assert.Equal(0UL, basis.Revision);
    }

    [Fact]
    public void New_cursor_rejects_changed_basis_pin_impossible_work_status_size_and_canceled_replay()
    {
        WorldSnapshot basis = WorldEventSettlementAdmission.CreateGenesis(EventSettlementFixture.Create17());
        TerrainRegionSnapshot[] regions = EventSettlementFixture.CandidateRegions(basis);
        ConstructionSettlementResolution state = ConstructionSettlementResolution.BeginEventOrdered(basis, regions);
        byte[] bytes = ConstructionSettlementResolutionCodecV3.Encode(state);
        byte[] wrongPin = (byte[])bytes.Clone();
        wrongPin[FieldOffset(wrongPin, 1)] ^= 1;
        Assert.Throws<FormatException>(() => Decode(wrongPin, basis, regions));
        foreach (ulong work in new[] { checked((ulong)state.MaximumTotalWorkUnits + 1), ulong.MaxValue })
        {
            Assert.Throws<FormatException>(() => Decode(NumberField(bytes, 2, work), basis, regions));
        }
        foreach (ulong status in new[] { 1UL, 2UL, 3UL })
        {
            Assert.Throws<FormatException>(() => Decode(NumberField(bytes, 3, status), basis, regions));
        }
        WorldSnapshot played = new WorldStepProcessor().Decide(basis, EventSettlementFixture.Step(basis, 20)).Candidate;
        Assert.Throws<FormatException>(() => Decode(bytes, played, regions));
        Assert.Throws<FormatException>(() => Decode(bytes, basis, basis.Space.Regions));
        Assert.Throws<FormatException>(() => Decode([.. bytes, 0], basis, regions));
        Assert.Throws<FormatException>(() => Decode(bytes[..^1], basis, regions));
        Assert.Throws<FormatException>(() => Decode(new byte[ConstructionSettlementResolutionCodecV3.MaximumEncodedByteLength + 1], basis, regions));
        Assert.Throws<OperationCanceledException>(() => ConstructionSettlementResolutionCodecV3.Decode(bytes, basis, regions,
            new CancellationToken(canceled: true)));
        Assert.Equal(bytes, ConstructionSettlementResolutionCodecV3.Encode(Decode(bytes, basis, regions)));
    }

    [Fact]
    public void A_no_motion_candidate_has_a_canonical_zero_work_terminal_event_cursor()
    {
        WorldSnapshot basis = WorldEventSettlementAdmission.CreateGenesis(EventSettlementFixture.Create17());
        ConstructionSettlementResolution empty = ConstructionSettlementResolution.BeginEventOrdered(basis, basis.Space.Regions);
        Assert.True(empty.IsComplete);
        Assert.False(empty.IsFailed, empty.FailureReason);
        Assert.Equal(0UL, empty.CompletedWorkUnits);
        Assert.Equal(0, empty.MaximumTotalWorkUnits);
        byte[] bytes = ConstructionSettlementResolutionCodecV3.Encode(empty);
        Assert.Equal(bytes, ConstructionSettlementResolutionCodecV3.Encode(Decode(bytes, basis, basis.Space.Regions)));
        Assert.Throws<FormatException>(() => Decode(NumberField(bytes, 2, 1), basis, basis.Space.Regions));
    }

    private static ConstructionSettlementResolution Finish(ConstructionSettlementResolution state)
    {
        int calls = 0;
        while (!state.IsComplete)
        {
            Assert.InRange(calls++, 0, state.MaximumTotalWorkUnits + 1);
            state = state.Advance(256);
        }
        return state;
    }

    private static ConstructionSettlementResolution Decode(byte[] bytes, WorldSnapshot basis,
        IEnumerable<TerrainRegionSnapshot> regions) => ConstructionSettlementResolutionCodecV3.Decode(bytes, basis, regions,
            TestContext.Current.CancellationToken);

    private static int FieldOffset(byte[] bytes, int ordinal)
    {
        int offset = 12;
        for (int index = 0; index < ordinal; index++)
        {
            offset += 4 + checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
        }
        return offset + 4;
    }

    private static byte[] NumberField(byte[] bytes, int ordinal, ulong value)
    {
        byte[] changed = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt64BigEndian(changed.AsSpan(FieldOffset(changed, ordinal)), value);
        return changed;
    }
}
