using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class StructuralPolicyWorldTests
{
    [Fact]
    public void Funded_completion_and_occupied_travel_retain_the_complete_policy_through_recovery_and_history()
    {
        var fixture = new StructuralPolicyWorldFixture();
        using var files = new StorageFiles();
        UInt128 originalMass = fixture.Genesis.StructuralPolicy!.Bind(fixture.Genesis).Graph.TotalInputMass;
        Completion preparation = PrepareCompletion(fixture.Stairs, fixture.Genesis, files.Path);
        var funded = preparation.Before.StructuralPolicy!.Bind(preparation.Before);
        Assert.Equal(originalMass, funded.Graph.TotalInputMass);
        string? directory = EvidenceDirectory();
        if (directory is not null)
        {
            File.Copy(files.Path, Path.Combine(directory, "before-completion.sqlite"), true);
            File.WriteAllBytes(Path.Combine(directory, "genesis.bin"), WorldSnapshotCodec.Encode(fixture.Genesis));
            File.WriteAllBytes(Path.Combine(directory, "completion-step.bin"), WorldStepCodec.Encode(preparation.Batch));
        }
        WorldSnapshot completed;
        byte[] receipt;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            byte[] encoded = WorldStepCodec.Encode(preparation.Batch);
            Assert.Equal(encoded, WorldStepCodec.Encode(WorldStepCodec.Decode(encoded)));
            WorldStepDecision decision = authority.Commit(WorldStepCodec.Decode(encoded));
            completed = decision.Candidate;
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            receipt = WorldReceiptCodecV1.Encode(decision.Receipts[0]);
            Assert.Equal(12, completed.ConstructionPhysical!.Members.Count);
            Assert.Equal(fixture.Genesis.StructuralPolicy!.ContentHash, completed.StructuralPolicy!.ContentHash);
            ConstructionBearingModel installed = completed.StructuralPolicy.Bind(completed);
            Assert.Equal(originalMass, installed.Graph.TotalInputMass);
            UInt128 memberMass = installed.Members.Aggregate((UInt128)0, (total, member) => total + member.Mass.Value);
            Assert.Equal(memberMass, (UInt128)funded.Actors.Single(value => value.ActorId == fixture.Stairs.Builder).BodyAndCarriedMass.Value
                - installed.Actors.Single(value => value.ActorId == fixture.Stairs.Builder).BodyAndCarriedMass.Value);
            AssertNormalAdvance(preparation.Before, completed);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(preparation.Batch));
        }
        if (directory is not null)
        {
            File.Copy(files.Path, Path.Combine(directory, "after-completion.sqlite"), true);
            RetainExpected(directory, fixture, preparation, completed, receipt);
        }

        WorldSnapshot upstairs;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldSnapshot reopened = authority.InspectForHost().State;
            Assert.Equal(WorldSnapshotCodec.Encode(completed), WorldSnapshotCodec.Encode(reopened));
            upstairs = fixture.Stairs.WalkTo(authority, reopened, fixture.Stairs.Player,
                StairNavigationWorldFixture.UpperDeck, world =>
                {
                    Assert.Equal(fixture.Genesis.StructuralPolicy!.ContentHash, world.StructuralPolicy!.ContentHash);
                    Assert.Equal((ushort)14, world.ProfileVersion);
                });
            Assert.Equal(originalMass, upstairs.StructuralPolicy!.Bind(upstairs).Graph.TotalInputMass);
        }
        if (directory is not null)
        {
            File.Copy(files.Path, Path.Combine(directory, "occupied-upstairs.sqlite"), true);
            File.WriteAllBytes(Path.Combine(directory, "occupied-upstairs.bin"), WorldSnapshotCodec.Encode(upstairs));
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            foreach (WorldSnapshot expected in new[] { upstairs, completed, preparation.Before, fixture.Genesis })
            {
                WorldSnapshot recorded = authority.ReadRecordedStateForHost(expected.Revision).State;
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded));
                Assert.Equal(fixture.Genesis.StructuralPolicy!.ContentHash, recorded.StructuralPolicy!.ContentHash);
            }
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Self_overloaded_completion_refuses_all_material_work_and_identities_before_publication()
    {
        var fixture = new StructuralPolicyWorldFixture(_ => 1);
        using var files = new StorageFiles();
        Completion preparation = PrepareCompletion(fixture.Stairs, fixture.Genesis, files.Path);
        WorldSnapshot before = preparation.Before;
        var processor = new WorldStepProcessor();
        WorldSnapshot legacy = WithoutPolicy(before);
        WorldStepDecision control = processor.Decide(legacy, Reprofile(preparation.Batch, 13));
        Assert.Equal(WorldActionRejection.None, Assert.Single(control.Receipts).Rejection);
        Assert.Equal(12, control.Candidate.ConstructionPhysical!.Members.Count);

        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        WorldStepDecision refused = authority.Commit(preparation.Batch);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
        WorldSnapshot after = refused.Candidate;
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.ContentHash, after.ConstructionPhysical!.ContentHash);
        Assert.Equal(before.Excavation!.ContentHash, after.Excavation!.ContentHash);
        Assert.Equal(before.Quests!.ContentHash, after.Quests!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Empty(after.ConstructionPhysical.Members);
        Assert.Empty(after.Space.Structures);
        Assert.DoesNotContain(refused.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.Equal(before.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)),
            after.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)));
        AssertNormalAdvance(before, after);
        Assert.Equal(after.ContentHash, authority.ReadRecordedStateForHost(after.Revision).State.ContentHash);
    }

    [Fact]
    public void Ordinary_movement_checks_real_occupant_load_before_entering_an_under_rated_deck()
    {
        var stairs = new StairNavigationWorldFixture(physicalConstruction: true);
        int deck = stairs.Genesis.Construction!.Definitions.Single().Members.Select((member, index) => (member, index))
            .Single(value => value.member.MinZ == 2000).index;
        ConstructionLoadPolicy policy = StructuralPolicyWorldFixture.PolicyFor(stairs.Genesis,
            index => index == deck ? 30_000_000UL : 100_000_000UL);
        WorldSnapshot genesis = WorldStructuralPolicyAdmission.CreateGenesis(stairs.Genesis, policy);
        using var files = new StorageFiles();
        WorldSnapshot world = stairs.FundAndBuild(files.Path, genesis);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        world = stairs.WalkTo(authority, world, stairs.Player, StairNavigationWorldFixture.StairApproach);
        for (int step = 0; step < 256; step++)
        {
            WorldStepBatch batch = stairs.NextStep(world, stairs.Player, StairNavigationWorldFixture.UpperDeck,
                out SubunitPosition nextFeet);
            WorldSnapshot next = authority.Commit(batch).Candidate;
            if (next.Space.GetActor(stairs.Player).Feet == world.Space.GetActor(stairs.Player).Feet)
            {
                AssertNormalAdvance(world, next);
                Assert.Equal(world.Space.GetActor(stairs.Player).Revision, next.Space.GetActor(stairs.Player).Revision);
                Assert.Equal(world.Inventory.ContentHash, next.Inventory.ContentHash);
                Assert.Equal(world.ConstructionPhysical!.ContentHash, next.ConstructionPhysical!.ContentHash);
                WorldSnapshot legacy = WithoutPolicy(world);
                WorldSnapshot accepted = new WorldStepProcessor().Decide(legacy, Reprofile(batch, 13)).Candidate;
                Assert.Equal(nextFeet, accepted.Space.GetActor(stairs.Player).Feet);
                StructuralLoadResolution failure = Finish(policy.Bind(accepted).Graph);
                Assert.Contains(failure.Failures, value => value.ElementId == ConstructionBearingModel.MemberIdentity(
                    world.Construction!.Projects.Single().Installed!.StructureId, (uint)deck));
                return;
            }
            Assert.Equal(nextFeet, next.Space.GetActor(stairs.Player).Feet);
            Assert.NotEqual(StairNavigationWorldFixture.UpperDeck, nextFeet);
            world = next;
        }
        Assert.Fail("The ordinary approach must encounter the deck's actual load limit within its unchanged route budget.");
    }

    [Fact]
    public void Giving_goods_checks_the_recipient_foundation_and_rolls_back_split_lot_issuance()
    {
        var stairs = new StairNavigationWorldFixture(physicalConstruction: true);
        ActorPresence player = stairs.Genesis.Space.GetActor(stairs.Player);
        LocalSpatialSnapshot space = stairs.Genesis.Space.WithActors(stairs.Genesis.Space.Actors.Select(actor =>
            actor.ActorId == player.ActorId ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope,
                StairNavigationWorldFixture.UnderDeck.Offset(256, 0, 0), actor.Revision, actor.HomePlaceId) : actor));
        WorldSnapshot basis = WithoutPolicy(stairs.Genesis, space);
        Assert.Equal(SpatialInteraction.Reachable, space.QueryInteraction(stairs.Player, stairs.Builder));
        ConstructionLoadPolicy generous = StructuralPolicyWorldFixture.PolicyFor(basis);
        ulong currentMass = checked((ulong)generous.Masses.ActorLoad(basis, stairs.Builder));
        var supportCell = space.GetActor(stairs.Builder).Feet.Cell.Offset(0, 0, -1);
        var anchors = generous.Foundations.Select(anchor => anchor.Cell == supportCell
            ? new StableTerrainBearing(anchor.ElementId, anchor.Cell, anchor.SourceId, anchor.Material, new(currentMass)) : anchor);
        var limited = new ConstructionLoadPolicy(generous.Scope, generous.Templates, anchors, generous.Masses);
        WorldSnapshot before = WorldStructuralPolicyAdmission.CreateGenesis(basis, limited);
        EntityId bag = before.GetResident(stairs.Player).InventoryCustodianId;
        var lot = before.Inventory.Custody.Lots.Single(value => value.CustodianId == bag
            && value.Definition == before.Quests!.Definitions.Single().Item);
        Assert.True(lot.Quantity > 1);
        var delivery = ConstructionUpgradeFixture.Delivery(before, stairs.Player);
        var command = new WorldActionCommand(stairs.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            stairs.Player, before.WorldId, before.BranchId, player.Revision,
            new GiveItemIntent(stairs.Builder, before.Space.GetActor(stairs.Builder).Revision, lot.LotId, lot.Revision,
                bag, before.Inventory.Custody.GetStorage(bag).Revision, 1));
        WorldStepBatch batch = stairs.Actions.Batch(before, [command], []);
        var processor = new WorldStepProcessor();
        WorldStepDecision control = processor.Decide(basis, Reprofile(batch, 13));
        Assert.Equal(WorldActionRejection.None, Assert.Single(control.Receipts).Rejection);
        Assert.Contains(control.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.True(limited.Masses.ActorLoad(control.Candidate, stairs.Builder) > currentMass);
        WorldSnapshot positive = WorldStructuralPolicyAdmission.CreateGenesis(basis, generous);
        Assert.Equal(WorldActionRejection.None, Assert.Single(processor.Decide(positive, batch).Receipts).Rejection);

        using var files = new StorageFiles();
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, before);
        WorldStepDecision refused = authority.Commit(batch);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
        Assert.Equal(before.Inventory.ContentHash, refused.Candidate.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, refused.Candidate.Production!.ContentHash);
        Assert.DoesNotContain(refused.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.Equal(currentMass, checked((ulong)limited.Masses.ActorLoad(refused.Candidate, stairs.Builder)));
        AssertNormalAdvance(before, refused.Candidate);
        Assert.Equal(refused.Candidate.ContentHash, authority.ReadRecordedStateForHost(1).State.ContentHash);
    }

    private static Completion PrepareCompletion(StairNavigationWorldFixture stairs, WorldSnapshot genesis, string path)
    {
        WorldSnapshot world = stairs.Actions.CreateSource(path, completeQuest: true, genesis: genesis);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(path);
        world = stairs.Actions.Act(authority, world, stairs.Player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
        var controller = new LocalNpcController();
        for (int step = 0; step < 128; step++)
        {
            var actions = controller.SelectActions(world, stairs.Actions.NextIdentity).ToArray();
            WorldStepBatch batch = stairs.Actions.Batch(world, actions, controller.SelectMovement(world));
            if (actions.Any(action => action.Intent is WorkConstructionIntent))
            {
                Assert.Single(actions);
                Assert.Equal(1UL, world.Construction!.Projects.Single().Definition.RequiredWork.Value);
                Assert.Null(world.Construction.Projects.Single().Installed);
                return new(world, batch);
            }
            WorldStepDecision decision = authority.Commit(batch);
            Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            world = decision.Candidate;
        }
        throw new InvalidOperationException("The funded NPC must reach completion within the unchanged work approach budget.");
    }

    private static WorldSnapshot WithoutPolicy(WorldSnapshot world, LocalSpatialSnapshot? space = null) =>
        new(world.Inventory, space ?? world.Space, world.Residents, world.AdmissionVersion, world.Calendar, world.Revision,
            world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests,
            world.Construction, excavation: world.Excavation, constructionPhysical: world.ConstructionPhysical);

    private static WorldStepBatch Reprofile(WorldStepBatch batch, ushort profile) =>
        new(batch.CauseId, batch.WorldId, batch.BranchId, batch.ExpectedRevision, batch.ExpectedAdvancingSteps,
            batch.Kind, batch.Movement, batch.Actions, profile);

    private static StructuralLoadResolution Finish(StructuralLoadGraph graph)
    {
        var result = StructuralLoadResolution.Begin(graph, []);
        while (!result.IsComplete) { result = result.Advance(); }
        return result;
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
        }
    }

    private static string? EvidenceDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_STRUCTURAL_POLICY_EVIDENCE");
        if (directory is null) { return null; }
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void RetainExpected(string directory, StructuralPolicyWorldFixture fixture,
        Completion preparation, WorldSnapshot completed, byte[] receipt)
    {
        WorldSnapshot before = preparation.Before;
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Durable complete authored load policy through real funded completion, stable occupancy and exact recovery; no collapse publication",
            ProfileVersion = 14, PolicyHash = completed.StructuralPolicy!.ContentHash.Value,
            WorldId = completed.WorldId.ToString(), BranchId = completed.BranchId.ToString(),
            PreRevision = before.Revision, PreHash = before.ContentHash.Value,
            PostRevision = completed.Revision, PostHash = completed.ContentHash.Value,
            Player = fixture.Stairs.Player.ToString(), Builder = fixture.Stairs.Builder.ToString(),
            CommandId = preparation.Batch.Actions.Single().CommandId.ToString(), ReceiptHex = Convert.ToHexString(receipt),
            StructureId = completed.Construction!.Projects.Single().Installed!.StructureId.ToString(),
            ExpectedMemberIds = completed.ConstructionPhysical!.Members.Select(value => value.ElementId.ToString()),
            MemberCount = completed.ConstructionPhysical.Members.Count,
            PhysicalHash = completed.ConstructionPhysical.ContentHash.Value,
            PhysicalRevision = completed.ConstructionPhysical.Revision,
            PhysicalBeforeRevision = before.ConstructionPhysical!.Revision,
            PhysicalAfterRevision = completed.ConstructionPhysical.Revision,
            PreConstructionHash = before.Construction!.ContentHash.Value, PostConstructionHash = completed.Construction.ContentHash.Value,
            PreProductionHash = before.Production!.ContentHash.Value, PostProductionHash = completed.Production!.ContentHash.Value,
            PreInventoryHash = before.Inventory.ContentHash.Value, PostInventoryHash = completed.Inventory.ContentHash.Value,
            PreCustodyHash = before.Inventory.Custody.ContentHash.Value, PostCustodyHash = completed.Inventory.Custody.ContentHash.Value,
            History = new[] { fixture.Genesis, before, completed }.Select(value => new { value.Revision, Hash = value.ContentHash.Value })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record Completion(WorldSnapshot Before, WorldStepBatch Batch);
}
