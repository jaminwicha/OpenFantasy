using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class WorldWatershedTests
{
    [Fact]
    public void CompleteWorldRoundTripsAndKeepsLegacyBytesUnchanged()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        byte[] legacy = WorldSnapshotCodec.Encode(basis);
        PreparedWatershed plan = Plan(basis);
        WorldSnapshot world = Attach(basis, plan);
        Assert.Equal(6, world.ProfileVersion);
        byte[] encoded = WorldSnapshotCodec.Encode(world);
        WorldSnapshot restored = WorldSnapshotCodec.Decode(encoded);
        Assert.Equal(encoded, WorldSnapshotCodec.Encode(restored));
        Assert.Equal(WatershedCodecV1.Encode(plan), WatershedCodecV1.Encode(restored.Watershed!));
        Assert.Equal(legacy, WorldSnapshotCodec.Encode(basis));
        Assert.Null(WorldSnapshotCodec.Decode(legacy).Watershed);
        foreach (EntityId id in plan.Columns.Select(value => value.SubstrateParcelId)
            .Concat(plan.Boundaries.Select(value => value.BoundaryId))
            .Concat(new[] { plan.BasinId, plan.FormationId, plan.Request.ExternalBoundaryId }))
        {
            Assert.True(restored.IdentityIndex.Contains(id));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AnotherWorldBranchOrRealmCannotBeAdmitted(int component)
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        var scope = basis.Space.Scope;
        var wrong = new TerrainScope(component == 0 ? Id(20) : scope.WorldId,
            component == 1 ? Id(20) : scope.BranchId, component == 2 ? Id(20) : scope.RealmId);
        Assert.Throws<ArgumentException>(() => Attach(basis, Plan(basis, scope: wrong)));
    }

    [Fact]
    public void MacroTerminalCannotAliasActorsLotsOrLocalTerrain()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        EntityId terrain = basis.Space.Regions[0].Cells.First(value => value.SourceId is not null).SourceId!.Value;
        foreach (EntityId identity in new[] { basis.Residents[0].ActorId,
            basis.Inventory.Custody.Lots[0].LotId, terrain })
        {
            Assert.Throws<ArgumentException>(() => Attach(basis, Plan(basis, terminal: identity)));
        }
    }

    [Fact]
    public void WorkAndVersionedStepReplayRetainTheSameImmutableMacroFacts()
    {
        WorldSnapshot world = Attach(ConstructionWorldFixture.Create(), null);
        PreparedWatershed plan = world.Watershed!;
        for (ulong i = 0; i < 200; i++)
        {
            var batch = new WorldStepBatch(Id(100 + i), world.WorldId, world.BranchId,
                world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [],
                [ConstructionWorldFixture.Command(world, 100 + i)], 6);
            byte[] bytes = WorldStepCodec.Encode(batch);
            WorldStepBatch restored = WorldStepCodec.Decode(bytes);
            Assert.Equal(bytes, WorldStepCodec.Encode(restored));
            WorldStepDecision step = new WorldStepProcessor().Decide(world, restored);
            Assert.Equal(WorldActionRejection.None, Assert.Single(step.Receipts).Rejection);
            Assert.Same(plan, step.Candidate.Watershed);
            Assert.True(step.Candidate.Construction!.Projects[0].Progress.CompletedUnits
                > world.Construction!.Projects[0].Progress.CompletedUnits);
            world = step.Candidate;
        }
        Assert.Single(world.Space.Structures);
        byte[] complete = WorldSnapshotCodec.Encode(world);
        WorldSnapshot reopened = WorldSnapshotCodec.Decode(complete);
        Assert.Single(reopened.Space.Structures);
        Assert.Equal(complete, WorldSnapshotCodec.Encode(reopened));
        var collision = new WorldStepBatch(plan.Columns[0].SubstrateParcelId, world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], 6);
        Assert.Throws<InvalidOperationException>(() => new WorldStepProcessor().Decide(world, collision));
    }

    [Fact]
    public void MalformedOrIncompleteWorldCannotSilentlyLoseItsWatershed()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        WorldSnapshot world = Attach(basis, null);
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        bytes[^1] ^= 1;
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(bytes));
        byte[] invalid = Frame("world.snapshot", new byte[] { 0, 0, 0, 0, 0, 0, 0, 6 },
            WorldSnapshotCodec.Encode(ConstructionWorldFixture.Basis()), WatershedCodecV1.Encode(world.Watershed!));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(invalid));
        Assert.Throws<ArgumentException>(() => Attach(ConstructionWorldFixture.Basis(), world.Watershed));
    }

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

    private static EntityId Id(ulong value) => new(0x7761746572736864, value);

    private static PreparedWatershed Plan(WorldSnapshot basis, TerrainScope? scope = null, EntityId? terminal = null) =>
        new WatershedPlanner().Prepare(new WatershedRequest(scope ?? basis.Space.Scope, Id(1),
            new WatershedBounds(-2, -3, 4, 5), new WatershedRules(0, 8), 0, terminal ?? Id(2)),
            TestContext.Current.CancellationToken);

    private static WorldSnapshot Attach(WorldSnapshot basis, PreparedWatershed? plan) =>
        new(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion, basis.Calendar,
            basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering, basis.Production,
            basis.Progression, basis.Quests, basis.Construction, plan ?? Plan(basis));
}
