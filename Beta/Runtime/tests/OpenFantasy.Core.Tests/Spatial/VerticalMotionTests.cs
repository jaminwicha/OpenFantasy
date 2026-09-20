using System.Buffers.Binary;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

/// <summary>Supplied geometric scenes prove contact ordering, not live construction, impact safety or publication.</summary>
public sealed class VerticalMotionTests
{
    [Fact]
    public void Independent_bodies_land_at_different_distances_without_dragging_the_first_below_its_floor()
    {
        SpatialStructure low = Slab(1, Box(0, 0, 512, 256, 256, 576));
        SpatialStructure high = Slab(2, Box(512, 0, 1024, 768, 256, 1088));
        VerticalMotionResolution result = Finish(Begin([low, high]));

        AssertLanded(result, low, 256, Box(0, 0, 256, 256, 256, 320));
        AssertLanded(result, high, 768, Box(512, 0, 256, 768, 256, 320));
        Assert.Equal(new[] { 256L, 768L }, result.Result.Events.Select(value => value.DropSubunits));
        Assert.Equal(new[] { low.StructureId, high.StructureId }, result.Result.Events.SelectMany(value => value.BodyIds));
        Assert.Equal(512L, low.Boxes[0].Minimum.Z);
        Assert.Equal(1024L, high.Boxes[0].Minimum.Z);
    }

    [Fact]
    public void A_newly_stopped_lower_slab_catches_the_later_upper_slab_and_its_rider()
    {
        var (scene, lower, upper, rider) = SeparatedStack();
        VerticalMotionResolution result = Finish(VerticalMotionResolution.Begin(scene));

        AssertLanded(result, lower, 256, Box(0, 0, 256, 256, 256, 320));
        AssertLanded(result, upper, 704, Box(0, 0, 320, 256, 256, 384));
        ActorPresence landed = Assert.Single(result.Result.Actors);
        Assert.Equal(704L, result.Result.Drops[rider.ActorId]);
        Assert.Equal(new SubunitPosition(128, 128, 384), landed.Feet);
        Assert.Equal(rider.Revision, landed.Revision);
        Assert.Equal(rider.Binding, landed.Binding);
        Assert.Equal(rider.HomePlaceId, landed.HomePlaceId);
        Assert.Equal(rider.Condition.ComputeContentHash(), landed.Condition.ComputeContentHash());
        Assert.Equal(new SubunitPosition(128, 128, 1088), rider.Feet);
        Assert.Equal(new[] { 256L, 704L }, result.Result.Events.Select(value => value.DropSubunits).Distinct());
        Assert.Equal(new[] { lower.StructureId, upper.StructureId, rider.ActorId }.Order(),
            result.Result.Events.SelectMany(value => value.BodyIds).Order());
    }

