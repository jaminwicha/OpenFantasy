using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// Requires the retained ledger named by the source variable. Without it these cases are explicitly skipped;
/// a managed rehearsal exercises the authentication and is never native evidence.
/// </summary>
public sealed class CoordinatedNativeFixtureTests(CoordinatedNativeJourney journey) : IClassFixture<CoordinatedNativeJourney>
{
    [Fact]
    public void The_retained_ledger_is_prepared_by_readiness_and_accepted_movement_without_changing_its_source()
    {
        Assert.SkipUnless(journey.IsPrepared, "The retained source environment variable is required for this fixture.");
        Assert.Equal(journey.SourceSha256, journey.HashSource());
        Assert.Equal(File.ReadAllBytes(Path.Combine(journey.SourceDirectory, "final.ofc")), WorldSnapshotCodec.Encode(journey.Retained));
        Assert.Equal((ushort)27, journey.Retained.ProfileVersion);
        Assert.True(journey.RetainedArchives >= 3);
        Assert.True(journey.BeforeArchives >= journey.RetainedArchives);

        WorldSnapshot retained = journey.Retained, before = journey.Before;
        Assert.Equal(retained.WorldId, before.WorldId);
        Assert.Equal(retained.AdmissionVersion, before.AdmissionVersion);
        Assert.InRange(journey.Walk.Count, 1, CoordinatedNativeJourney.WalkingStepLimit);
        ulong steps = (ulong)(journey.ReadinessSteps + journey.Walk.Count);
        Assert.Equal(retained.Revision + steps, before.Revision);
        Assert.Equal(retained.AdvancingSteps + steps, before.AdvancingSteps);
        Assert.False(before.HasReadingHold);
        Assert.Null(before.PendingExcavation);
        Assert.Null(before.Care!.Pending);

        // Preparation restores, spends and re-permits nothing: every owner but position is the retained one.
        Assert.Equal(retained.Care!.ContentHash, before.Care.ContentHash);
        Assert.Equal(retained.Production!.ContentHash, before.Production!.ContentHash);
        Assert.Equal(retained.Inventory.ContentHash, before.Inventory.ContentHash);
        Assert.Equal(retained.Progression!.ContentHash, before.Progression!.ContentHash);
        Assert.Equivalent(retained.Progression.GetActor(journey.Player), before.Progression.GetActor(journey.Player), strict: true);

        // Only the player moved, one accepted cardinal sample at a time from the retained position.
        SubunitPosition feet = retained.Space.GetActor(journey.Player).Feet;
        foreach (WalkStep step in journey.Walk)
        {
            Assert.Equal(feet, step.From);
            Assert.Equal(feet.Offset(step.DeltaX, step.DeltaY, 0), step.To);
            Assert.InRange(step.Sequence, retained.Revision + 1, before.Revision);
            feet = step.To;
        }
        Assert.Equal(feet, before.Space.GetActor(journey.Player).Feet);
        Assert.Equal(retained.Space.Actors.Where(value => value.ActorId != journey.Player).Select(value => (value.ActorId, value.Feet, value.Revision)),
            before.Space.Actors.Where(value => value.ActorId != journey.Player).Select(value => (value.ActorId, value.Feet, value.Revision)));
        Assert.Equal(retained.Residents.Select(value => (value.ActorId, value.InventoryCustodianId, value.ControllerKind)),
            before.Residents.Select(value => (value.ActorId, value.InventoryCustodianId, value.ControllerKind)));
    }

    [Fact]
    public void The_ready_permitted_helper_fails_for_reach_alone_and_its_policy_selects_no_aid()
    {
        Assert.SkipUnless(journey.IsPrepared, "The retained source environment variable is required for this fixture.");
        WorldSnapshot before = journey.Before;
        CarePermission permission = Assert.Single(before.Care!.Ordinary!.Permissions);
        Assert.True(permission.IsActive);
        Assert.Equal(journey.Player, permission.PatientActorId);
        Assert.Equal(journey.Helper, permission.HelperActorId);
        Assert.True(WorldActorAvailability.CanAct(before, journey.Player));
        Assert.True(WorldActorAvailability.CanAct(before, journey.Helper));

        // The same exact choice was admissible while in reach; only accepted movement separates the boundaries.
        Assert.Equal(SpatialInteraction.Reachable,
            journey.InReach.Space.QueryInteraction(journey.Helper, journey.Player, journey.RemedyRangeSubunits));
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(journey.InReach, journey.Helper,
            CoordinatedNativeJourney.CaptureHelperAid(journey.InReach, journey.Helper, journey.HelperStockLot)));
        UseRestorativeItemIntent aid = CoordinatedNativeJourney.CaptureHelperAid(before, journey.Helper, journey.HelperStockLot);
        Assert.NotEqual(SpatialInteraction.Reachable,
            before.Space.QueryInteraction(journey.Helper, journey.Player, journey.RemedyRangeSubunits));
        Assert.Equal(WorldActionRejection.OutOfReach, WorldRestorativeActions.Evaluate(before, journey.Helper, aid));
        Assert.Equal(RestorativeAvailabilityReason.OutOfReach, WorldRestorativeActions.Assess(before, journey.Helper, aid).Reason);
        Assert.Equal(RestorativeAvailabilityReason.OutOfReach, WorldRestorativeActions.PrepareAvailability(before,
            journey.Helper, journey.Player, permission.UseDefinitionId, permission.PermissionId).Reason);
        Assert.Empty(new LocalRestorativeController().SelectActions(before, _ => false, new HashSet<EntityId>()));
        Assert.Empty(new LocalNpcController().SelectMovement(before));

