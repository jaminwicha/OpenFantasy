using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class CoupledSettlementWorldTests
{
    [Fact]
    public void Actual_funded_stacked_assemblies_and_their_rider_settle_together_after_saved_geometric_continuations()
    {
        var fixture = new CoupledSettlementWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        Assert.Equal((ushort)17, before.ProfileVersion);
        Assert.Equal(StructureSupportMode.TerrainAndConstruction, before.Space.StructureSupport);
        Assert.Equal(2, before.Construction!.Projects.Count);
        Assert.All(before.Construction.Projects, project => Assert.True(project.IsCompleted));
        Assert.Equal(2, before.Construction.Projects.Select(project => project.Installed!.GuardLotId).Distinct().Count());
        Assert.Equal(CoupledSettlementWorldFixture.RiderFeet, before.Space.GetActor(fixture.Builder).Feet);
        Assert.Equal(12UL, before.Production!.Batches.SelectMany(batch => batch.Ingredients)
            .Aggregate(0UL, (sum, ingredient) => checked(sum + ingredient.Quantity)));
        AssertStableLoads(before);
        AssertPermutation(before);

        WorldStepBatch trigger = RoundTrip(fixture.NextExcavation(before));
        WorldActionCommand command = Assert.Single(trigger.Actions);
        var history = fixture.PreparationHistory.ToList();
        var continuations = new List<WorldStepBatch>();
        RetainStart(files.Path, fixture, trigger);
        WorldSnapshot state;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepDecision started = authority.Commit(trigger);
            state = started.Candidate;
            Assert.Empty(started.Receipts);
            Assert.Null(authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(state, fixture.Player)));
            Assert.Equal(new[] { WorldCommittedIdentityKind.CommitCause, WorldCommittedIdentityKind.Command },
                started.Identities.Select(identity => identity.Kind).Order());
        }
        PendingExcavation initial = Assert.IsType<PendingExcavation>(state.PendingExcavation);
        Assert.False(initial.Resolution.IsComplete);
        Assert.Equal(0UL, initial.Resolution.CompletedWorkUnits);
        Assert.Equal(before.ContentHash, initial.BasisHash);
        Assert.Equal(before.Revision, initial.BasisRevision);
        Assert.Equal(before.CoupledSettlementPolicy!.MaximumWorkUnits, initial.Resolution.MaximumTotalWorkUnits);
        AssertUnchangedPending(before, state);
        history.Add(state);
        CopyEvidence(files.Path, "pending.sqlite");

        WorldActionReceipt? terminal = null;
        for (ulong count = 0; state.PendingExcavation is not null; count++)
        {
            Assert.True(count < initial.MaximumContinuationCount, "Real retained work must terminate within its finite policy bound.");
            CopyEvidence(files.Path, $"pending-{count}.sqlite");
            PendingExcavation previous = state.PendingExcavation!;
            using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
            Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            if (count == 0)
            {
                Assert.Throws<AuthorityDispatchException>(() => authority.Commit(trigger));
                Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Actions.Batch(state, [], [])));
                foreach (EntityId occupied in new[] { previous.Command.CommandId, previous.TriggerCauseId,
                    previous.ReservedSourceId, previous.ResultReservations[0], previous.ResultReservations[^1] })
                {
                    Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Continue(state, occupied)));
                    Assert.Equal(state.ContentHash, authority.InspectForHost().State.ContentHash);
                }
            }
            WorldStepBatch continuation = RoundTrip(fixture.Continue(state));
            continuations.Add(continuation);
            WorldStepDecision next = authority.Commit(continuation);
            state = next.Candidate;
            history.Add(state);
            if (state.PendingExcavation is { } pending)
            {
                Assert.Empty(next.Receipts);
                Assert.Equal(previous.ContinuationCount + 1, pending.ContinuationCount);
                Assert.Equal(previous.Resolution.CompletedWorkUnits + 1, pending.Resolution.CompletedWorkUnits);
                Assert.Equal(initial.ResultReservations, pending.ResultReservations);
                Assert.Equal(WorldCommittedIdentityKind.CommitCause, Assert.Single(next.Identities).Kind);
                AssertUnchangedPending(before, state);
            }
            else
            {
                terminal = Assert.Single(next.Receipts);
                Assert.Equal(WorldActionRejection.None, terminal.Rejection);
                Assert.Equal(command.CommandId, terminal.CommandId);
                Assert.Equal(state.Revision, terminal.CommitSequence);
                Assert.Contains(terminal.ResultId, initial.ResultReservations);
                Assert.Equal(new[] { initial.ReservedSourceId }, next.Identities
                    .Where(identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity).Select(identity => identity.Identity));
            }
        }
        Assert.True(continuations.Count > 3, "The coupled member and rider sweeps must execute real intermediate work.");
        WorldActionReceipt receipt = Assert.IsType<WorldActionReceipt>(terminal);
        AssertApplied(before, state, fixture, receipt);
        AssertStableLoads(state);
        AssertPermutation(state);
        RetainEnd(files.Path, fixture, before, state, trigger, continuations, receipt, history);

        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
                authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(state, fixture.Player)))));
            foreach (WorldSnapshot expected in history.DistinctBy(snapshot => snapshot.Revision).Reverse())
            {
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(
                    authority.ReadRecordedStateForHost(expected.Revision).State));
            }
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(continuations[^1]));
            Assert.Equal(state.ContentHash, authority.InspectForHost().State.ContentHash);
        }
    }

    [Fact]
    public void An_assembly_limit_refusal_retains_all_geometry_and_material_and_charges_only_the_reserved_interval()
    {
        var fixture = new CoupledSettlementWorldFixture(maximumAssemblies: 1);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldStepBatch trigger = fixture.NextExcavation(before);
        WorldActionCommand command = Assert.Single(trigger.Actions);
        WorldSnapshot pending;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepDecision start = authority.Commit(trigger);
            pending = start.Candidate;
            Assert.Empty(start.Receipts);
            AssertUnchangedPending(before, pending);
            Assert.True(Assert.IsType<PendingExcavation>(pending.PendingExcavation).Resolution.IsFailed);
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(pending), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        WorldStepDecision terminal = reopened.Commit(fixture.Continue(pending));
        WorldSnapshot after = terminal.Candidate;
        WorldActionReceipt receipt = Assert.Single(terminal.Receipts);
        Assert.Equal(command.CommandId, receipt.CommandId);
        Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection);
        Assert.Null(after.PendingExcavation);
        AssertSingleInterval(before, after);
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.ContentHash, after.ConstructionPhysical!.ContentHash);
        Assert.Equal(before.Excavation!.ContentHash, after.Excavation!.ContentHash);
        Assert.Equal(before.Gathering!.AdvanceStep(after.AdvancingSteps).ContentHash, after.Gathering!.ContentHash);
        Assert.Equal(before.Space.Regions.Select(region => region.ComputeContentHash()), after.Space.Regions.Select(region => region.ComputeContentHash()));
        Assert.Equal(before.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision)),
            after.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision)));
        Assert.DoesNotContain(terminal.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
            reopened.FindReceipt(command, ConstructionUpgradeFixture.Delivery(after, fixture.Player)))));
    }

    private static void AssertApplied(WorldSnapshot before, WorldSnapshot after, CoupledSettlementWorldFixture fixture,
        WorldActionReceipt receipt)
    {
        Assert.Equal((ushort)17, after.ProfileVersion);
        Assert.Null(after.PendingExcavation);
        AssertSingleInterval(before, after);
        Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Quests!.ContentHash, after.Quests!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.Revision + 1, after.ConstructionPhysical!.Revision);
        foreach (ConstructionMemberState member in before.ConstructionPhysical.Members)
        {
            ConstructionMemberState next = after.ConstructionPhysical.GetMember(member.StructureId, member.MemberIndex);
            Assert.Equal(member.ElementId, next.ElementId);
            Assert.Equal(member.Revision + 1, next.Revision);
            Assert.Equal(member.ConditionParts, next.ConditionParts);
            Assert.Equal(new SubunitPosition(0, 0, -256), next.Displacement);
            Assert.Equal(ConstructionMemberDisposition.Resting, next.Disposition);
            Assert.Equal(before.ConstructionPhysical.GetMemberReference(member.StructureId, member.MemberIndex),
                after.ConstructionPhysical.GetMemberReference(member.StructureId, member.MemberIndex));
        }
        foreach (ActorPresence actor in before.Space.Actors)
        {
            ActorPresence next = after.Space.GetActor(actor.ActorId);
            bool rider = actor.ActorId == fixture.Builder;
            Assert.Equal(rider ? actor.Feet.Offset(0, 0, -256) : actor.Feet, next.Feet);
            Assert.Equal(actor.Revision + (rider ? 1UL : 0UL), next.Revision);
            Assert.Equal(actor.Condition.ComputeContentHash(), next.Condition.ComputeContentHash());
            Assert.Equal(actor.Binding, next.Binding);
            uint spent = actor.ActorId == fixture.Player ? GatherRulesV1.EffortPerSwing : 0;
            Assert.Equal(Math.Min(GatherRulesV1.MaximumEffort,
                before.Gathering!.GetActorWork(actor.ActorId).Effort - spent + GatherRulesV1.EffortPerAdvancingStep),
                after.Gathering!.GetActorWork(actor.ActorId).Effort);
        }
        Assert.Equal(1UL, Assert.Single(after.Excavation!.Sites).CompletedWork);
        CountedSourceLot source = Assert.Single(after.Inventory.Custody.Lots,
            lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        Assert.Equal(EntityId.DeriveSpawned(receipt.CommandId, 0), source.LotId);
        Assert.Equal(after.GetResident(fixture.Player).InventoryCustodianId, source.CustodianId);
        Assert.Equal(fixture.Site.Material.Item, source.Definition);
        Assert.Equal(new SourceInterval(fixture.Site.CountedSourceId, 0, fixture.Site.Quantity), source.Source);
        Assert.Equal(0UL, source.Revision);
        Assert.Equal(LotProtection.None, source.Protection);
        Assert.Null(source.ReservedBy);
        Assert.Equal(before.Inventory.Custody.Lots.Count + 1, after.Inventory.Custody.Lots.Count);
        Assert.All(before.Inventory.Custody.Lots, lot => Assert.Equal(lot, after.Inventory.Custody.GetLot(lot.LotId)));
        int changedCells = 0;
        for (int regionIndex = 0; regionIndex < before.Space.Regions.Count; regionIndex++)
        {
            TerrainRegionSnapshot oldRegion = before.Space.Regions[regionIndex];
            TerrainRegionSnapshot newRegion = after.Space.Regions[regionIndex];
            Assert.Equal(oldRegion.Bounds, newRegion.Bounds);
            Assert.Equal(oldRegion.Revision + (oldRegion.Bounds.Contains(fixture.Site.Cell) ? 1UL : 0UL), newRegion.Revision);
            foreach (TerrainCell cell in oldRegion.Cells)
            {
                TerrainCell next = newRegion.GetCell(cell.Coordinate);
                Assert.Equal(cell.SourceId, next.SourceId);
                Assert.Equal(cell.FormationId, next.FormationId);
                Assert.Equal(cell.Material, next.Material);
                Assert.Equal(cell.Stratum, next.Stratum);
                if (cell.IsExcavated != next.IsExcavated)
                {
                    changedCells++;
                    Assert.Equal(fixture.Site.Cell, cell.Coordinate);
                    Assert.True(cell.IsSolid);
                    Assert.True(next.IsExcavated);
                }
            }
        }
        Assert.Equal(1, changedCells);
        UInt128 extracted = checked((UInt128)fixture.Site.Quantity * fixture.Site.Material.Physical.MassPerUnit.Value);
        Assert.Equal(RepresentedMass(before) + extracted, RepresentedMass(after));
    }

    private static void AssertStableLoads(WorldSnapshot world)
    {
        ConstructionBearingModel model = world.StructuralPolicy!.Bind(world);
        StructuralLoadResolution result = StructuralLoadResolution.Begin(model.Graph, []);
        for (int count = 0; !result.IsComplete; count++)
        {
            Assert.True(count < 128);
            result = result.Advance();
        }
        Assert.Empty(result.Failures);
        Assert.Equal(RepresentedMass(world), model.Graph.TotalInputMass);
        Assert.Equal(model.Graph.TotalInputMass, result.AcceptedFoundationLoad);
        Assert.Equal((UInt128)0, result.FailedInputMass);
    }

    private static UInt128 RepresentedMass(WorldSnapshot world) => world.ConstructionPhysical!.Members.Aggregate((UInt128)0,
        (sum, member) => sum + world.ConstructionPhysical.GetMemberReference(member.StructureId, member.MemberIndex).Mass.Value)
        + world.Space.Actors.Aggregate((UInt128)0, (sum, actor) => sum + world.StructuralPolicy!.Masses.ActorLoad(world, actor.ActorId));

    private static void AssertUnchangedPending(WorldSnapshot before, WorldSnapshot pending)
    {
        Assert.NotNull(pending.PendingExcavation);
        Assert.Equal((ushort)17, pending.ProfileVersion);
        var stable = new WorldSnapshot(pending.Inventory, pending.Space, pending.Residents, pending.AdmissionVersion,
            pending.Calendar, before.Revision, pending.AdvancingSteps, pending.Dialogues, pending.Gathering,
            pending.Production, pending.Progression, pending.Quests, pending.Construction, excavation: pending.Excavation,
            constructionPhysical: pending.ConstructionPhysical, structuralPolicy: pending.StructuralPolicy,
            structuralSettlementPolicy: pending.StructuralSettlementPolicy, consequencePolicy: pending.ConsequencePolicy,
            coupledSettlementPolicy: pending.CoupledSettlementPolicy);
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(stable));
    }

    private static void AssertSingleInterval(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), after.Calendar);
        Assert.All(before.Residents, resident => Assert.Equal(resident.ExperiencedTime.Add(ExposurePacingV1.FromExplorationSteps(1)),
            after.GetResident(resident.ActorId).ExperiencedTime));
    }

    private static void AssertPermutation(WorldSnapshot world)
    {
        var construction = new ConstructionSnapshot(world.WorldId, world.BranchId, world.Construction!.Revision,
            world.Construction.Definitions.Reverse(), world.Construction.Projects.Reverse());
        var physical = new ConstructionPhysicalSnapshot(construction, world.ConstructionPhysical!.Revision,
            world.ConstructionPhysical.Members.Reverse());
        var space = new LocalSpatialSnapshot(world.Space.Scope, world.Space.Regions.Reverse(), world.Space.Actors.Reverse(),
            physical.GetStructures().Reverse(), world.Space.GroundSupport, world.Space.StructureSupport);
        ConstructionLoadPolicy old = world.StructuralPolicy!;
        var policy = new ConstructionLoadPolicy(old.Scope, old.Templates.Reverse(), old.Foundations.Reverse(),
            new(old.Masses.Bodies.Reverse(), old.Masses.SupplementalItems.Reverse()));
        var reversed = new WorldSnapshot(world.Inventory, space, world.Residents.Reverse(), world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production,
            world.Progression, world.Quests, construction, excavation: world.Excavation, constructionPhysical: physical,
            structuralPolicy: policy, structuralSettlementPolicy: world.StructuralSettlementPolicy,
            consequencePolicy: world.ConsequencePolicy, coupledSettlementPolicy: world.CoupledSettlementPolicy);
        Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(reversed));
    }

    private static WorldStepBatch RoundTrip(WorldStepBatch batch)
    {
        byte[] bytes = WorldStepCodec.Encode(batch);
        WorldStepBatch restored = WorldStepCodec.Decode(bytes);
        Assert.Equal(bytes, WorldStepCodec.Encode(restored));
        return restored;
    }

    private static void RetainStart(string path, CoupledSettlementWorldFixture fixture, WorldStepBatch trigger)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.WriteAllBytes(Path.Combine(directory, "genesis.bin"), WorldSnapshotCodec.Encode(fixture.Genesis));
        File.WriteAllBytes(Path.Combine(directory, "trigger.bin"), WorldStepCodec.Encode(trigger));
        CopyEvidence(path, "before.sqlite");
    }

    private static void RetainEnd(string path, CoupledSettlementWorldFixture fixture, WorldSnapshot before,
        WorldSnapshot after, WorldStepBatch trigger, IReadOnlyList<WorldStepBatch> continuations,
        WorldActionReceipt receipt, IReadOnlyList<WorldSnapshot> history)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        CopyEvidence(path, "after.sqlite");
        for (int index = 0; index < continuations.Count; index++)
        {
            File.WriteAllBytes(Path.Combine(directory, $"continuation-{index + 1:D3}.bin"), WorldStepCodec.Encode(continuations[index]));
        }
        WorldSnapshot firstPending = history.First(state => state.PendingExcavation is not null);
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Two actually funded and constructed intact assemblies settle 256 subunits with a carried rider; no fracture or arbitrary impact law",
            after.ProfileVersion, WorldId = after.WorldId.ToString(), BranchId = after.BranchId.ToString(),
            SiteId = fixture.Site.SiteId.ToString(), SourceId = fixture.Site.CountedSourceId.ToString(),
            TerrainSourceId = fixture.Site.TerrainSourceId.ToString(), Player = fixture.Player.ToString(), Builder = fixture.Builder.ToString(),
            BeforeRevision = before.Revision, BeforeHash = before.ContentHash.Value,
            PendingRevision = firstPending.Revision, PendingHash = firstPending.ContentHash.Value,
            AfterRevision = after.Revision, AfterHash = after.ContentHash.Value,
            PreRevision = before.Revision, PreHash = before.ContentHash.Value,
            PostRevision = after.Revision, PostHash = after.ContentHash.Value,
            TriggerCauseId = trigger.CauseId.ToString(), CommandId = receipt.CommandId.ToString(),
            ReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)), Continuations = continuations.Count,
            TriggerFile = "trigger.bin", BeforeFile = "before.sqlite", AfterFile = "after.sqlite",
            ContinuationFiles = Enumerable.Range(1, continuations.Count).Select(index => $"continuation-{index:D3}.bin"),
            PendingFiles = Enumerable.Range(0, continuations.Count).Select(index => $"pending-{index}.sqlite"),
            BeforeSteps = before.AdvancingSteps, AfterSteps = after.AdvancingSteps,
            SourceLotId = EntityId.DeriveSpawned(receipt.CommandId, 0).ToString(),
            LowerStructureId = after.Construction!.FindProject(CoupledSettlementWorldFixture.LowerProjectId)!.Installed!.StructureId.ToString(),
            UpperStructureId = after.Construction.FindProject(CoupledSettlementWorldFixture.UpperProjectId)!.Installed!.StructureId.ToString(),
            BeforeInventoryHash = before.Inventory.ContentHash.Value, AfterInventoryHash = after.Inventory.ContentHash.Value,
            BeforePhysicalHash = before.ConstructionPhysical!.ContentHash.Value, AfterPhysicalHash = after.ConstructionPhysical!.ContentHash.Value,
            Projects = after.Construction!.Projects.Select(project => new { ProjectId = project.ProjectId.ToString(),
                StructureId = project.Installed!.StructureId.ToString(), InstalledLotId = project.Installed.GuardLotId.ToString(),
                BatchId = project.Installed.ProductionBatchId.ToString() }),
            Actors = after.Space.Actors.Select(actor => new { ActorId = actor.ActorId.ToString(), actor.Feet.X, actor.Feet.Y, actor.Feet.Z, actor.Revision }),
            History = history.DistinctBy(state => state.Revision).Select(state => new { state.Revision, Hash = state.ContentHash.Value })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CopyEvidence(string source, string name)
    {
        string? directory = EvidenceDirectory();
        if (directory is not null) { File.Copy(source, Path.Combine(directory, name), true); }
    }

    private static string? EvidenceDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_COUPLED_SETTLEMENT_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) { return null; }
        Directory.CreateDirectory(directory);
        return directory;
    }
}