    [Fact]
    public void An_unknown_distant_floor_does_not_hide_a_newly_stopped_support_above_it()
    {
        TerrainRegionSnapshot known = Region(new(new(0, 0, 1), 2, 1, 6));
        SpatialStructure floor = Slab(3, Box(0, 0, 256, 128, 256, 512));
        // A supplied rigid overhang catches the upper body outside the stationary pedestal's XY.
        // Without the new stopped overhang, that upper probe reaches unavailable cell layer zero.
        var low = new SpatialStructure(SpatialFixture.Id(1001),
            [Box(0, 0, 768, 128, 256, 832), Box(0, 0, 832, 512, 256, 896)],
            [new(new(0, 0, 512000, 256000), 896000)]);
        SpatialStructure high = Slab(2, Box(256, 0, 1280, 512, 256, 1344));
        var scene = Scene([low, high], regions: [known], stationaryStructures: [floor], maximumDrop: 2048);
        var initialProbe = new VerticalDescentQuery(SpatialFixture.Scope, [known], [floor], []);
        Assert.Equal(VerticalDescentStatus.Unavailable, initialProbe.QueryBox(high.Boxes[0], 2048).Status);
        VerticalMotionResolution result = Finish(VerticalMotionResolution.Begin(scene));

        Assert.Equal(256L, result.Result.Drops[low.StructureId]);
        SpatialStructure reached = Assert.Single(result.Result.Structures, value => value.StructureId == low.StructureId);
        Assert.Equal(new[] { Box(0, 0, 512, 128, 256, 576), Box(0, 0, 576, 512, 256, 640) }, reached.Boxes);
        Assert.Equal(low.Boxes.Select(BoxVolume), reached.Boxes.Select(BoxVolume));
        AssertLanded(result, high, 640, Box(256, 0, 640, 512, 256, 704));
        Assert.Equal(new[] { 256L, 640L }, result.Result.Events.Select(value => value.DropSubunits).Distinct());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(16)]
    [InlineData(256)]
    public void Touching_stack_and_rider_stop_together_with_canonical_events_for_any_budget_or_enumeration(int budget)
    {
        SpatialStructure lower = Slab(2, Box(0, 0, 512, 256, 256, 576));
        SpatialStructure upper = Slab(1, Box(0, 0, 576, 256, 256, 640));
        ActorPresence rider = SpatialFixture.Actor(1, new(128, 128, 640), SpatialFixture.Body(64, 64, 128));
        var left = Region(new(new(-1, -1, 0), 1, 3, 8));
        var right = Region(new(new(0, -1, 0), 3, 3, 8));
        var bodies = new[] { VerticalMotionBody.ForStructure(lower), VerticalMotionBody.ForActor(rider),
            VerticalMotionBody.ForStructure(upper) };
        var canonical = new VerticalMotionScene(SpatialFixture.Scope, [left, right], [], [], bodies, 1024);
        var reversed = new VerticalMotionScene(SpatialFixture.Scope, [right, left], [], [], bodies.Reverse(), 1024);
        VerticalMotionResolution expected = Finish(VerticalMotionResolution.Begin(canonical));
        VerticalMotionResolution actual = Finish(VerticalMotionResolution.Begin(reversed), budget);

        Assert.Equal(canonical.InputPin, reversed.InputPin);
        Assert.Equal(VerticalMotionCodecV1.Encode(expected), VerticalMotionCodecV1.Encode(actual));
        AssertLanded(actual, lower, 256, Box(0, 0, 256, 256, 256, 320));
        AssertLanded(actual, upper, 256, Box(0, 0, 320, 256, 256, 384));
        Assert.Equal(256L, actual.Result.Drops[rider.ActorId]);
        Assert.Equal(new SubunitPosition(128, 128, 384), Assert.Single(actual.Result.Actors).Feet);
        Assert.All(actual.Result.Events, value =>
        {
            Assert.Equal(256L, value.DropSubunits);
            Assert.Equal(value.BodyIds.Order(), value.BodyIds);
        });
        Assert.Equal(EventWitness(expected), EventWitness(actual));
        Assert.Equal(new[] { lower.StructureId, upper.StructureId, rider.ActorId }.Order(),
            actual.Result.Events.SelectMany(value => value.BodyIds).Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_head_touching_the_moving_ceiling_is_safe_only_when_both_bodies_reach_their_own_floors_together(bool raisedActorFloor)
    {
        var structure = new SpatialStructure(SpatialFixture.Id(1001),
            [Box(0, 0, 512, 256, 256, 1024), Box(0, 0, 1024, 512, 256, 1088)],
            [new(new(0, 0, 512000, 256000), 1088000)]);
        ActorPresence actor = SpatialFixture.Actor(1, new(384, 128, 512), SpatialFixture.Body(64, 64, 512), revision: 9);
        SpatialStructure[] floors = raisedActorFloor ? [Slab(2, Box(256, 0, 256, 512, 256, 257))] : [];
        VerticalMotionBody[] bodies = [VerticalMotionBody.ForStructure(structure), VerticalMotionBody.ForActor(actor)];
        byte[]? expectedArchive = null;

        foreach (bool reverse in new[] { false, true })
        {
            var scene = new VerticalMotionScene(SpatialFixture.Scope, [Region()], floors, [],
                reverse ? bodies.Reverse() : bodies, 1024);
            VerticalMotionResolution result = Finish(VerticalMotionResolution.Begin(scene), reverse ? 1 : 256);
            byte[] archive = VerticalMotionCodecV1.Encode(result);
            if (expectedArchive is not null) { Assert.Equal(expectedArchive, archive); }
            expectedArchive = archive;

            if (raisedActorFloor)
            {
                AssertFailed(result, VerticalDescentStatus.ActorContact);
                VerticalMotionEvent first = Assert.Single(result.Events);
                Assert.Equal(255L, first.DropSubunits);
                Assert.Equal(actor.ActorId, Assert.Single(first.BodyIds));
            }
            else
            {
                Assert.False(result.IsFailed);
                Assert.Equal(256L, result.Result.Drops[structure.StructureId]);
                Assert.Equal(256L, result.Result.Drops[actor.ActorId]);
                SpatialStructure landedStructure = Assert.Single(result.Result.Structures);
                Assert.Equal(new[] { Box(0, 0, 256, 256, 256, 768), Box(0, 0, 768, 512, 256, 832) }, landedStructure.Boxes);
                ActorPresence landedActor = Assert.Single(result.Result.Actors);
                Assert.Equal(new SubunitPosition(384, 128, 256), landedActor.Feet);
                Assert.Equal(landedStructure.Boxes[1].Minimum.Z,
                    landedActor.Feet.Z + landedActor.Condition.Definition.ClearanceDimensions.HeightSubunits);
                Assert.Equal(actor.Revision, landedActor.Revision);
                Assert.Equal(actor.Condition.ComputeContentHash(), landedActor.Condition.ComputeContentHash());
                VerticalMotionEvent contact = Assert.Single(result.Result.Events);
                Assert.Equal(256L, contact.DropSubunits);
                Assert.Equal(new[] { structure.StructureId, actor.ActorId }.Order(), contact.BodyIds);
            }
        }
        Assert.Equal(new SubunitPosition(384, 128, 512), actor.Feet);
        Assert.Equal(512L, structure.GroundPlaneZ);
    }

    [Fact]
    public void A_stationary_actor_blocks_a_falling_slab_and_never_becomes_a_floor()
    {
        ActorPresence obstacle = SpatialFixture.Actor(2, new(128, 128, 256), SpatialFixture.Body(64, 64, 128));
        SpatialStructure slab = Slab(1, Box(96, 96, 768, 160, 160, 896));
        VerticalMotionResolution result = Finish(Begin([slab], stationaryActors: [obstacle]));

        AssertFailed(result, VerticalDescentStatus.ActorContact);
        Assert.Equal(new SubunitPosition(128, 128, 256), obstacle.Feet);
        Assert.Equal(0UL, obstacle.Revision);
    }

    [Fact]
    public void An_independent_actor_lands_without_advancing_its_authoritative_revision()
    {
        ActorPresence actor = SpatialFixture.Actor(1, new(128, 128, 768), SpatialFixture.Body(64, 64, 128), revision: 11);
        VerticalMotionResolution result = Finish(VerticalMotionResolution.Begin(Scene([], [actor])));
        Assert.False(result.IsFailed);
        Assert.Equal(512L, result.Result.Drops[actor.ActorId]);
        ActorPresence landed = Assert.Single(result.Result.Actors);
        Assert.Equal(new SubunitPosition(128, 128, 256), landed.Feet);
        Assert.Equal(11UL, landed.Revision);
        Assert.Equal(actor.Condition.ComputeContentHash(), landed.Condition.ComputeContentHash());
        Assert.Empty(result.Result.Structures);
        Assert.Equal(actor.ActorId, Assert.Single(Assert.Single(result.Result.Events).BodyIds));
    }

    [Fact]
    public void A_newly_stopped_actor_remains_a_collision_obstacle_for_a_later_actor()
    {
        ActorPresence lower = SpatialFixture.Actor(1, new(128, 128, 512), SpatialFixture.Body(64, 64, 128));
        ActorPresence upper = SpatialFixture.Actor(2, new(128, 128, 1024), lower.Condition.Definition);
        VerticalMotionResolution result = Finish(VerticalMotionResolution.Begin(Scene([], [upper, lower])));
        AssertFailed(result, VerticalDescentStatus.ActorContact);
        Assert.Equal(512L, lower.Feet.Z);
        Assert.Equal(1024L, upper.Feet.Z);
    }

    [Fact]
    public void A_later_body_cannot_pass_through_a_newly_stopped_half_width_support()
    {
        SpatialStructure lower = Slab(1, Box(0, 0, 512, 128, 256, 576));
        SpatialStructure upper = Slab(2, Box(0, 0, 1024, 256, 256, 1088));
        AssertFailed(Finish(Begin([lower, upper])), VerticalDescentStatus.IncompleteSupport);
    }

    [Fact]
    public void A_partial_high_ledge_cannot_be_skipped_for_the_full_lower_terrain_floor()
    {
        var terrain = SpatialFixture.Terrain(keptSolids: [new(0, 0, 1), new(0, 0, 2)]);
        SpatialStructure slab = Slab(1, Box(192, 96, 1024, 320, 160, 1152));
        AssertFailed(Finish(Begin([slab], regions: [terrain])), VerticalDescentStatus.IncompleteSupport);
    }

    [Fact]
    public void Every_member_is_swept_even_when_an_obstacle_misses_the_lowest_member()
    {
        var moving = new SpatialStructure(SpatialFixture.Id(1001),
            [Box(192, 0, 512, 256, 256, 576), Box(0, 0, 768, 64, 256, 832)]);
        SpatialStructure obstruction = Slab(2, Box(0, 0, 640, 64, 256, 704));
        AssertFailed(Finish(Begin([moving], stationaryStructures: [obstruction])), VerticalDescentStatus.IncompleteSupport);
        Assert.Equal(512L, moving.GroundPlaneZ);
        Assert.Equal(2, moving.Boxes.Count);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Landing_uses_exact_bearing_faces_not_just_full_conservative_colliders(long slit, bool expectedFailure)
    {
        var floor = new SpatialStructure(SpatialFixture.Id(1002), [Box(0, 0, 256, 256, 256, 512)],
            [new(new(0, 0, 128000, 256000), 512000),
                new(new(128000 + slit, 0, 256000, 256000), 512000)]);
        SpatialStructure moving = Slab(1, Box(0, 0, 768, 256, 256, 832));
        VerticalMotionResolution result = Finish(Begin([moving], stationaryStructures: [floor]));

        Assert.Equal(expectedFailure, result.IsFailed);
        if (expectedFailure) { AssertFailed(result, VerticalDescentStatus.IncompleteSupport); }
        else { AssertLanded(result, moving, 256, Box(0, 0, 512, 256, 256, 576)); }
    }

    [Fact]
    public void Unknown_intermediate_terrain_cannot_be_crossed_to_a_known_lower_floor()
    {
        var below = Region(new(new(0, 0, 0), 1, 1, 2));
        var above = Region(new(new(0, 0, 3), 1, 1, 3));
        SpatialStructure moving = Slab(1, Box(32, 32, 1024, 224, 224, 1152));
        AssertFailed(Finish(Begin([moving], regions: [below, above])), VerticalDescentStatus.Unavailable);
    }

    [Theory]
    [InlineData(767, true)]
    [InlineData(768, false)]
    public void The_finite_horizon_must_include_an_actual_contact(long maximumDrop, bool expectedFailure)
    {
        SpatialStructure slab = Slab(1, Box(0, 0, 1024, 256, 256, 1088));
        VerticalMotionResolution result = Finish(VerticalMotionResolution.Begin(Scene([slab], maximumDrop: maximumDrop)));
        Assert.Equal(expectedFailure, result.IsFailed);
        if (expectedFailure) { AssertFailed(result, VerticalDescentStatus.NoContactWithinLimit); }
        else { AssertLanded(result, slab, 768, Box(0, 0, 256, 256, 256, 320)); }
    }

    [Fact]
    public void Each_real_work_prefix_is_immutable_and_can_resume_to_the_same_canonical_result()
    {
        var (scene, lower, upper, _) = SeparatedStack();
        VerticalMotionResolution expected = Finish(VerticalMotionResolution.Begin(scene));
        byte[] expectedBytes = VerticalMotionCodecV1.Encode(expected);
        VerticalMotionResolution state = VerticalMotionResolution.Begin(scene);
        int prefixes = 0;
        while (true)
        {
            Assert.InRange(prefixes++, 0, scene.MaximumTotalWorkUnits + 1);
            byte[] bytes = VerticalMotionCodecV1.Encode(state);
            VerticalMotionResolution restored = Decode(bytes, scene);
            Assert.Equal(bytes, VerticalMotionCodecV1.Encode(restored));
            Assert.Equal(state.CompletedWorkUnits, restored.CompletedWorkUnits);
            Assert.Equal(state.InputPin, restored.InputPin);
            Assert.Equal(expectedBytes, VerticalMotionCodecV1.Encode(Finish(restored)));
            if (state.IsComplete) { break; }
            Assert.Throws<InvalidOperationException>(() => state.Result);
            VerticalMotionResolution advanced = restored.Advance(1);
            Assert.True(advanced.CompletedWorkUnits > restored.CompletedWorkUnits);
            Assert.Equal(bytes, VerticalMotionCodecV1.Encode(state));
            Assert.Equal(bytes, VerticalMotionCodecV1.Encode(restored));
            state = advanced;
        }
        Assert.True(prefixes > 2);
        Assert.Equal(512L, lower.Boxes[0].Minimum.Z);
        Assert.Equal(1024L, upper.Boxes[0].Minimum.Z);
    }

    [Fact]
    public void Archives_are_bound_to_the_complete_scene_and_canceled_replay_publishes_nothing()
    {
        var (scene, lower, upper, rider) = SeparatedStack();
        VerticalMotionResolution state = VerticalMotionResolution.Begin(scene).Advance(1);
        byte[] bytes = VerticalMotionCodecV1.Encode(state);
        var changed = Scene([lower, upper], [SpatialFixture.Actor(1, rider.Feet, rider.Condition.Definition,
            revision: rider.Revision + 1, injuries: rider.Condition.Injuries)]);
        Assert.NotEqual(scene.InputPin, changed.InputPin);
        Assert.Throws<FormatException>(() => Decode(bytes, changed));
        Assert.Throws<OperationCanceledException>(() =>
            VerticalMotionCodecV1.Decode(bytes, scene, new CancellationToken(canceled: true)));
        Assert.Equal(bytes, VerticalMotionCodecV1.Encode(Decode(bytes, scene)));

        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] prefix = bytes[..length];
            Assert.Throws<FormatException>(() => Decode(prefix, scene));
        }
        Assert.Throws<FormatException>(() => Decode([.. bytes, 0], scene));
        Assert.Throws<FormatException>(() => Decode(new byte[513], scene));
    }

    [Fact]
    public void Canonical_archives_cannot_forge_the_input_pin_terminal_status_or_an_unbounded_work_cursor()
    {
        var (scene, _, _, _) = SeparatedStack();
        VerticalMotionResolution pending = VerticalMotionResolution.Begin(scene).Advance(1);
        Assert.False(pending.IsComplete);
        byte[] bytes = VerticalMotionCodecV1.Encode(pending);
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 1, ContentHash.Compute("other-motion"u8).ToByteArray()), scene));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 2, Number(ulong.MaxValue)), scene));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 2, Number(checked((ulong)scene.MaximumTotalWorkUnits + 1))), scene));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 3, Number(1)), scene));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 3, Number(2)), scene));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 3, Number(3)), scene));

        byte[] completed = VerticalMotionCodecV1.Encode(Finish(pending));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(completed, 2, Number(0)), scene));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(completed, 3, Number(0)), scene));
        Assert.Equal(bytes, VerticalMotionCodecV1.Encode(Decode(bytes, scene)));
    }

    [Fact]
    public void Failed_contact_prefixes_resume_without_exposing_a_partial_endpoint_as_success()
    {
        SpatialStructure lower = Slab(1, Box(0, 0, 512, 128, 256, 576));
        SpatialStructure upper = Slab(2, Box(0, 0, 1024, 256, 256, 1088));
        VerticalMotionScene scene = Scene([lower, upper]);
        VerticalMotionResolution state = VerticalMotionResolution.Begin(scene);
        byte[] expected = VerticalMotionCodecV1.Encode(Finish(state));
        int prefixes = 0;
        while (!state.IsComplete)
        {
            Assert.InRange(prefixes++, 0, scene.MaximumTotalWorkUnits + 1);
            byte[] bytes = VerticalMotionCodecV1.Encode(state);
            VerticalMotionResolution restored = Decode(bytes, scene);
            Assert.Equal(expected, VerticalMotionCodecV1.Encode(Finish(restored)));
            Assert.Throws<InvalidOperationException>(() => restored.Result);
            state = restored.Advance(1);
        }
        AssertFailed(state, VerticalDescentStatus.IncompleteSupport);
        Assert.Equal(expected, VerticalMotionCodecV1.Encode(Decode(VerticalMotionCodecV1.Encode(state), scene)));
    }

    private static (VerticalMotionScene Scene, SpatialStructure Lower, SpatialStructure Upper, ActorPresence Rider) SeparatedStack()
    {
        SpatialStructure lower = Slab(1, Box(0, 0, 512, 256, 256, 576));
        SpatialStructure upper = Slab(2, Box(0, 0, 1024, 256, 256, 1088));
        ActorPresence rider = SpatialFixture.Actor(1, new(128, 128, 1088), SpatialFixture.Body(64, 64, 128), revision: 7,
            injuries: [new(SpatialFixture.Id(20001), SpatialFixture.Id(20002), new("support"), BodyInjuryKind.Scar)]);
        return (Scene([lower, upper], [rider]), lower, upper, rider);
    }

    private static VerticalMotionResolution Begin(IEnumerable<SpatialStructure> moving,
        IEnumerable<TerrainRegionSnapshot>? regions = null,
        IEnumerable<SpatialStructure>? stationaryStructures = null, IEnumerable<ActorPresence>? stationaryActors = null) =>
        VerticalMotionResolution.Begin(Scene(moving, regions: regions, stationaryStructures: stationaryStructures,
            stationaryActors: stationaryActors));

    private static VerticalMotionScene Scene(IEnumerable<SpatialStructure> moving, IEnumerable<ActorPresence>? riders = null,
        IEnumerable<TerrainRegionSnapshot>? regions = null, IEnumerable<SpatialStructure>? stationaryStructures = null,
        IEnumerable<ActorPresence>? stationaryActors = null, long maximumDrop = 1024) =>
        new(SpatialFixture.Scope, regions ?? [Region()], stationaryStructures ?? [], stationaryActors ?? [],
            moving.Select(VerticalMotionBody.ForStructure).Concat((riders ?? []).Select(VerticalMotionBody.ForActor)), maximumDrop);

    private static VerticalMotionResolution Finish(VerticalMotionResolution state, int budget = 256)
    {
        int calls = 0;
        while (!state.IsComplete)
        {
            Assert.InRange(calls++, 0, state.MaximumTotalWorkUnits + 1);
            ulong before = state.CompletedWorkUnits;
            state = state.Advance(budget);
            Assert.InRange(state.CompletedWorkUnits - before, 1UL, checked((ulong)budget));
            Assert.InRange(state.CompletedWorkUnits, 0UL, checked((ulong)state.MaximumTotalWorkUnits));
        }
        return state;
    }

    private static void AssertLanded(VerticalMotionResolution state, SpatialStructure original, long drop, SubunitBox expected)
    {
        Assert.True(state.IsComplete);
        Assert.False(state.IsFailed);
        Assert.Null(state.FailureReason);
        Assert.Null(state.FailureStatus);
        Assert.Equal(drop, state.Result.Drops[original.StructureId]);
        SpatialStructure reached = Assert.Single(state.Result.Structures, value => value.StructureId == original.StructureId);
        Assert.Equal(expected, Assert.Single(reached.Boxes));
        Assert.Equal(BoxVolume(Assert.Single(original.Boxes)), BoxVolume(Assert.Single(reached.Boxes)));
        Assert.Equal(original.BearingSurfaces.Count, reached.BearingSurfaces.Count);
        for (int index = 0; index < original.BearingSurfaces.Count; index++)
        {
            Assert.Equal(original.BearingSurfaces[index].Bounds, reached.BearingSurfaces[index].Bounds);
            Assert.Equal(original.BearingSurfaces[index].TopTicks - (Int128)drop * 1000, reached.BearingSurfaces[index].TopTicks);
        }
    }

    private static void AssertFailed(VerticalMotionResolution state, VerticalDescentStatus reason)
    {
        Assert.True(state.IsComplete);
        Assert.True(state.IsFailed);
        Assert.Equal(reason, state.FailureStatus);
        Assert.False(string.IsNullOrWhiteSpace(state.FailureReason));
        Assert.Throws<InvalidOperationException>(() => state.Result);
    }

    private static string[] EventWitness(VerticalMotionResolution state) =>
        state.Result.Events.Select(value => $"{value.DropSubunits}:{string.Join(',', value.BodyIds)}").ToArray();

    private static UInt128 BoxVolume(SubunitBox box) =>
        checked((UInt128)(box.Maximum.X - box.Minimum.X) * (UInt128)(box.Maximum.Y - box.Minimum.Y)
            * (UInt128)(box.Maximum.Z - box.Minimum.Z));

    private static VerticalMotionResolution Decode(byte[] bytes, VerticalMotionScene scene) =>
        VerticalMotionCodecV1.Decode(bytes, scene, TestContext.Current.CancellationToken);

    // Independent canonical framing edits exercise cursor validation without creating invalid domain objects.
    private static byte[] ReplaceField(byte[] frame, int fieldIndex, byte[] replacement)
    {
        Assert.True(frame.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
        uint count = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(8));
        var fields = new byte[checked((int)count)][];
        int cursor = 12;
        for (int index = 0; index < fields.Length; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(cursor)));
            cursor = checked(cursor + 4);
            fields[index] = frame.AsSpan(cursor, length).ToArray();
            cursor = checked(cursor + length);
        }
        Assert.Equal(frame.Length, cursor);
        fields[fieldIndex] = replacement;
        var encoded = new byte[checked(12 + fields.Sum(field => checked(4 + field.Length)))];
        frame.AsSpan(0, 12).CopyTo(encoded);
        cursor = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(cursor), checked((uint)field.Length));
            cursor = checked(cursor + 4);
            field.CopyTo(encoded, cursor);
            cursor = checked(cursor + field.Length);
        }
        return encoded;
    }

    private static byte[] Number(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static TerrainRegionSnapshot Region(TerrainRegionBounds? bounds = null)
    {
        var material = new CountedItemDefinition(SpatialFixture.Id(8000), ContentHash.Compute("vertical-motion-material"u8));
        var rules = new StratifiedTerrainRules(0, 0, 1, new(material, material, material));
        return new StratifiedTerrainGenerator().Generate(SpatialFixture.Scope, SpatialFixture.Id(7000), rules,
            bounds ?? new(new(-2, -2, 0), 6, 6, 8));
    }

    private static SpatialStructure Slab(ulong id, SubunitBox box) =>
        new(SpatialFixture.Id(1000 + id), [box],
            [new(new((Int128)box.Minimum.X * 1000, (Int128)box.Minimum.Y * 1000,
                (Int128)box.Maximum.X * 1000, (Int128)box.Maximum.Y * 1000), (Int128)box.Maximum.Z * 1000)]);

    private static SubunitBox Box(long x0, long y0, long z0, long x1, long y1, long z1) =>
        new(new(x0, y0, z0), new(x1, y1, z1));
}