        // The patient owns no dose; the previewed withdrawal changes the permission and nothing else it holds.
        var selfUse = before.Care.Ordinary.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse);
        Assert.Equal(RestorativeAvailabilityReason.NoOwnedDose, WorldRestorativeActions.PrepareAvailability(before,
            journey.Player, journey.Player, selfUse.DefinitionId).Reason);
        Assert.False(Assert.Single(journey.ExpectedImmediate.Care!.Ordinary!.Permissions).IsActive);
        Assert.Equal(before.Progression!.ContentHash, journey.ExpectedImmediate.Progression!.ContentHash);
        Assert.Equal(before.Inventory.ContentHash, journey.ExpectedImmediate.Inventory.ContentHash);
        Assert.Equal(before.Care.TotalOrdinaryUses, journey.ExpectedImmediate.Care.TotalOrdinaryUses);
    }

    [Fact]
    public void The_exported_backup_is_closed_and_begins_from_the_exact_stored_genesis()
    {
        Assert.SkipUnless(journey.IsPrepared, "The retained source environment variable is required for this fixture.");
        foreach (string suffix in new[] { "-wal", "-shm", "-journal" })
        {
            Assert.False(File.Exists(journey.BeforeDatabasePath + suffix));
        }
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(journey.GenesisBytes);
        Assert.Equal((ushort)27, genesis.ProfileVersion);
        Assert.Equal(0UL, genesis.Revision);
        Assert.Equal(0UL, genesis.AdvancingSteps);
        Assert.Equal(journey.Before.WorldId, genesis.WorldId);
        Assert.Equal(journey.Before.AdmissionVersion, genesis.AdmissionVersion);
        Assert.Equal(journey.GenesisBytes, File.ReadAllBytes(journey.GenesisPath));
        using var files = new StorageFiles();
        File.Copy(journey.BeforeDatabasePath, files.Path);
        using WorldDatabase stored = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(journey.GenesisBytes, stored.ReadGenesis());
        Assert.Equal(journey.Before.Revision, stored.ReadHead().Sequence);
        Assert.Equal(WorldSnapshotCodec.Encode(journey.Before), stored.ReadHead().State);
        Assert.Equal(journey.BeforeLedgerSha256, CoordinatedNativeJourney.LedgerDigest(stored, journey.Before.Revision));
    }

    [Fact]
    public async Task A_managed_rehearsal_through_the_public_offer_authenticates_like_native_output()
    {
        Assert.SkipUnless(journey.IsPrepared, "The retained source environment variable is required for this fixture.");
        using var files = new StorageFiles();
        File.Copy(journey.BeforeDatabasePath, files.Path);
        int hp = journey.Before.Progression!.GetActor(journey.Player).CurrentHp;
        await using (FarmClientSession client = await FarmClientSession.OpenCoordinatedFixtureAsync(files.Path, journey.GenesisPath))
        {
            LocalWorldView opened = client.World.ReadView();
            Assert.Equal(journey.Before.WorldId, opened.WorldId);
            Assert.Equal(journey.Before.Calendar, opened.Calendar);
            Assert.Equal(journey.Player, opened.PlayerActorId);
            Assert.Equal(journey.Helper, client.WrenActorId);
            Assert.Null(opened.Care);
            Assert.False(opened.IsInEncounter);
            Assert.Equal(hp, opened.Progression!.Hp);
            LocalCarePermissionView permission = Assert.Single(opened.Restorative!.PermissionOffers);
            Assert.True(permission.IsActive);
            Assert.Equal(journey.Helper, permission.HelperActorId);

            // Personal use is blocked by the empty pack itself, whether or not the session is paused.
            VerifySelfUseBlocked(client, opened);
            client.World.SetReady(true);
            LocalWorldView ready = client.World.ReadView();
            VerifySelfUseBlocked(client, ready);

            FarmActionOffer offer = client.CreateOffers(ready)[FarmSemanticAction.RevokeCarePermission];
            Assert.True(offer.CanExecute, offer.BlockingReason);
            Assert.Equal(journey.ExpectedRevoke, Assert.IsType<RevokeCarePermissionIntent>(offer.Intent));
            WorldActionReceipt receipt = await client.SubmitOffer(offer)
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            client.World.SetReady(false);

            LocalWorldView withdrawn = client.World.ReadView();
            Assert.False(Assert.Single(withdrawn.Restorative!.PermissionOffers).IsActive);
            Assert.Equal(hp, withdrawn.Progression!.Hp);
            Assert.Equal(opened.Progression.Mp, withdrawn.Progression.Mp);
        }
        CoordinatedNativeJourney.Authenticate(files.Path, journey.FixtureDirectory);
    }

    private static void VerifySelfUseBlocked(FarmClientSession client, LocalWorldView view)
    {
        LocalRestorativeUseView use = Assert.Single(view.Restorative!.SelfUseOffers);
        Assert.False(use.CanUse);
        Assert.Null(use.Intent);
        Assert.Equal(RestorativeAvailabilityReason.NoOwnedDose, use.Reason);
        FarmActionOffer offer = client.CreateOffers(view)[FarmSemanticAction.UseRestorative];
        Assert.False(offer.CanExecute);
        Assert.Null(offer.Intent);
    }
}
