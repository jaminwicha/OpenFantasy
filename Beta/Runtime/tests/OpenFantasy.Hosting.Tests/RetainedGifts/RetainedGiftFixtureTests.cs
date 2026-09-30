using System.Text.Json;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.ProposalMigration;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.RetainedGifts.RetainedGiftPreparation;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.RetainedGifts;

/// <summary>
/// Prepares and exports the native retained gift fixture from the pinned original through the accepted
/// transition, then proves the same journey in a managed scheduled-session continuation.
/// </summary>
[Collection(RetainedTransitionCollection.Name)]
public sealed class RetainedGiftFixtureTests(RetainedTransitionFixture transition)
{
    [Fact]
    public async Task Prepared_original_world_carries_a_helper_gift_across_a_real_cut_before_acceptance_and_own_use()
    {
        using StorageFiles files = transition.CopyUpgraded();
        using var fixtureCopy = new StorageFiles();
        var authority = SqliteWorldAuthority.OpenExisting(files.Path, transition.GenesisBytes);
        WorldSnapshot start = authority.ReadStateForHost();
        string genesisPath = Path.Combine(Path.GetDirectoryName(fixtureCopy.Path)!, "genesis.ofc");
        RetainedGiftPreparation preparation;
        WorldSnapshot prepared;
        byte[] exported;
        string? exportRoot;
        try
        {
            Assert.Equal(transition.UpgradedHead.ContentHash, start.ContentHash);
            preparation = Run(authority);
            prepared = preparation.Prepared;
            VerifyPrepared(start, prepared, preparation);
            authority.BackupForHost(fixtureCopy.Path);
            File.WriteAllBytes(genesisPath, transition.GenesisBytes);
            exported = File.ReadAllBytes(fixtureCopy.Path);
            exportRoot = Export(fixtureCopy.Path, prepared, preparation);
        }
        catch
        {
            authority.Dispose();
            throw;
        }

        // The same journey in the scheduled session, with the native binding's own gift policy.
        EntityId player = preparation.Player, helper = preparation.Helper;
        ActorControlBinding control = prepared.Inventory.Controls.Single(value => value.ActorId == player);
        var time = new ManualSessionTimeProvider();
        await using (var session = new LocalWorldSession(authority, player, new(control.PrincipalId, control.SessionId), time,
            [FarmClientSession.GiftPolicy(prepared, helper, player)]))
        {
            await Tick(time);
            Assert.Equal(prepared.ContentHash, authority.ReadStateForHost().ContentHash);
            session.SetReady(true);
            await Tick(time);
            WorldSnapshot offered = authority.ReadStateForHost();
            WorldMaterialProposal gift = Assert.Single(offered.Coordination!.Proposals!.Open);
            Assert.Equal((helper, player, preparation.PotionLot, 1UL), (gift.Terms.SenderActorId, gift.Terms.RecipientActorId,
                gift.Terms.LotId, gift.Terms.Quantity));
            Assert.Equal(LocalMaterialProposalPreparation.OfferLifetimeSteps, gift.ExpiresAtAdvancingStep - gift.CreatedAdvancingStep);
            Assert.Equal(prepared.Coordination!.Anchor!.ContentHash, offered.Coordination.Anchor!.ContentHash);
            Assert.Equal(prepared.Inventory.ContentHash, offered.Inventory.ContentHash);

            LocalMaterialProposalLot lot = PlayerLot(session.ReadView(), preparation.Remedy);
            ProposeMaterialIntent own = session.ReadView().MaterialProposals!.Preparation!.Prepare(lot.Kind, lot.LotId, helper, 1)
                ?? throw new InvalidOperationException("The player's own lot must be offerable to the helper.");
            WorldActionCommand propose = Command(offered, player, own, NextId("continuation-offer", 0));
            WorldActionReceipt proposed = await ScheduledMaterialProposalSessionTests.Submit(session, time, propose);
            Assert.Equal(WorldActionRejection.None, proposed.Rejection);
            WorldSnapshot cut = authority.ReadStateForHost();
            WorldMaterialCareArchiveAnchor anchor = cut.Coordination!.Anchor!;
            Assert.Equal(prepared.Coordination.Anchor.Reference.Ordinal + 1, anchor.Reference.Ordinal);
            Assert.Equal(proposed.CommitSequence - 1, anchor.ClosingWorldSequence);
            Assert.Null(anchor.TransitionHash);
            Assert.Equal(gift.ContentHash, Assert.Single(anchor.Proposals!.Carried).ContentHash);
            Assert.Equal(gift.ContentHash, cut.Coordination.Proposals!.FindOpen(gift.ProposalId)!.ContentHash);

            await Tick(time);
            EntityId playerOffer = WorldMaterialProposal.DeriveIdentity(propose.CommandId);
            WorldMaterialProposalOutcome declined = authority.ReadStateForHost().Coordination!.Proposals!.FindOutcome(playerOffer)!;
            Assert.Equal(WorldMaterialProposalOutcomeKind.Declined, declined.Kind);

            WorldSnapshot beforeAcceptance = authority.ReadStateForHost();
            MaterialProposalView carried = session.ReadView().MaterialProposals!.Offers.Single(value => value.ProposalId == gift.ProposalId);
            WorldActionCommand accept = Command(beforeAcceptance, player, carried.PrepareAcceptance(), NextId("continuation-accept", 0));
            Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, time, accept)).Rejection);
            WorldSnapshot received = authority.ReadStateForHost();
            int hp = prepared.Progression!.GetActor(player).CurrentHp;
            Assert.Equal(hp, received.Progression!.GetActor(player).CurrentHp);
            Assert.Equal(1UL, Potions(received, preparation.Remedy, Bag(received, player)));
            Assert.Equal(79UL, Potions(received, preparation.Remedy, Bag(received, helper)));

            EntityId self = received.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse).DefinitionId;
            UseRestorativeItemIntent use = WorldRestorativeActions.PrepareAvailability(received, player, player, self).Intent
                ?? throw new InvalidOperationException("The received dose must be usable on the actual injury.");
            WorldActionCommand consume = Command(received, player, use, NextId("continuation-use", 0));
            Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, time, consume)).Rejection);
            WorldSnapshot healed = authority.ReadStateForHost();
            Assert.Equal(RetainedTransitionFixture.SourceOrdinaryUses + 1, healed.Care!.TotalOrdinaryUses);
            Assert.Equal(ExpectedHp(prepared, player, preparation.Remedy), healed.Progression!.GetActor(player).CurrentHp);
            Assert.Equal(prepared.Progression.GetActor(player).CurrentMp, healed.Progression.GetActor(player).CurrentMp);
            Assert.Equal(0UL, Potions(healed, preparation.Remedy, Bag(healed, player)));
            Assert.Equal(79UL, Potions(healed, preparation.Remedy));
            var definition = received.Care.Ordinary.GetDefinition(self);
            Assert.Equal(received.Calendar.AddMilliseconds(checked((long)definition.ExplorationMilliseconds)), healed.Calendar);
            await session.StopAsync();
        }

        // The explicit native opener selects the exact exported fixture, paused, with the same policy.
        using var opened = new StorageFiles();
        File.Copy(fixtureCopy.Path, opened.Path);
        await using (FarmClientSession client = await FarmClientSession.OpenRetainedGiftsAsync(opened.Path, genesisPath,
            FarmClientSession.RetainedGiftTargetProfileVersion))
        {
            LocalWorldView view = client.World.ReadView();
            Assert.Equal(LocalWorldRunState.Paused, client.World.ReadRunState());
            Assert.Equal(helper, client.WrenActorId);
            Assert.Equal(prepared.Progression!.GetActor(player).CurrentHp, view.Progression!.Hp);
            Assert.DoesNotContain(view.MaterialProposals!.Offers, value => value.Status == MaterialProposalStatus.Open);
            Assert.NotNull(view.MaterialProposals.Preparation);
            Assert.All(view.Restorative!.PermissionOffers, value => Assert.False(value.IsActive));
        }
        Assert.Equal(exported, File.ReadAllBytes(opened.Path));
        if (exportRoot is not null) Assert.Equal(exported, File.ReadAllBytes(Path.Combine(exportRoot, "save.sqlite")));
        Assert.Equal(RetainedTransitionFixture.SourceSha256, RetainedTransitionFixture.Sha256(transition.OriginalSource));
    }

    private static void VerifyPrepared(WorldSnapshot start, WorldSnapshot prepared, RetainedGiftPreparation preparation)
    {
        EntityId player = preparation.Player, helper = preparation.Helper;
        Assert.Equal((ushort)30, prepared.ProfileVersion);
        Assert.Equal(RetainedTransitionFixture.SourceOrdinaryUses, prepared.Care!.TotalOrdinaryUses);
        Assert.Equal(0UL, Potions(prepared, preparation.Remedy, Bag(prepared, player)));
        Assert.Equal(80UL, Potions(prepared, preparation.Remedy, Bag(prepared, helper)));
        Assert.Equal(80UL, Potions(prepared, preparation.Remedy));
        // Custody, controls and resources are the lived ones; preparation moved no stock and healed nobody.
        Assert.Equal(start.Inventory.Controls, prepared.Inventory.Controls);
        Assert.Equal(start.Inventory.ContentHash, prepared.Inventory.ContentHash);
        Assert.Equal(start.Production!.Lots.Select(value => (value.LotId, value.CustodianId, value.Quantity, value.Revision)),
            prepared.Production!.Lots.Select(value => (value.LotId, value.CustodianId, value.Quantity, value.Revision)));
        Assert.Equal(start.Progression!.ContentHash, prepared.Progression!.ContentHash);
        var patient = prepared.Progression.GetActor(player);
        Assert.True(LocalRestorativeController.FitsFullDose(patient.CurrentHp, patient.Stats.MaxHp, preparation.Remedy.RestoredHp),
            "The actual lived injury must absorb the helper's whole dose.");
        Assert.False(prepared.Care.Ordinary!.Permissions.Single(value => value.PatientActorId == player).IsActive);
        Assert.Equal(PreparedOutcomes, prepared.Coordination!.Proposals!.Outcomes.Count);
        Assert.All(prepared.Coordination.Proposals.Outcomes, value => Assert.Equal(WorldMaterialProposalOutcomeKind.Withdrawn, value.Kind));
        Assert.Empty(prepared.Coordination.Proposals.Open);
        Assert.Equal(start.Coordination!.Anchor!.ContentHash, prepared.Coordination.Anchor!.ContentHash);
        Assert.Equal((ulong)IdleAmblePhase, AmblePhase(prepared));
        Assert.True(WorldActorAvailability.CanAct(prepared, player) && WorldActorAvailability.CanAct(prepared, helper));
        Assert.Null(prepared.Care.Pending);
        Assert.False(prepared.HasReadingHold);
        Assert.Equal(prepared.Revision, preparation.Actions[^1].Sequence);
        Assert.Equal(Enumerable.Range(1, preparation.Actions.Count).Select(value => start.Revision + (ulong)value),
            preparation.Actions.Select(value => value.Sequence));
    }

    private static LocalMaterialProposalLot PlayerLot(LocalWorldView view, RestorativeRemedyDefinition remedy) =>
        view.MaterialProposals!.Preparation!.Lots.Where(value => value.Definition != remedy.Item)
            .OrderBy(value => value.Kind).ThenBy(value => value.LotId).First();

    internal static int ExpectedHp(WorldSnapshot state, EntityId player, RestorativeRemedyDefinition remedy)
    {
        var patient = state.Progression!.GetActor(player);
        return (int)Math.Min(patient.Stats.MaxHp, (long)patient.CurrentHp + remedy.RestoredHp);
    }

    /// <summary>Writes new immutable native inputs only when explicitly requested; never overwrites evidence.</summary>
    private string? Export(string save, WorldSnapshot prepared, RetainedGiftPreparation preparation)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_RETAINED_GIFT_FIXTURES") is not { Length: > 0 } requested) return null;
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(),
            "Retained gift fixture export requires a fresh or empty directory; existing evidence is never overwritten.");
        Directory.CreateDirectory(root);
        EntityId player = preparation.Player, helper = preparation.Helper;
        var lot = PlayerLot(new LocalWorldView(prepared, player), preparation.Remedy);
        var patient = prepared.Progression!.GetActor(player);
        var self = prepared.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse);
        File.Copy(save, Path.Combine(root, "save.sqlite"));
        File.WriteAllBytes(Path.Combine(root, "genesis.ofc"), transition.GenesisBytes);
        File.WriteAllBytes(Path.Combine(root, "transition.ofc"), WorldProposalTransitionCodecV1.Encode(transition.Request));
        File.WriteAllBytes(Path.Combine(root, "before.ofc"), WorldSnapshotCodec.Encode(prepared));
        File.WriteAllText(Path.Combine(root, "metadata.json"), JsonSerializer.Serialize(new
        {
            kind = "native-retained-gift-fixture-v1",
            setup = "Pinned original profile27 world, accepted explicit transition, then only the labeled managed preparatory commits below. Native input begins before any gift of this journey exists.",
            sourceSha256 = RetainedTransitionFixture.SourceSha256, sourceHead = RetainedTransitionFixture.SourceHeadHash,
            sourceRevision = RetainedTransitionFixture.SourceRevision, transitionRevision = transition.UpgradedHead.Revision,
            transitionCause = transition.Request.CauseId.ToString(),
            worldId = prepared.WorldId.ToString(), branchId = prepared.BranchId.ToString(),
            beforeRevision = prepared.Revision, beforeAdvancingSteps = prepared.AdvancingSteps, beforeHash = prepared.ContentHash.Value,
            beforeArchiveOrdinal = prepared.Coordination!.Anchor!.Reference.Ordinal, preparedOutcomes = PreparedOutcomes,
            amblePhase = AmblePhase(prepared),
            playerId = player.ToString(), helperId = helper.ToString(), potionLotId = preparation.PotionLot.ToString(),
            playerLotId = lot.LotId.ToString(), playerLotKind = lot.Kind.ToString(),
            permissionWasActive = preparation.PermissionWasActive,
            selfUseDefinitionId = self.DefinitionId.ToString(), explorationMilliseconds = self.ExplorationMilliseconds,
            beforeHp = patient.CurrentHp, beforeMp = patient.CurrentMp, maximumHp = patient.Stats.MaxHp,
            restoredHp = preparation.Remedy.RestoredHp, expectedHp = ExpectedHp(prepared, player, preparation.Remedy),
            ordinaryUses = prepared.Care.TotalOrdinaryUses, helperPotions = 80, playerPotions = 0,
            offerLifetimeSteps = LocalMaterialProposalPreparation.OfferLifetimeSteps,
            preparation = preparation.Actions.Select(value => new
            {
                label = value.Label, sequence = value.Sequence, actorId = value.ActorId.ToString(), commandId = value.CommandId?.ToString()
            }).ToArray(),
            limits = "Preparation is managed and labeled, not native. No stock, injury, control, budget, genesis or archive cut is fabricated."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
        {
            kind = "native-retained-gift-fixtures-v1", save = Path.Combine(root, "save.sqlite"),
            genesis = Path.Combine(root, "genesis.ofc"), transition = Path.Combine(root, "transition.ofc"),
            before = Path.Combine(root, "before.ofc"), metadata = Path.Combine(root, "metadata.json")
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return root;
    }
}
