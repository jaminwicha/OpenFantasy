using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class StructuralSettlementWorldTests
{
    [Fact]
    public void Real_excavation_settles_a_funded_slab_and_its_rider_atomically_across_restart_and_history()
    {
        var fixture = new StructuralSettlementWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldStepBatch excavation = fixture.NextExcavation(before);
        var batch = new WorldStepBatch(excavation.CauseId, excavation.WorldId, excavation.BranchId,
            excavation.ExpectedRevision, excavation.ExpectedAdvancingSteps, excavation.Kind,
            [new(ConstructionUpgradeFixture.Delivery(before, fixture.Builder), fixture.Builder, 32, 0)],
            excavation.Actions, excavation.ProfileVersion);
        RetainBefore(files.Path, fixture, batch, legacy: false);
        ConstructionMemberState memberBefore = Assert.Single(before.ConstructionPhysical!.Members);
        ConstructionProject project = Assert.Single(before.Construction!.Projects);
        StructuralMemberReference backing = before.ConstructionPhysical.GetMemberReference(memberBefore.StructureId, memberBefore.MemberIndex);
        byte[] installedMaterial = OpenFantasy.Core.Production.ProductionCodecV1.Encode(before.Production!);
        Assert.Equal(452UL, before.StructuralSettlementPolicy!.FlightMilliseconds(256));

        WorldSnapshot after;
        byte[] receiptBytes;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(before.ContentHash, authority.InspectForHost().State.ContentHash);
            byte[] step = WorldStepCodec.Encode(batch);
            Assert.Equal(step, WorldStepCodec.Encode(WorldStepCodec.Decode(step)));
            WorldStepDecision decision = authority.Commit(WorldStepCodec.Decode(step));
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            receiptBytes = WorldReceiptCodecV1.Encode(decision.Receipts[0]);
            after = decision.Candidate;
            Assert.Equal((ushort)15, after.ProfileVersion);
            Assert.Equal(StructuralSettlementWorldFixture.RiderFeet.Offset(0, 0, -256), after.Space.GetActor(fixture.Builder).Feet);
            Assert.Equal(before.Space.GetActor(fixture.Builder).Revision + 1, after.Space.GetActor(fixture.Builder).Revision);
            Assert.Equal(before.Space.GetActor(fixture.Player).Feet, after.Space.GetActor(fixture.Player).Feet);
            Assert.Equal(before.Space.GetActor(fixture.Player).Revision, after.Space.GetActor(fixture.Player).Revision);
            Assert.All(before.Space.Actors, actor => Assert.Equal(actor.Condition.ComputeContentHash(),
                after.Space.GetActor(actor.ActorId).Condition.ComputeContentHash()));
            ConstructionMemberState memberAfter = Assert.Single(after.ConstructionPhysical!.Members);
            Assert.Equal(memberBefore.ElementId, memberAfter.ElementId);
            Assert.Equal(memberBefore.Revision + 1, memberAfter.Revision);
            Assert.Equal(new SubunitPosition(0, 0, -256), memberAfter.Displacement);
            Assert.Equal(ConstructionMemberDisposition.Resting, memberAfter.Disposition);
            Assert.Equal(memberBefore.ConditionParts, memberAfter.ConditionParts);
            Assert.Equal(before.ConstructionPhysical.Revision + 1, after.ConstructionPhysical.Revision);
            Assert.Equal(backing, after.ConstructionPhysical.GetMemberReference(memberAfter.StructureId, memberAfter.MemberIndex));
            Assert.Equal(before.Construction.ContentHash, after.Construction!.ContentHash);
            Assert.Equal(project.Installed, after.Construction.Projects.Single().Installed);
            Assert.Equal(installedMaterial, OpenFantasy.Core.Production.ProductionCodecV1.Encode(after.Production!));
            Assert.Equal(before.StructuralPolicy!.ContentHash, after.StructuralPolicy!.ContentHash);
            Assert.Equal(before.StructuralSettlementPolicy.ContentHash, after.StructuralSettlementPolicy!.ContentHash);
            Assert.True(after.Space.Regions.Single(region => region.Bounds.Contains(fixture.Site.Cell)).GetCell(fixture.Site.Cell).IsExcavated);
            Assert.Equal(1UL, after.Excavation!.Sites.Single().CompletedWork);
            var issued = Assert.Single(after.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
            Assert.Equal(fixture.Site.Quantity, issued.Quantity);
            Assert.Equal(after.GetResident(fixture.Player).InventoryCustodianId, issued.CustodianId);
            Assert.Equal(EntityId.DeriveSpawned(batch.Actions.Single().CommandId, 0), issued.LotId);
            Assert.Equal(new[] { issued.LotId }, decision.Identities.Where(identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity)
                .Select(identity => identity.Identity));
            AssertAdvance(before, after);
            Assert.Equal(Math.Min(GatherRulesV1.MaximumEffort, before.Gathering!.GetActorWork(fixture.Player).Effort
                - GatherRulesV1.EffortPerSwing + GatherRulesV1.EffortPerAdvancingStep), after.Gathering!.GetActorWork(fixture.Player).Effort);
            StructuralLoadResolution stable = Finish(after.StructuralPolicy.Bind(after).Graph);
            Assert.Empty(stable.Failures);
            Assert.Equal(before.StructuralPolicy.Bind(before).Graph.TotalInputMass
                + (UInt128)fixture.Site.Quantity * fixture.Site.Material.Physical.MassPerUnit.Value, stable.Graph.TotalInputMass);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(batch));
            Assert.Equal(after.ContentHash, authority.InspectForHost().State.ContentHash);
        }
        RetainAfter(files.Path, fixture, before, after, batch, receiptBytes, legacy: false);

        WorldSnapshot retried;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            Assert.Equal(receiptBytes, WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(authority.FindReceipt(
                batch.Actions.Single(), ConstructionUpgradeFixture.Delivery(after, fixture.Player)))));
            WorldStepDecision again = authority.Commit(fixture.NextExcavation(after));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(again.Receipts).Rejection);
            retried = again.Candidate;
            AssertUnchangedAction(after, retried);
            Assert.DoesNotContain(again.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
            Assert.Equal(fixture.Site.Quantity, retried.Inventory.Custody.Lots.Where(lot => lot.Source.SourceId == fixture.Site.CountedSourceId)
                .Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity)));
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            foreach (WorldSnapshot expected in new[] { retried, after, before, fixture.Genesis })
            {
                WorldSnapshot recorded = authority.ReadRecordedStateForHost(expected.Revision).State;
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded));
            }
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void A_funded_overhang_cannot_sweep_through_the_excavator_when_its_support_is_removed()
    {
        var fixture = new StructuralSettlementWorldFixture(overhangCollision: true);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        Assert.Equal(2, before.ConstructionPhysical!.Members.Count);
        SpatialStructure assembly = Assert.Single(before.Space.Structures);
        SubunitBox beam = Assert.Single(assembly.Boxes, box => box.Minimum.Z == 512);
        TerrainRegionSnapshot pedestal = before.Space.Regions.Single(region => region.Bounds.Contains(fixture.Site.Cell));
        TerrainRegionSnapshot removed = pedestal.Excavate(pedestal.Revision, [fixture.Site.Cell]);
        // Compose out precisely the whole falling assembly, retaining every stationary actor as an obstacle.
        var descent = new VerticalDescentQuery(before.Space.Scope,
            before.Space.Regions.Select(region => ReferenceEquals(region, pedestal) ? removed : region), [], before.Space.Actors);
        VerticalDescentResult contact = descent.QueryBox(beam, 256);
        Assert.Equal(VerticalDescentStatus.ActorContact, contact.Status);
        Assert.InRange(contact.DropSubunits, 1L, 255L);

        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        WorldStepDecision refused = authority.Commit(fixture.NextExcavation(before));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
        AssertUnchangedAction(before, refused.Candidate);
        Assert.DoesNotContain(refused.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.DoesNotContain(refused.Candidate.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        Assert.False(refused.Candidate.Space.Regions.Single(region => region.Bounds.Contains(fixture.Site.Cell)).GetCell(fixture.Site.Cell).IsExcavated);
    }

    [Fact]
    public void The_older_structural_profile_retains_its_refusal_without_a_settlement_policy()
    {
        var fixture = new StructuralSettlementWorldFixture(admitSettlement: false);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldStepBatch batch = fixture.NextExcavation(before);
        RetainBefore(files.Path, fixture, batch, legacy: true);
        WorldSnapshot after;
        byte[] receipt;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepDecision refused = authority.Commit(batch);
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
            after = refused.Candidate;
            receipt = WorldReceiptCodecV1.Encode(refused.Receipts[0]);
            AssertUnchangedAction(before, after);
            Assert.DoesNotContain(refused.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
            Assert.Null(after.StructuralSettlementPolicy);
            Assert.Equal((ushort)14, after.ProfileVersion);
        }
        RetainAfter(files.Path, fixture, before, after, batch, receipt, legacy: true);
    }

    [Theory]
    [InlineData("unsafe_actor")]
    [InlineData("unsafe_member")]
    [InlineData("missing_lower")]
    [InlineData("empty_lower")]
    public void Incomplete_or_unsafe_descent_preserves_material_work_and_all_occupants(string boundary)
    {
        var fixture = new StructuralSettlementWorldFixture(
            lowerSupport: boundary == "missing_lower" ? SettlementLowerSupport.Missing
                : boundary == "empty_lower" ? SettlementLowerSupport.Excavated : SettlementLowerSupport.Rated,
            safeActorDrop: boundary == "unsafe_actor" ? 128 : 256,
            safeMemberDrop: boundary == "unsafe_member" ? 128 : 256);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        WorldStepDecision refused = authority.Commit(fixture.NextExcavation(before));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
        AssertUnchangedAction(before, refused.Candidate);
        Assert.DoesNotContain(refused.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.False(refused.Candidate.Space.Regions.Single(region => region.Bounds.Contains(fixture.Site.Cell)).GetCell(fixture.Site.Cell).IsExcavated);
        Assert.DoesNotContain(refused.Candidate.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        Assert.Equal(refused.Candidate.ContentHash, authority.ReadRecordedStateForHost(refused.Candidate.Revision).State.ContentHash);
    }

    private static void AssertUnchangedAction(WorldSnapshot before, WorldSnapshot after)
    {
        AssertAdvance(before, after);
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.ContentHash, after.ConstructionPhysical!.ContentHash);
        Assert.Equal(before.Excavation!.ContentHash, after.Excavation!.ContentHash);
        Assert.Equal(before.Quests!.ContentHash, after.Quests!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Gathering!.AdvanceStep(after.AdvancingSteps).ContentHash, after.Gathering!.ContentHash);
        Assert.Equal(before.Space.Regions.Select(region => region.ComputeContentHash()), after.Space.Regions.Select(region => region.ComputeContentHash()));
        Assert.Equal(before.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision, actor.Condition.ComputeContentHash())),
            after.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision, actor.Condition.ComputeContentHash())));
    }

    private static void AssertAdvance(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), after.Calendar);
        Assert.All(before.Residents, resident => Assert.Equal(resident.ExperiencedTime.Add(ExposurePacingV1.FromExplorationSteps(1)),
            after.GetResident(resident.ActorId).ExperiencedTime));
    }

    private static StructuralLoadResolution Finish(StructuralLoadGraph graph)
    {
        StructuralLoadResolution resolution = StructuralLoadResolution.Begin(graph, []);
        for (int step = 0; !resolution.IsComplete; step++)
        {
            Assert.True(step < 128, "This finite stable fixture must resolve within its bounded graph work.");
            resolution = resolution.Advance(StructuralLoadResolution.MaximumEvaluationsPerAdvance);
        }
        return resolution;
    }

    private static void RetainBefore(string path, StructuralSettlementWorldFixture fixture, WorldStepBatch batch, bool legacy)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.Copy(path, Path.Combine(directory, legacy ? "legacy-before.sqlite" : "before-settlement.sqlite"), true);
        File.WriteAllBytes(Path.Combine(directory, legacy ? "legacy-genesis.bin" : "genesis.bin"), WorldSnapshotCodec.Encode(fixture.Genesis));
        File.WriteAllBytes(Path.Combine(directory, legacy ? "legacy-step.bin" : "settlement-step.bin"), WorldStepCodec.Encode(batch));
    }

    private static void RetainAfter(string path, StructuralSettlementWorldFixture fixture, WorldSnapshot before,
        WorldSnapshot after, WorldStepBatch batch, byte[] receipt, bool legacy)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.Copy(path, Path.Combine(directory, legacy ? "legacy-after.sqlite" : "after-settlement.sqlite"), true);
        ConstructionMemberState pre = before.ConstructionPhysical!.Members.Single();
        ConstructionMemberState post = after.ConstructionPhysical!.Members.Single();
        File.WriteAllText(Path.Combine(directory, legacy ? "legacy-expected.json" : "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = legacy ? "Legacy structural profile refuses support removal" : "Actual funded one-cell slab and rider settlement through excavation",
            after.ProfileVersion, WorldId = after.WorldId.ToString(), BranchId = after.BranchId.ToString(),
            PreRevision = before.Revision, PreHash = before.ContentHash.Value, PostRevision = after.Revision, PostHash = after.ContentHash.Value,
            Player = fixture.Player.ToString(), Builder = fixture.Builder.ToString(),
            CommandId = batch.Actions.Single().CommandId.ToString(), ReceiptHex = Convert.ToHexString(receipt),
            PrePlayerFeet = Coordinates(before.Space.GetActor(fixture.Player).Feet), PostPlayerFeet = Coordinates(after.Space.GetActor(fixture.Player).Feet),
            PreBuilderFeet = Coordinates(before.Space.GetActor(fixture.Builder).Feet), PostBuilderFeet = Coordinates(after.Space.GetActor(fixture.Builder).Feet),
            StructureId = pre.StructureId.ToString(), pre.MemberIndex, ElementId = pre.ElementId.ToString(),
            PreMemberRevision = pre.Revision, PostMemberRevision = post.Revision,
            PreDisplacement = Coordinates(pre.Displacement), PostDisplacement = Coordinates(post.Displacement),
            PhysicalBeforeRevision = before.ConstructionPhysical.Revision, PhysicalAfterRevision = after.ConstructionPhysical.Revision,
            PrePhysicalHash = before.ConstructionPhysical.ContentHash.Value, PostPhysicalHash = after.ConstructionPhysical.ContentHash.Value,
            PreConstructionHash = before.Construction!.ContentHash.Value, PostConstructionHash = after.Construction!.ContentHash.Value,
            PreProductionHash = before.Production!.ContentHash.Value, PostProductionHash = after.Production!.ContentHash.Value,
            PreInventoryHash = before.Inventory.ContentHash.Value, PostInventoryHash = after.Inventory.ContentHash.Value,
            PreCustodyHash = before.Inventory.Custody.ContentHash.Value, PostCustodyHash = after.Inventory.Custody.ContentHash.Value,
            SiteId = fixture.Site.SiteId.ToString(), PreWork = before.Excavation!.Sites.Single().CompletedWork,
            PostWork = after.Excavation!.Sites.Single().CompletedWork,
            LotId = EntityId.DeriveSpawned(batch.Actions.Single().CommandId, 0).ToString(), SourceId = fixture.Site.CountedSourceId.ToString(),
            TerrainSourceId = fixture.Site.TerrainSourceId.ToString(), fixture.Site.Quantity,
            DestinationCustodianId = after.GetResident(fixture.Player).InventoryCustodianId.ToString(),
            PolicyHash = after.StructuralPolicy!.ContentHash.Value, SettlementPolicyHash = after.StructuralSettlementPolicy?.ContentHash.Value,
            History = new[] { fixture.Genesis, before, after }.Select(world => new { world.Revision, Hash = world.ContentHash.Value })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static object Coordinates(SubunitPosition position) => new { position.X, position.Y, position.Z };

    private static string? EvidenceDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_STRUCTURAL_SETTLEMENT_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) { return null; }
        Directory.CreateDirectory(directory);
        return directory;
    }
}
