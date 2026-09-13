using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class WorldWatershedNetworkTests
{
    [Fact]
    public void NetworkWorldRoundTripsWithoutChangingItsConstructionOrSingleBasinPredecessors()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        PreparedWatershedNetwork network = Plan(basis);
        byte[] original = WorldSnapshotCodec.Encode(basis);
        WorldSnapshot single = Copy(basis, watershed: network.Basins[0]);
        byte[] singleBytes = WorldSnapshotCodec.Encode(single);
        WorldSnapshot world = Copy(basis, network: network);
        Assert.Equal(7, world.ProfileVersion);
        Assert.Null(world.Watershed);
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        WorldSnapshot restored = WorldSnapshotCodec.Decode(bytes);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(restored));
        Assert.Equal(WatershedNetworkCodecV1.Encode(network), WatershedNetworkCodecV1.Encode(restored.WatershedNetwork!));
        Assert.Equal(original, WorldSnapshotCodec.Encode(basis));
        Assert.Equal(singleBytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(singleBytes)));
        Assert.Null(WorldSnapshotCodec.Decode(singleBytes).WatershedNetwork);
        Assert.Equal(16 * 1024 * 1024, WorldSnapshotCodec.MaximumStateByteLength);
        foreach (EntityId identity in Identities(network)) Assert.True(restored.IdentityIndex.Contains(identity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ForeignWorldBranchOrRealmIsRefused(int component)
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        var scope = basis.Space.Scope;
        var other = new TerrainScope(component == 0 ? Id(40) : scope.WorldId,
            component == 1 ? Id(40) : scope.BranchId, component == 2 ? Id(40) : scope.RealmId);
        Assert.Throws<ArgumentException>(() => Copy(basis, network: Plan(basis, scope: other)));
    }

    [Fact]
    public void NetworkRequiresCompleteBasisAndCannotCompeteWithAnotherMacroComponent()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        PreparedWatershedNetwork network = Plan(basis);
        Assert.Throws<ArgumentException>(() => Copy(ConstructionWorldFixture.Basis(), network: network));
        Assert.Throws<ArgumentException>(() => Copy(basis, network, network.Basins[0]));
        Assert.Throws<ArgumentException>(() => WorldWatershedNetworkAdmission.CreateGenesis(basis, network));
        Assert.Throws<ArgumentException>(() => WorldWatershedNetworkAdmission.CreateGenesis(Copy(basis, network), network));
        Assert.Throws<ArgumentException>(() => WorldWatershedNetworkAdmission.CreateGenesis(Copy(basis, watershed: network.Basins[0]), network));
    }

    [Fact]
    public void ProvinceAndMemberOutletsCannotAliasExistingActorsLotsOrTerrain()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        EntityId terrain = basis.Space.Regions[0].Cells.First(c => c.SourceId.HasValue).SourceId!.Value;
        foreach (EntityId occupied in new[] { basis.Residents[0].ActorId, basis.Inventory.Custody.Lots[0].LotId,
            terrain, basis.Space.Regions[0].Rules.Materials.Stone.DefinitionId })
        {
            Assert.Throws<ArgumentException>(() => Copy(basis, network: Plan(basis, province: occupied)));
            Assert.Throws<ArgumentException>(() => Copy(basis, network: Plan(basis, terminal: occupied)));
        }
    }

    [Fact]
    public void AChangedNetworkInvalidatesThePreviouslyAdmittedMacroIdentityLayer()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        WorldSnapshot first = Copy(basis, network: Plan(basis));
        WorldSnapshot second = Copy(basis, network: Plan(basis, province: Id(41), seed: Id(42)));
        WorldIdentityIndex index = WorldAdmissionValidator.Validate(second, first.IdentityIndex);
        foreach (EntityId id in Identities(second.WatershedNetwork!)) Assert.True(index.Contains(id));
        Assert.False(index.Contains(first.WatershedNetwork!.ProvinceId));
        WorldIdentityIndex removed = WorldAdmissionValidator.Validate(basis, first.IdentityIndex);
        Assert.False(removed.Contains(first.WatershedNetwork.ProvinceId));
    }

    [Fact]
    public void ConstructionAndVersionedStepReplayRetainAllNetworkFactsAndReservations()
    {
        WorldSnapshot world = Copy(ConstructionWorldFixture.Create(), network: null);
        world = Copy(world, network: Plan(world));
        PreparedWatershedNetwork network = world.WatershedNetwork!;
        byte[] before = WatershedNetworkCodecV1.Encode(network);
        for (ulong i = 0; i < 200; i++)
        {
            var batch = new WorldStepBatch(Id(100 + i), world.WorldId, world.BranchId, world.Revision,
                world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [ConstructionWorldFixture.Command(world, 100 + i)], 7);
            byte[] bytes = WorldStepCodec.Encode(batch);
            Assert.Equal(bytes, WorldStepCodec.Encode(WorldStepCodec.Decode(bytes)));
            WorldStepDecision step = new WorldStepProcessor().Decide(world, WorldStepCodec.Decode(bytes));
            Assert.Equal(WorldActionRejection.None, Assert.Single(step.Receipts).Rejection);
            Assert.Same(network, step.Candidate.WatershedNetwork);
            world = step.Candidate;
        }
        Assert.Single(world.Space.Structures);
        WorldSnapshot restored = WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world));
        Assert.Equal(before, WatershedNetworkCodecV1.Encode(restored.WatershedNetwork!));
        Assert.Single(restored.Space.Structures);
        foreach (EntityId id in new[] { network.ProvinceId, network.SharedFaces[0].BoundaryId, network.Basins[0].Columns[0].SubstrateParcelId })
        {
            Assert.Throws<InvalidOperationException>(() => new WorldStepProcessor().Decide(world,
                new WorldStepBatch(id, world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps,
                    WorldBatchKind.AdvancingStep, [], [], 7)));
        }
        Assert.Throws<InvalidOperationException>(() => new WorldStepProcessor().Decide(world,
            new WorldStepBatch(Id(900), world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [], 6)));
    }

    [Fact]
    public void DecodeRejectsMissingInvalidOrWronglyNestedMacroAndWorldProfiles()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        PreparedWatershedNetwork network = Plan(basis);
        byte[] encoded = WorldSnapshotCodec.Encode(Copy(basis, network));
        byte[] corrupt = (byte[])encoded.Clone(); corrupt[^1] ^= 1;
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(corrupt));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode([.. encoded, 0]));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(encoded[..^1]));
        foreach (WorldSnapshot wrong in new[] { ConstructionWorldFixture.Basis(), Copy(basis, watershed: network.Basins[0]), Copy(basis, network) })
        {
            byte[] packet = Frame("world.snapshot", new byte[] { 0, 0, 0, 0, 0, 0, 0, 7 },
                WorldSnapshotCodec.Encode(wrong), WatershedNetworkCodecV1.Encode(network));
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(packet));
        }
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(Frame("world.snapshot",
            new byte[] { 0, 0, 0, 0, 0, 0, 0, 7 }, WorldSnapshotCodec.Encode(basis), WatershedCodecV1.Encode(network.Basins[0]))));
    }

    private static PreparedWatershedNetwork Plan(WorldSnapshot basis, TerrainScope? scope = null,
        EntityId? province = null, EntityId? terminal = null, EntityId? seed = null)
    {
        PreparedWatershed Basin(long x, long elevation, int outlet, EntityId boundary) => new WatershedPlanner().Prepare(
            new WatershedRequest(scope ?? basis.Space.Scope, seed ?? Id(1), new WatershedBounds(x, -3, 4, 5),
                new WatershedRules(elevation, 2), outlet, boundary), TestContext.Current.CancellationToken);
        PreparedWatershed a = Basin(-2, 20, 3, terminal ?? Id(2));
        PreparedWatershed b = Basin(2, 0, 0, Id(3));
        return new WatershedNetworkPlanner().Prepare(province ?? Id(4), [a, b],
            [WatershedOutletClosure.ToBasin(a.BasinId, b.BasinId, 0), WatershedOutletClosure.ToTerminal(b.BasinId, 40)],
            TestContext.Current.CancellationToken);
    }

    private static IEnumerable<EntityId> Identities(PreparedWatershedNetwork network) =>
        new[] { network.ProvinceId }.Concat(network.SharedFaces.Select(f => f.BoundaryId))
            .Concat(network.Basins.SelectMany(b => new[] { b.BasinId, b.FormationId, b.Request.ExternalBoundaryId }
                .Concat(b.Columns.Select(c => c.SubstrateParcelId)).Concat(b.Boundaries.Select(f => f.BoundaryId))));

    private static WorldSnapshot Copy(WorldSnapshot basis, PreparedWatershedNetwork? network = null,
        PreparedWatershed? watershed = null) =>
        new(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion, basis.Calendar,
            basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering, basis.Production,
            basis.Progression, basis.Quests, basis.Construction, watershed, network);

    private static byte[] Frame(string tag, params byte[][] fields)
    {
        using var stream = new MemoryStream();
        stream.Write("OFC\0"u8);
        void Number(uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
            stream.Write(bytes);
        }
        Number(1);
        Number((uint)fields.Length + 1);
        foreach (byte[] field in new[] { System.Text.Encoding.UTF8.GetBytes(tag) }.Concat(fields))
        {
            Number((uint)field.Length);
            stream.Write(field);
        }
        return stream.ToArray();
    }

    private static EntityId Id(ulong value) => new(0x6e6574776f726b, value);
}
