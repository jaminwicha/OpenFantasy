using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class PhysicalConstructionWorldTests
{
    [Fact]
    public void Funded_completion_owns_each_physical_member_once_across_restart_and_upstairs_history()
    {
        var fixture = new StairNavigationWorldFixture(physicalConstruction: true);
        using var files = new StorageFiles();
        CompletionPreparation preparation = PrepareCompletion(fixture, files.Path, fixture.Genesis);
        WorldSnapshot before = preparation.Before;
        byte[] stepBytes = WorldStepCodec.Encode(preparation.Batch);
        Assert.Equal(stepBytes, WorldStepCodec.Encode(WorldStepCodec.Decode(stepBytes)));
        RetainBefore(files.Path, preparation, stepBytes);

        WorldSnapshot completed;
        byte[] receiptBytes;
        EntityId[] expectedMembers;
        WorldActionCommand command = Assert.Single(preparation.Batch.Actions);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            WorldStepDecision decision = authority.Commit(WorldStepCodec.Decode(stepBytes));
            completed = decision.Candidate;
            WorldActionReceipt receipt = Assert.Single(decision.Receipts);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            receiptBytes = WorldReceiptCodecV1.Encode(receipt);
            expectedMembers = AssertCompletedPhysical(completed);
            Assert.Equal(12, expectedMembers.Length);
            Assert.Equal(expectedMembers.Order(), decision.Identities.Where(identity => expectedMembers.Contains(identity.Identity))
                .Select(identity => identity.Identity));
            Assert.All(expectedMembers, identity =>
            {
                Assert.Equal(WorldCommittedIdentityKind.SpawnedEntity,
                    Assert.Single(decision.Identities, value => value.Identity == identity).Kind);
                Assert.True(completed.IdentityIndex.Contains(identity));
            });
            AssertNormalAdvance(before, completed);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(preparation.Batch));
            Assert.Equal(completed.ContentHash, authority.InspectForHost().State.ContentHash);
        }
        RetainCompletion(files.Path, fixture, preparation, completed, expectedMembers, receiptBytes);

        var history = preparation.History.Append(before).Append(completed).DistinctBy(state => state.Revision).ToList();
        WorldSnapshot upstairs;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldSnapshot world = authority.InspectForHost().State;
            Assert.Equal(WorldSnapshotCodec.Encode(completed), WorldSnapshotCodec.Encode(world));
            WorldActionReceipt receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command,
                ConstructionUpgradeFixture.Delivery(world, fixture.Builder)));
            Assert.Equal(receiptBytes, WorldReceiptCodecV1.Encode(receipt));
            byte[] physicalBytes = ConstructionPhysicalCodecV1.Encode(world.ConstructionPhysical!);
            world = fixture.WalkTo(authority, world, fixture.Player, StairNavigationWorldFixture.StairApproach,
                state => Assert.Equal(physicalBytes, ConstructionPhysicalCodecV1.Encode(state.ConstructionPhysical!)));
            history.Add(world);
            upstairs = fixture.WalkTo(authority, world, fixture.Player, StairNavigationWorldFixture.UpperDeck,
                state => Assert.Equal(physicalBytes, ConstructionPhysicalCodecV1.Encode(state.ConstructionPhysical!)));
            Assert.Equal(StairNavigationWorldFixture.UpperDeck, upstairs.Space.GetActor(fixture.Player).Feet);
            Assert.Equal(expectedMembers, AssertCompletedPhysical(upstairs));
            Assert.Equal(completed.Production!.ContentHash, upstairs.Production!.ContentHash);
            history.Add(upstairs);
        }
        RetainOccupied(files.Path, upstairs);

        byte[] closedDatabase = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(upstairs), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            foreach (WorldSnapshot expected in history.AsEnumerable().Reverse())
            {
                RecordedWorldState recorded = authority.ReadRecordedStateForHost(expected.Revision);
                Assert.Equal(upstairs.Revision, recorded.SourceHeadRevision);
                Assert.Equal(upstairs.ContentHash, recorded.SourceHeadHash);
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded.State));
                Assert.Equal(expected.ConstructionPhysical!.ContentHash, recorded.State.ConstructionPhysical!.ContentHash);
                Assert.Equal((ushort)13, recorded.State.ProfileVersion);
            }
        }
        Assert.Equal(closedDatabase, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Historical_member_identity_collision_rolls_back_the_entire_completion_action()
    {
        var fixture = new StairNavigationWorldFixture(physicalConstruction: true);
        using var files = new StorageFiles();
        CompletionPreparation preparation = PrepareCompletion(fixture, files.Path, fixture.Genesis);
        WorldSnapshot before = preparation.Before;
        WorldActionCommand command = Assert.Single(preparation.Batch.Actions);
        ConstructionProject project = Assert.Single(before.Construction!.Projects);
        EntityId structureId = EntityId.DeriveSpawned(command.CommandId,
            checked((ulong)project.Definition.MaterialTransform.Outputs.Count + 1));
        EntityId collision = ConstructionBearingModel.MemberIdentity(structureId, 7);
        Assert.False(before.IdentityIndex.Contains(collision));
        var processor = new WorldStepProcessor();
        WorldStepDecision accepted = processor.Decide(before, preparation.Batch);
        Assert.Equal(WorldActionRejection.None, Assert.Single(accepted.Receipts).Rejection);
        Assert.Contains(collision, AssertCompletedPhysical(accepted.Candidate));

        WorldStepDecision refused = processor.Decide(before, preparation.Batch, identity => identity == collision);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
        WorldSnapshot after = refused.Candidate;
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Construction.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.ContentHash, after.ConstructionPhysical!.ContentHash);
        Assert.Equal(before.Excavation!.ContentHash, after.Excavation!.ContentHash);
        Assert.Equal(before.Quests!.ContentHash, after.Quests!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision)),
            after.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision)));
        Assert.Empty(after.Space.Structures);
        Assert.Empty(after.ConstructionPhysical.Members);
        Assert.Null(Assert.Single(after.Construction.Projects).Installed);
        Assert.DoesNotContain(refused.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.False(after.IdentityIndex.Contains(collision));
        AssertNormalAdvance(before, after);
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(after))));

        // The collision probe prepared private candidates only; it cannot alter the durable pre-completion world.
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
    }

    [Fact]
    public void Partial_funded_work_updates_the_fabrication_pin_without_issuing_members_early()
    {
        var fixture = new StairNavigationWorldFixture();
        WorldSnapshot genesis = ThreeWorkGenesis(fixture.Genesis);
        using var files = new StorageFiles();
        CompletionPreparation preparation = PrepareCompletion(fixture, files.Path, genesis);
        WorldSnapshot[] partial = preparation.History.Where(state => state.Construction!.Revision is 1 or 2)
            .DistinctBy(state => state.Construction!.Revision).OrderBy(state => state.Construction!.Revision).ToArray();
        Assert.Equal(new ulong[] { 1, 2 }, partial.Select(state => state.Construction!.Revision));
        Assert.NotEqual(genesis.ConstructionPhysical!.ContentHash, preparation.Before.ConstructionPhysical!.ContentHash);
        foreach (WorldSnapshot state in partial)
        {
            Assert.Empty(state.ConstructionPhysical!.Members);
            Assert.Equal(0UL, state.ConstructionPhysical.Revision);
            Assert.Equal(state.Construction!.ContentHash, state.ConstructionPhysical.ConstructionHash);
            Assert.Empty(state.Space.Structures);
            Assert.Empty(state.Production!.Batches);
            Assert.Null(Assert.Single(state.Construction.Projects).Installed);
        }
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(preparation.Before), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
        foreach (WorldSnapshot expected in partial)
        {
            Assert.Equal(WorldSnapshotCodec.Encode(expected),
                WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(expected.Revision).State));
        }
        WorldStepDecision completion = authority.Commit(preparation.Batch);
        Assert.Equal(WorldActionRejection.None, Assert.Single(completion.Receipts).Rejection);
        Assert.Equal(12, AssertCompletedPhysical(completion.Candidate).Length);
        Assert.Equal(3UL, completion.Candidate.Construction!.Revision);
    }

    [Fact]
    public void Fresh_physical_admission_preserves_legacy_bytes_and_refuses_other_or_played_profiles()
    {
        var fixture = new StairNavigationWorldFixture();
        byte[] eleven = WorldSnapshotCodec.Encode(fixture.LegacyGenesis);
        byte[] twelve = WorldSnapshotCodec.Encode(fixture.Genesis);
        WorldSnapshot admitted = WorldConstructionPhysicalAdmission.CreateGenesis(fixture.Genesis);
        Assert.Equal((ushort)13, admitted.ProfileVersion);
        Assert.Empty(admitted.ConstructionPhysical!.Members);
        Assert.Equal(admitted.Construction!.ContentHash, admitted.ConstructionPhysical.ConstructionHash);
        Assert.Equal(0UL, admitted.ConstructionPhysical.Revision);
        Assert.NotEqual(fixture.Genesis.AdmissionVersion, admitted.AdmissionVersion);
        byte[] current = WorldSnapshotCodec.Encode(admitted);
        Assert.Equal(current, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(current)));
        Assert.Equal(eleven, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(eleven)));
        Assert.Equal(twelve, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(twelve)));
        Assert.Equal(eleven, WorldSnapshotCodec.Encode(fixture.LegacyGenesis));
        Assert.Equal(twelve, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Null(fixture.LegacyGenesis.ConstructionPhysical);
        Assert.Null(fixture.Genesis.ConstructionPhysical);
        Assert.Throws<ArgumentException>(() => WorldConstructionPhysicalAdmission.CreateGenesis(fixture.LegacyGenesis));
        Assert.Throws<ArgumentException>(() => WorldConstructionPhysicalAdmission.CreateGenesis(admitted));
        WorldSnapshot played = new WorldStepProcessor().Decide(fixture.Genesis,
            fixture.Actions.Batch(fixture.Genesis, [], [])).Candidate;
        Assert.Throws<ArgumentException>(() => WorldConstructionPhysicalAdmission.CreateGenesis(played));
    }

    private static CompletionPreparation PrepareCompletion(StairNavigationWorldFixture fixture, string path, WorldSnapshot genesis)
    {
        Assert.Equal((ushort)13, genesis.ProfileVersion);
        var history = new List<WorldSnapshot> { genesis };
        WorldSnapshot world = fixture.Actions.CreateSource(path, completeQuest: true, genesis: genesis);
        history.Add(world);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(path);
        world = fixture.Actions.Act(authority, world, fixture.Player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
        history.Add(world);
        var controller = new LocalNpcController();
        for (int step = 0; step < 128; step++)
        {
            ConstructionProject project = Assert.Single(world.Construction!.Projects);
            Assert.Null(project.Installed);
            Assert.Empty(world.ConstructionPhysical!.Members);
            Assert.Equal(0UL, world.ConstructionPhysical.Revision);
            Assert.Equal(world.Construction.ContentHash, world.ConstructionPhysical.ConstructionHash);
            WorldActionCommand[] actions = controller.SelectActions(world, fixture.Actions.NextIdentity).ToArray();
            WorldStepBatch batch = fixture.Actions.Batch(world, actions, controller.SelectMovement(world));
            if (actions.Any(action => action.Intent is WorkConstructionIntent)
                && project.Progress.CompletedUnits + 1 == project.Definition.RequiredWork.Value)
            {
                Assert.Equal(fixture.Builder, Assert.Single(actions).ActorId);
                Assert.Empty(batch.Movement);
                return new(genesis, world, batch, history);
            }
            WorldStepDecision decision = authority.Commit(batch);
            Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            world = decision.Candidate;
            if (step == 0 || actions.Length != 0) { history.Add(world); }
        }
        throw new InvalidOperationException("The actual funded NPC must reach its final construction action within the fixture bound.");
    }

    private static EntityId[] AssertCompletedPhysical(WorldSnapshot world)
    {
        Assert.Equal((ushort)13, world.ProfileVersion);
        ConstructionProject project = Assert.Single(world.Construction!.Projects);
        InstalledStructure installed = Assert.IsType<InstalledStructure>(project.Installed);
        ConstructionPhysicalSnapshot physical = Assert.IsType<ConstructionPhysicalSnapshot>(world.ConstructionPhysical);
        Assert.Equal(1UL, physical.Revision);
        Assert.Equal(world.Construction.ContentHash, physical.ConstructionHash);
        EntityId[] expected = Enumerable.Range(0, project.Definition.Members.Count)
            .Select(index => ConstructionBearingModel.MemberIdentity(installed.StructureId, (uint)index)).ToArray();
        Assert.Equal(expected, physical.Members.Select(member => member.ElementId));
        Assert.All(physical.Members, member =>
        {
            Assert.Equal(0UL, member.Revision);
            Assert.Equal(StructuralElement.MaximumConditionParts, member.ConditionParts);
            Assert.Equal(default(SubunitPosition), member.Displacement);
            Assert.Equal(ConstructionMemberDisposition.Standing, member.Disposition);
            StructuralMemberReference reference = physical.GetMemberReference(member.StructureId, member.MemberIndex);
            Assert.Equal(installed.GuardLotId, reference.InstalledLotId);
            Assert.Equal(project.ProjectId, reference.ProjectId);
            Assert.Equal(project.Definition.Members[(int)member.MemberIndex].VolumeMicrolitres, reference.Volume.Value);
        });
        SpatialStructure geometry = Assert.Single(physical.GetStructures());
        SpatialStructure actual = Assert.Single(world.Space.Structures);
        Assert.Equal(geometry.StructureId, actual.StructureId);
        Assert.Equal(geometry.Boxes, actual.Boxes);
        Assert.Equal(geometry.BearingSurfaces, actual.BearingSurfaces);
        Assert.Single(world.Production!.Batches);
        return expected;
    }

    private static void AssertNormalAdvance(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), after.Calendar);
        foreach (WorldResident resident in before.Residents)
        {
            Assert.Equal(resident.ExperiencedTime.Add(ExposurePacingV1.FromExplorationSteps(1)),
                after.GetResident(resident.ActorId).ExperiencedTime);
            Assert.Equal(Math.Min(GatherRulesV1.MaximumEffort,
                before.Gathering!.GetActorWork(resident.ActorId).Effort + GatherRulesV1.EffortPerAdvancingStep),
                after.Gathering!.GetActorWork(resident.ActorId).Effort);
        }
    }

    private static WorldSnapshot ThreeWorkGenesis(WorldSnapshot basis)
    {
        ConstructionProject original = Assert.Single(basis.Construction!.Projects);
        ConstructionDefinition old = original.Definition;
        var definition = new ConstructionDefinition(old.DefinitionId, old.Key, old.DisplayName, old.MaterialTransform,
            old.InstalledOutputIndex, old.RequiredTool, old.Members, new(3), old.WorkRate, old.StockInterpretation);
        var project = new ConstructionProject(original.ProjectId, definition, original.OwnerActorId, original.BuilderActorId,
            original.QuestId, original.ProjectCustodianId, original.ReservationId, original.SiteFeet, original.WorkFeet,
            original.RequiredAccess);
        var construction = new ConstructionSnapshot(basis.WorldId, basis.BranchId, 0, [definition], [project]);
        var authored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
            basis.Calendar, 0, 0, gathering: basis.Gathering, production: basis.Production, progression: basis.Progression,
            quests: basis.Quests, construction: construction, excavation: basis.Excavation);
        return WorldConstructionPhysicalAdmission.CreateGenesis(authored);
    }

    private static void RetainBefore(string path, CompletionPreparation preparation, byte[] stepBytes)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.Copy(path, Path.Combine(directory, "before-completion.sqlite"), overwrite: true);
        File.WriteAllBytes(Path.Combine(directory, "genesis.bin"), WorldSnapshotCodec.Encode(preparation.Genesis));
        File.WriteAllBytes(Path.Combine(directory, "completion-step.bin"), stepBytes);
    }

    private static void RetainCompletion(string path, StairNavigationWorldFixture fixture,
        CompletionPreparation preparation, WorldSnapshot completed, EntityId[] expectedMembers, byte[] receiptBytes)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.Copy(path, Path.Combine(directory, "after-completion.sqlite"), overwrite: true);
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Actual quest-funded NPC completion with twelve pristine physical members; no damage or collapse command",
            ProfileVersion = 13,
            WorldId = completed.WorldId.ToString(),
            BranchId = completed.BranchId.ToString(),
            PreRevision = preparation.Before.Revision,
            PreHash = preparation.Before.ContentHash.Value,
            PostRevision = completed.Revision,
            PostHash = completed.ContentHash.Value,
            Player = fixture.Player.ToString(),
            Builder = fixture.Builder.ToString(),
            CommandId = Assert.Single(preparation.Batch.Actions).CommandId.ToString(),
            ReceiptHex = Convert.ToHexString(receiptBytes),
            StructureId = Assert.Single(completed.Construction!.Projects).Installed!.StructureId.ToString(),
            ExpectedMemberIds = expectedMembers.Select(identity => identity.ToString()),
            MemberCount = expectedMembers.Length,
            PhysicalHash = completed.ConstructionPhysical!.ContentHash.Value,
            PhysicalRevision = completed.ConstructionPhysical.Revision,
            PhysicalBeforeRevision = preparation.Before.ConstructionPhysical!.Revision,
            PhysicalAfterRevision = completed.ConstructionPhysical.Revision,
            PreConstructionHash = preparation.Before.Construction!.ContentHash.Value,
            PostConstructionHash = completed.Construction.ContentHash.Value,
            PreProductionHash = preparation.Before.Production!.ContentHash.Value,
            PostProductionHash = completed.Production!.ContentHash.Value,
            PreInventoryHash = preparation.Before.Inventory.ContentHash.Value,
            PostInventoryHash = completed.Inventory.ContentHash.Value,
            PreCustodyHash = preparation.Before.Inventory.Custody.ContentHash.Value,
            PostCustodyHash = completed.Inventory.Custody.ContentHash.Value,
            History = preparation.History.Append(preparation.Before).Append(completed).DistinctBy(state => state.Revision)
                .Select(state => new { state.Revision, Hash = state.ContentHash.Value })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void RetainOccupied(string path, WorldSnapshot upstairs)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.Copy(path, Path.Combine(directory, "occupied-upstairs.sqlite"), overwrite: true);
        File.WriteAllBytes(Path.Combine(directory, "occupied-upstairs.bin"), WorldSnapshotCodec.Encode(upstairs));
    }

    private static string? EvidenceDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_CONSTRUCTION_PHYSICAL_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) { return null; }
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record CompletionPreparation(WorldSnapshot Genesis, WorldSnapshot Before,
        WorldStepBatch Batch, IReadOnlyList<WorldSnapshot> History);
}
