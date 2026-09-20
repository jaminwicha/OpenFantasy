using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConstructionContactAssessmentWorldTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Funded_current_worlds_keep_the_exact_ordinary_load_graph_before_and_after_real_settlement(bool deferred)
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: deferred ? 1 : null);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        Assert.Equal(deferred ? (ushort)16 : (ushort)15, before.ProfileVersion);
        Assert.Single(before.Production!.Batches);
        ConstructionProject project = Assert.Single(before.Construction!.Projects);
        Assert.NotNull(project.Installed);
        byte[] closedBefore = File.ReadAllBytes(files.Path);
        ConstructionBearingModel beforeAssessment = AssertSameAssessment(before);
        AssertUnfundedHoleIsOnlyARefusedProposal(before, fixture.Site.Cell);
        Assert.Equal(closedBefore, File.ReadAllBytes(files.Path));

        WorldSnapshot after;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepDecision decision = authority.Commit(fixture.NextExcavation(before));
            int continuations = 0;
            while (decision.Candidate.PendingExcavation is not null)
            {
                Assert.Empty(decision.Receipts);
                Assert.True(continuations++ < ConstructionSettlementResolution.MaximumWorkUnits + 1,
                    "The funded excavation must terminate within its retained finite work bound.");
                WorldSnapshot pending = decision.Candidate;
                decision = authority.Commit(new WorldStepBatch(fixture.Actions.NextIdentity(), pending.WorldId,
                    pending.BranchId, pending.Revision, pending.AdvancingSteps, WorldBatchKind.ConsequenceContinuation,
                    [], [], pending.ProfileVersion));
            }
            Assert.Equal(deferred, continuations > 0);
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            after = decision.Candidate;
        }

        byte[] closedAfter = File.ReadAllBytes(files.Path);
        ConstructionBearingModel afterAssessment = AssertSameAssessment(after);
        Assert.Equal(closedAfter, File.ReadAllBytes(files.Path));
        Assert.Equal(before.ProfileVersion, after.ProfileVersion);
        Assert.Equal(before.Construction.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.Production.ContentHash, after.Production!.ContentHash);
        Assert.Equal(project.Installed, Assert.Single(after.Construction.Projects).Installed);
        Assert.Equal(beforeAssessment.Members.ToArray(), afterAssessment.Members.ToArray());
        Assert.Equal(ConstructionMemberDisposition.Resting, Assert.Single(after.ConstructionPhysical!.Members).Disposition);
        Assert.Equal(new SubunitPosition(0, 0, -256), Assert.Single(after.ConstructionPhysical.Members).Displacement);
        Assert.Equal(before.Space.GetActor(fixture.Builder).Feet.Offset(0, 0, -256), after.Space.GetActor(fixture.Builder).Feet);
        Assert.Equal(1UL, after.Excavation!.Sites.Single().CompletedWork);
        var issued = Assert.Single(after.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        Assert.Equal(fixture.Site.Quantity, issued.Quantity);
        Assert.Equal(after.GetResident(fixture.Player).InventoryCustodianId, issued.CustodianId);
        UInt128 issuedMass = checked((UInt128)fixture.Site.Quantity * fixture.Site.Material.Physical.MassPerUnit.Value);
        Assert.Equal(beforeAssessment.Graph.TotalInputMass + issuedMass, afterAssessment.Graph.TotalInputMass);
        Assert.Equal((UInt128)beforeAssessment.Actors.Single(actor => actor.ActorId == fixture.Player).BodyAndCarriedMass.Value
            + issuedMass, (UInt128)afterAssessment.Actors.Single(actor => actor.ActorId == fixture.Player).BodyAndCarriedMass.Value);
        Assert.Equal(beforeAssessment.Actors.Single(actor => actor.ActorId == fixture.Builder),
            afterAssessment.Actors.Single(actor => actor.ActorId == fixture.Builder));
    }

    private static ConstructionBearingModel AssertSameAssessment(WorldSnapshot world)
    {
        byte[] root = WorldSnapshotCodec.Encode(world);
        var policy = Assert.IsType<ConstructionLoadPolicy>(world.StructuralPolicy);
        var physical = Assert.IsType<ConstructionPhysicalSnapshot>(world.ConstructionPhysical);
        // Reversal proves that the proposal's complete catalogs have a deterministic retained order.
        var scene = new ConstructionContactScene(world, world.Space.Regions.Reverse(), physical, world.Space.Actors.Reverse());
        ConstructionBearingModel ordinary = policy.Bind(world);
        ConstructionBearingModel assessed = policy.AssessContacts(scene);
        Assert.Equal(StructuralLoadCodec.EncodeGraph(ordinary.Graph), StructuralLoadCodec.EncodeGraph(assessed.Graph));
        Assert.Equal(ordinary.Members.ToArray(), assessed.Members.ToArray());
        Assert.Equal(ordinary.Actors.ToArray(), assessed.Actors.ToArray());
        Assert.Equal(ordinary.Foundations.ToArray(), assessed.Foundations.ToArray());
        Assert.Equal(ordinary.Graph.TotalInputMass, assessed.Graph.TotalInputMass);
        Assert.Equal(scene.InputPin, assessed.CandidateScenePin);
        Assert.Equal(policy.ContentHash, assessed.PolicyPin);
        Assert.Equal(world.ContentHash, assessed.WorldHash);
        Assert.NotEqual(ordinary.InputPin, assessed.InputPin);
        Assert.Equal(scene.InputPin, new ConstructionContactScene(world, world.Space.Regions, physical, world.Space.Actors).InputPin);
        ConstructionProject installed = Assert.Single(world.Construction!.Projects);
        Assert.Equal(installed.SiteFeet.Offset(0, 0, physical.GetMember(installed.Installed!.StructureId, 0).Displacement.Z),
            scene.OriginFor(installed));

        StructuralLoadResolution resolution = StructuralLoadResolution.Begin(assessed.Graph, []);
        for (int slice = 0; !resolution.IsComplete; slice++)
        {
            Assert.True(slice <= assessed.Graph.Elements.Count, "A stable retained graph finishes its first bounded load pass.");
            resolution = resolution.Advance(StructuralLoadResolution.MaximumEvaluationsPerAdvance);
            Assert.Empty(resolution.Failures);
        }
        UInt128 retainedMass = assessed.Members.Aggregate((UInt128)0, (sum, member) => checked(sum + member.Mass.Value));
        retainedMass = assessed.Actors.Aggregate(retainedMass, (sum, actor) => checked(sum + actor.BodyAndCarriedMass.Value));
        Assert.Equal(retainedMass, assessed.Graph.TotalInputMass);
        Assert.Equal(retainedMass, resolution.AcceptedFoundationLoad);
        Assert.Equal((UInt128)0, resolution.FailedInputMass);
        Assert.Equal(root, WorldSnapshotCodec.Encode(world));
        return assessed;
    }

    private static void AssertUnfundedHoleIsOnlyARefusedProposal(WorldSnapshot world, CellCoordinate cell)
    {
        byte[] root = WorldSnapshotCodec.Encode(world);
        TerrainRegionSnapshot original = world.Space.Regions.Single(region => region.Bounds.Contains(cell));
        TerrainRegionSnapshot proposed = original.Excavate(original.Revision, [cell]);
        var scene = new ConstructionContactScene(world,
            world.Space.Regions.Select(region => region.Bounds == original.Bounds ? proposed : region),
            world.ConstructionPhysical!, world.Space.Actors);
        // The proposal removes geometric support without moving the actual slab or its rider. It cannot
        // borrow the later settled contacts, issue the substrate or alter the retained world's custody.
        Assert.Throws<ArgumentException>(() => world.StructuralPolicy!.AssessContacts(scene));
        Assert.False(original.GetCell(cell).IsExcavated);
        Assert.Equal(0UL, world.Excavation!.Sites.Single().CompletedWork);
        Assert.Equal(root, WorldSnapshotCodec.Encode(world));
    }
}
