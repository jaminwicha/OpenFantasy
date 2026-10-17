using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class WorldMovementPrerequisitesTests
{
    [Fact]
    public void File_backed_boundary_crossing_retains_cargo_and_replays_through_the_existing_authority()
    {
        var basis = WorldServiceFixture.Genesis();
        var original = basis.Space.Regions.Single();
        var generator = new StratifiedTerrainGenerator();
        TerrainRegionSnapshot[] regions = [
            generator.Generate(original.Scope, original.Seed, original.Rules, new(new(-2, -2, -1), 2, 4, 5)),
            generator.Generate(original.Scope, original.Seed, original.Rules, new(new(0, -2, -1), 4, 4, 5)),
            generator.Generate(original.Scope, original.Seed, original.Rules, new(new(100, -2, -1), 4, 4, 5))
        ];
        var genesis = new WorldSnapshot(basis.Inventory, new(original.Scope, regions, basis.Space.Actors),
            basis.Residents, basis.AdmissionVersion, basis.Calendar, basis.Revision, basis.AdvancingSteps);
        var input = Move();
        var prepared = WorldMovementPrerequisites.Capture(genesis, input, 2);
        Assert.True(prepared.CanAttempt);
        var lot = Assert.Single(prepared.Carriage.CountedLots);
        Assert.Equal(6UL, lot.Quantity);
        using var files = new StorageFiles();
        var directory = Directory.CreateTempSubdirectory("openfantasy-movement-read-");
        WorldSnapshot final;
        try
        {
            foreach (var region in regions)
                File.WriteAllBytes(Path.Combine(directory.FullName, region.ComputeContentHash().Value), TerrainRegionCodecV1.Encode(region));
            var reads = new List<ContentHash>();
            var loaded = new TerrainReadPreparation((reference, limit) =>
            {
                reads.Add(reference.ContentHash);
                string path = Path.Combine(directory.FullName, reference.ContentHash.Value);
                Assert.InRange(new FileInfo(path).Length, 1, limit);
                return File.ReadAllBytes(path);
            }).Load(prepared.Spatial.Terrain, 2 * TerrainRegionCodecV1.MaximumEncodedByteLength, 120);
            Assert.Equal(2, reads.Count);
            Assert.DoesNotContain(regions[2].ComputeContentHash(), reads);
            var geometry = prepared.Evaluate(loaded);
            Assert.Equal(SpatialMoveStatus.Prepared, geometry.Status);
            using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
            {
                prepared.ValidateCurrent(authority.ReadStateForHost());
                final = authority.Commit(WorldServiceFixture.Step(genesis, 910, movement: [input])).Candidate;
                Assert.Equal(geometry.Candidate!.Feet, final.Space.GetActor(input.ActorId).Feet);
                Assert.Equal(-1, final.Space.GetActor(input.ActorId).Feet.Cell.X);
                Assert.Equal(lot, Assert.Single(WorldActorCarriage.Capture(final, input.ActorId).CountedLots));
                Assert.Equal(genesis.AdvancingSteps + 1, final.AdvancingSteps);
                Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(final));
                prepared.Carriage.ValidateCurrent(final);
                prepared.Control.ValidateCurrent(final);
            }
            using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            var next = WorldMovementPrerequisites.Capture(final, input, 2);
            next.ValidateCurrent(reopened.ReadStateForHost());
            prepared.ValidateCurrent(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(genesis)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Advancing_time_invalidates_the_join_even_when_geometry_control_and_cargo_match()
    {
        var genesis = WorldServiceFixture.Genesis();
        var prepared = WorldMovementPrerequisites.Capture(genesis, Move(), 1);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var advanced = authority.Commit(WorldServiceFixture.Step(genesis, 920)).Candidate;
        prepared.Spatial.ValidateCurrent(advanced.Space);
        prepared.Control.ValidateCurrent(advanced);
        prepared.Carriage.ValidateCurrent(advanced);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(advanced));
        Assert.True(WorldMovementPrerequisites.Capture(advanced, Move(), 1).CanAttempt);
    }

    [Fact]
    public void Actual_gift_invalidates_sender_and_recipient_carriage_without_moving_either_body()
    {
        var genesis = WorldServiceFixture.Genesis();
        var sender = WorldMovementPrerequisites.Capture(genesis, Move(), 1);
        var recipient = WorldMovementPrerequisites.Capture(genesis,
            new(new(WorldServiceFixture.Id(101), WorldServiceFixture.Id(111)), WorldServiceFixture.Id(11), 32, 0), 1);
        Assert.Equal(SpatialMoveStatus.Prepared, sender.Evaluate(Load(sender, genesis)).Status);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var decision = authority.Commit(WorldServiceFixture.Step(genesis, 930,
            actions: [WorldServiceFixture.Give(genesis, 931, 2)]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        foreach (var prepared in new[] { sender, recipient })
        {
            prepared.Spatial.ValidateCurrent(decision.Candidate.Space);
            prepared.Control.ValidateCurrent(decision.Candidate);
            Assert.Throws<InvalidOperationException>(() => prepared.Carriage.ValidateCurrent(decision.Candidate));
            Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(decision.Candidate));
        }
        Assert.Equal(6UL, Assert.Single(sender.Carriage.CountedLots).Quantity);
        Assert.Empty(recipient.Carriage.CountedLots);
    }

    [Fact]
    public void Equal_quantity_returned_through_real_gifts_is_not_the_original_carriage()
    {
        var basis = WorldServiceFixture.Genesis();
        var inventory = basis.Inventory;
        var reciprocal = new CountedAuthorityState(inventory.Custody, inventory.AdmittedUnitCapacity,
            inventory.Actors, inventory.Controls,
            inventory.Policies.Select(policy => new CustodianAccessPolicy(policy.CustodianId,
                policy.OwnerActorId, CustodyDepositRule.AnyControlledActor)), inventory.Sources, inventory.ReservationIds);
        var genesis = new WorldSnapshot(reciprocal, basis.Space, basis.Residents, basis.AdmissionVersion,
            basis.Calendar, basis.Revision, basis.AdvancingSteps);
        var before = WorldActorCarriage.Capture(genesis, Move().ActorId);
        var original = Assert.Single(before.CountedLots);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var sent = authority.Commit(WorldServiceFixture.Step(genesis, 950,
            actions: [WorldServiceFixture.Give(genesis, 951, 6)]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(sent.Receipts).Rejection);
        var held = sent.Candidate;
        var recipient = WorldServiceFixture.Id(11);
        var received = Assert.Single(WorldActorCarriage.Capture(held, recipient).CountedLots);
        var giveBack = new WorldActionCommand(WorldServiceFixture.Id(953), WorldServiceFixture.Id(101),
            WorldServiceFixture.Id(111), recipient, held.WorldId, held.BranchId, held.Space.GetActor(recipient).Revision,
            new GiveItemIntent(Move().ActorId, held.Space.GetActor(Move().ActorId).Revision, received.LotId,
                received.Revision, received.CustodianId, held.Inventory.Custody.GetStorage(received.CustodianId).Revision, 6));
        var returned = authority.Commit(WorldServiceFixture.Step(held, 952, actions: [giveBack]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(returned.Receipts).Rejection);
        var actual = Assert.Single(WorldActorCarriage.Capture(returned.Candidate, Move().ActorId).CountedLots);
        Assert.Equal(original.Quantity, actual.Quantity);
        Assert.Equal(original.Definition, actual.Definition);
        Assert.Equal(original.Source, actual.Source);
        Assert.Equal(original.LotId, actual.LotId);
        Assert.Equal(original.Revision + 2, actual.Revision);
        Assert.Throws<InvalidOperationException>(() => before.ValidateCurrent(returned.Candidate));
    }

    [Fact]
    public void Closing_a_reading_hold_changes_eligibility_without_earning_time_or_moving_material()
    {
        var genesis = WorldServiceFixture.Genesis();
        var before = WorldMovementPrerequisites.Capture(genesis, Move(), 1);
        Assert.Equal(SpatialMoveStatus.Prepared, before.Evaluate(Load(before, genesis)).Status);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var speak = new WorldActionCommand(WorldServiceFixture.Id(941), WorldServiceFixture.Delivery.PrincipalId,
            WorldServiceFixture.Delivery.SessionId, Move().ActorId, genesis.WorldId, genesis.BranchId, 0,
            new SpeakToActorIntent(WorldServiceFixture.Id(11), 0));
        var spoken = authority.Commit(WorldServiceFixture.Step(genesis, 940, actions: [speak], movement: [Move()]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(spoken.Receipts).Rejection);
        var held = spoken.Candidate;
        Assert.Equal(genesis.Space.GetActor(Move().ActorId).Feet, held.Space.GetActor(Move().ActorId).Feet);
        before.Spatial.ValidateCurrent(held.Space);
        Assert.Throws<InvalidOperationException>(() => before.ValidateCurrent(held));
        var prepared = WorldMovementPrerequisites.Capture(held, Move(), 1);
        Assert.False(prepared.CanAttempt);
        Assert.Throws<InvalidOperationException>(() => prepared.Evaluate(Load(prepared, held)));
        var close = new WorldActionCommand(WorldServiceFixture.Id(943), speak.PrincipalId, speak.OriginSessionId,
            speak.ActorId, held.WorldId, held.BranchId, held.Space.GetActor(speak.ActorId).Revision,
            new CloseDialogueIntent(held.Dialogues.Single().DialogueId));
        var closed = authority.Commit(WorldServiceFixture.Step(held, 942, actions: [close])).Candidate;
        Assert.False(closed.HasReadingHold);
        Assert.Equal(held.Calendar, closed.Calendar);
        Assert.Equal(held.AdvancingSteps, closed.AdvancingSteps);
        prepared.Spatial.ValidateCurrent(closed.Space);
        prepared.Control.ValidateCurrent(closed);
        prepared.Carriage.ValidateCurrent(closed);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(closed));
        Assert.True(WorldMovementPrerequisites.Capture(closed, Move(), 1).CanAttempt);
    }

    [Fact]
    public void Nonadvancing_revocation_invalidates_the_join_despite_unchanged_pose_time_and_cargo()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        fixture.Grant(authority);
        fixture.LoseAndExpire(authority);
        var acquired = fixture.Acquire(authority);
        var input = new WorldMovementInput(fixture.Delivery(fixture.Grantee), fixture.Owner, 32, 0,
            acquired.EffectiveControl!.Find(fixture.Owner)!.Witness);
        var prepared = WorldMovementPrerequisites.Capture(acquired, input, LocalSpatialSnapshot.MaximumRegions);
        Assert.True(prepared.CanAttempt);
        var revoked = fixture.Revoke(authority);
        Assert.Equal(acquired.Calendar, revoked.Calendar);
        prepared.Spatial.ValidateCurrent(revoked.Space);
        prepared.Carriage.ValidateCurrent(revoked);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(revoked));
        var denied = WorldMovementPrerequisites.Capture(revoked, input, LocalSpatialSnapshot.MaximumRegions);
        Assert.False(denied.CanAttempt);
        Assert.Throws<InvalidOperationException>(() => denied.Evaluate(Load(denied, revoked)));
    }

    private static WorldMovementInput Move() => new(WorldServiceFixture.Delivery, WorldServiceFixture.Id(10), -32, 0);

    private static TerrainReadSet Load(WorldMovementPrerequisites prepared, WorldSnapshot world) =>
        prepared.Spatial.Terrain.Resolve(world.Space.Regions.Where(region =>
            prepared.Spatial.Terrain.References.Any(reference => reference.Bounds == region.Bounds)));
}
