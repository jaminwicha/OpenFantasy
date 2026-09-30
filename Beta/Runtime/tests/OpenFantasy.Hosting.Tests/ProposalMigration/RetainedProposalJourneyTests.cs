using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.ProposalMigration;

/// <summary>
/// The actual transitioned world: the helper offers one of its own existing potions, the offer rides a
/// real automatic cut, the player accepts it and later uses it. Every command is an authenticated
/// world command through the independently delivered proposal service or an ordinary advancing batch.
/// </summary>
[Collection(RetainedTransitionCollection.Name)]
public sealed class RetainedProposalJourneyTests(RetainedTransitionFixture fixture)
{
    private const ulong OfferLifetime = 4096;
    private const int MaximumPressureSteps = 400;

    [Fact]
    public void Helper_potion_offer_crosses_a_real_cut_is_accepted_and_later_used_by_the_player()
    {
        using StorageFiles files = fixture.CopyUpgraded();
        using var driver = new Driver(files.Path, fixture.GenesisBytes);
        WorldSnapshot start = driver.State;
        EntityId player = start.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        // The reciprocal quests name no single companion; the player's own permission names the helper.
        EntityId helper = start.Care!.Ordinary!.Permissions.Single(value => value.PatientActorId == player).HelperActorId;
        RestorativeRemedyDefinition remedy = start.Care.Remedies.Single();
        EntityId helperBag = start.GetResident(helper).InventoryCustodianId;
        EntityId playerBag = start.GetResident(player).InventoryCustodianId;
        CountedSourceLot stock = start.Inventory.Custody.Lots.Single(value => value.CustodianId == helperBag
            && value.Definition == remedy.Item);
        Assert.Equal(80UL, stock.Quantity);
        ulong totalPotions = Potions(start, remedy);
        ulong playerPotions = Potions(start, remedy, playerBag);
        int hp = start.Progression!.GetActor(player).CurrentHp;
        var controls = start.Inventory.Controls.ToArray();

        // Actual unilateral delivery still fails at the recipient's OwnerOnly policy, even though
        // the helper controls its own real stock and has an active permission to administer care.
        Assert.Equal(CustodyDepositRule.OwnerOnly,
            start.Inventory.Policies.Single(policy => policy.CustodianId == playerBag).DepositRule);
        WorldActionCommand direct = driver.Command(helper, new GiveItemIntent(player,
            start.Space.GetActor(player).Revision, stock.LotId, stock.Revision, helperBag,
            start.Inventory.Custody.GetStorage(helperBag).Revision, 1));
        WorldActionReceipt refused = Assert.Single(driver.Step(direct).Receipts);
        Assert.Equal(WorldActionRejection.NotAuthorized, refused.Rejection);
        Assert.Equal(start.Inventory.ContentHash, driver.State.Inventory.ContentHash);
        Assert.Equal(80UL, driver.State.Inventory.Custody.GetLot(stock.LotId).Quantity);

        // The helper's own offer of one existing potion reserves nothing and heals nobody.
        WorldSnapshot before = driver.State;
        CountedSourceLot current = before.Inventory.Custody.GetLot(stock.LotId);
        var terms = new ConsentedMaterialTerms(before.WorldId, before.BranchId, helper, player,
            ConsentedMaterialLotKind.CountedSource, current.LotId, current.Definition, helperBag, playerBag, 1, current.Revision);
        WorldActionCommand create = driver.Command(helper, new ProposeMaterialIntent(terms, before.AdvancingSteps + 1 + OfferLifetime));
        WorldActionReceipt created = driver.Service.Submit(create, driver.Delivery(helper));
        Assert.Equal(WorldActionRejection.None, created.Rejection);
        EntityId offer = WorldMaterialProposal.DeriveIdentity(create.CommandId);
        WorldMaterialProposal carried = driver.State.Coordination!.Proposals!.FindOpen(offer)!;
        Assert.NotNull(carried);
        ulong afterCreation = driver.State.Revision;
        Assert.Equal(WorldReceiptCodecV1.Encode(created),
            WorldReceiptCodecV1.Encode(driver.Service.Submit(create, driver.Delivery(helper))));
        Assert.Equal(afterCreation, driver.State.Revision);
        Assert.Equal(80UL, driver.State.Inventory.Custody.GetLot(stock.LotId).Quantity);
        Assert.Equal(hp, driver.State.Progression!.GetActor(player).CurrentHp);

        // An unauthorized or stale acceptance closes nothing and moves nothing.
        MaterialProposalView view = driver.Service.Find(offer, player, driver.Delivery(player))!;
        WorldActionCommand foreign = driver.Command(player, view.PrepareAcceptance());
        ContentHash beforeForeign = driver.State.ContentHash;
        Assert.Throws<AuthorityDispatchException>(() => driver.Service.Submit(foreign, driver.Delivery(helper)));
        Assert.Equal(beforeForeign, driver.State.ContentHash);
        Assert.Null(driver.Authority.FindReceipt(foreign, driver.Delivery(player)));
        WorldActionCommand fresh = driver.Command(player, view.PrepareAcceptance());
        var stale = new WorldActionCommand(fresh.CommandId, fresh.PrincipalId, fresh.OriginSessionId, player, fresh.WorldId,
            fresh.BranchId, fresh.ExpectedActorRevision + 7, fresh.Intent);
        Assert.Equal(WorldActionRejection.RevisionConflict, driver.Service.Submit(stale, driver.Delivery(player)).Rejection);
        Assert.NotNull(driver.State.Coordination!.Proposals!.FindOpen(offer));
        Assert.Equal(playerPotions, Potions(driver.State, remedy, playerBag));

        // Earn spatial separation through actual movement; a fresh, otherwise valid acceptance
        // must reject for reach, preserving the open offer, exact stock and existing injury.
        var feet = driver.State.Space.GetActor(player).Feet;
        var walk = driver.WalkOutOfReach(player, helper);
        MaterialProposalView distant = driver.Service.Find(offer, player, driver.Delivery(player))!;
        WorldActionCommand unreachable = driver.Command(player, distant.PrepareAcceptance());
        Assert.Equal(WorldActionRejection.OutOfReach,
            driver.Service.Submit(unreachable, driver.Delivery(player)).Rejection);
        Assert.Equal(carried.ContentHash, driver.State.Coordination!.Proposals!.FindOpen(offer)!.ContentHash);
        Assert.Equal(80UL, driver.State.Inventory.Custody.GetLot(stock.LotId).Quantity);
        Assert.Equal(playerPotions, Potions(driver.State, remedy, playerBag));
        Assert.Equal(hp, driver.State.Progression!.GetActor(player).CurrentHp);
        foreach (var move in walk.AsEnumerable().Reverse()) driver.Move(player, -move.X, -move.Y);
        Assert.Equal(feet, driver.State.Space.GetActor(player).Feet);

        // Real pressure: the helper's own later offers and withdrawals fill the outcome window until the
        // owner itself closes a segment. Nothing seals, relabels or shortens the cut.
        List<EntityId> withdrawn = [];
        EntityId? pending = null;
        for (int step = 0; driver.State.Coordination!.Anchor!.Reference.Ordinal == 3; step++)
        {
            Assert.True(step < MaximumPressureSteps, "The real outcome window must close a segment.");
            WorldSnapshot state = driver.State;
            if (pending is { } open && state.Coordination!.Proposals!.FindOpen(open) is not null)
            {
                WorldActionCommand withdraw = driver.Command(helper, new WithdrawMaterialProposalIntent(open));
                if (driver.Service.Submit(withdraw, driver.Delivery(helper)).Rejection == WorldActionRejection.None)
                {
                    withdrawn.Add(open);
                    pending = null;
                }
                continue;
            }
            CountedSourceLot lot = state.Inventory.Custody.GetLot(stock.LotId);
            var again = new ConsentedMaterialTerms(state.WorldId, state.BranchId, helper, player,
                ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, helperBag, playerBag, 1, lot.Revision);
            WorldActionCommand propose = driver.Command(helper, new ProposeMaterialIntent(again, state.AdvancingSteps + 1 + OfferLifetime));
            if (driver.Service.Submit(propose, driver.Delivery(helper)).Rejection == WorldActionRejection.None)
            {
                pending = WorldMaterialProposal.DeriveIdentity(propose.CommandId);
            }
        }
        WorldSnapshot cut = driver.State;
        WorldMaterialCareArchiveAnchor anchor = cut.Coordination!.Anchor!;
        Assert.Equal(4UL, anchor.Reference.Ordinal);
        Assert.Null(anchor.TransitionHash);
        Assert.Contains(anchor.Proposals!.Carried, value => value.ProposalId == offer && value.ContentHash == carried.ContentHash);
        Assert.Equal(carried.ContentHash, cut.Coordination.Proposals!.FindOpen(offer)!.ContentHash);
        Assert.NotEmpty(withdrawn);
        Assert.Equal(hp, cut.Progression!.GetActor(player).CurrentHp);
        Assert.Equal(RetainedTransitionFixture.SourceOrdinaryUses, cut.Care!.TotalOrdinaryUses);

        // Settlement after the cut: exact conservation, no healing, and an exact retry.
        MaterialProposalView settled = driver.Service.Find(offer, player, driver.Delivery(player))!;
        WorldActionCommand accept = driver.Command(player, settled.PrepareAcceptance());
        ulong stepsBeforeAcceptance = driver.State.AdvancingSteps;
        WorldActionReceipt accepted = driver.Service.Submit(accept, driver.Delivery(player));
        Assert.Equal(WorldActionRejection.None, accepted.Rejection);
        WorldSnapshot received = driver.State;
        Assert.Equal(stepsBeforeAcceptance + 1, received.AdvancingSteps);
        Assert.Null(received.Coordination!.Proposals!.FindOpen(offer));
        Assert.Equal(79UL, received.Inventory.Custody.GetLot(stock.LotId).Quantity);
        Assert.Equal(playerPotions + 1, Potions(received, remedy, playerBag));
        Assert.Equal(totalPotions, Potions(received, remedy));
        Assert.Equal(hp, received.Progression!.GetActor(player).CurrentHp);
        Assert.Equal(controls, received.Inventory.Controls);
        ulong afterAcceptance = received.Revision;
        Assert.Equal(WorldReceiptCodecV1.Encode(accepted),
            WorldReceiptCodecV1.Encode(driver.Service.Submit(accept, driver.Delivery(player))));
        Assert.Equal(afterAcceptance, driver.State.Revision);

        // A later separate command spends the accepted dose on the player's own actual injury.
        EntityId self = received.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse).DefinitionId;
        RestorativeAvailability availability = WorldRestorativeActions.PrepareAvailability(received, player, player, self);
        UseRestorativeItemIntent use = availability.Intent
            ?? throw new InvalidOperationException("The accepted dose must be usable on the actual injury: " + availability.Reason);
        WorldActionCommand consume = driver.Command(player, use);
        WorldStepDecision used = driver.Step(consume);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(used.Receipts).Status);
        WorldSnapshot healed = used.Candidate;
        Assert.Equal(RetainedTransitionFixture.SourceOrdinaryUses + 1, healed.Care!.TotalOrdinaryUses);
        Assert.Equal(playerPotions, Potions(healed, remedy, playerBag));
        Assert.Equal(totalPotions - 1, Potions(healed, remedy));
        Assert.Equal(Math.Min((long)received.Progression!.GetActor(player).Stats.MaxHp,
            (long)hp + remedy.RestoredHp), healed.Progression!.GetActor(player).CurrentHp);
        Assert.Equal(received.Progression.GetActor(player).CurrentMp, healed.Progression.GetActor(player).CurrentMp);
        var recordedUse = Assert.Single(healed.Care.Ordinary!.Uses, value => value.CommandId == consume.CommandId);
        Assert.Equal((player, player, consume.CommandId),
            (recordedUse.ActorId, recordedUse.RecipientActorId, recordedUse.CommandId));
        Assert.Equal(recordedUse.UseId, Assert.Single(healed.ResourceHistory!.Evidence
            .OfType<WorldRestorativeUseResourceEvidence>(), value => value.CommandId == consume.CommandId).UseId);
        var selfDefinition = received.Care.Ordinary.Definitions.Single(value => value.DefinitionId == self);
        Assert.Equal(received.Calendar.AddMilliseconds(checked((long)selfDefinition.ExplorationMilliseconds)), healed.Calendar);
        Assert.Equal(selfDefinition.ExplorationMilliseconds, recordedUse.ExplorationMilliseconds);
        WorldActionReceipt useReceipt = Assert.Single(used.Receipts);
        Assert.Equal(WorldReceiptCodecV1.Encode(useReceipt),
            WorldReceiptCodecV1.Encode(driver.Authority.FindReceipt(consume, driver.Delivery(player))!));
        Assert.Equal(received.AdvancingSteps + 1, healed.AdvancingSteps);

        // Reopening authenticates genesis, all three lived archives, the transition and the later cut.
        ContentHash head = healed.ContentHash;
        driver.Reopen();
        Assert.Equal(head, driver.State.ContentHash);
        RecordedWorldState lived = driver.Authority.ReadRecordedStateForHost(RetainedTransitionFixture.SourceRevision);
        Assert.Equal((27, RetainedTransitionFixture.SourceHeadHash), ((int)lived.State.ProfileVersion, lived.State.ContentHash.Value));
        RecordedWorldState transitioned = driver.Authority.ReadRecordedStateForHost(RetainedTransitionFixture.SourceRevision + 1);
        Assert.Equal(fixture.UpgradedHead.ContentHash, transitioned.State.ContentHash);
        Assert.Equal(received.ContentHash,
            driver.Authority.ReadRecordedStateForHost(received.Revision).State.ContentHash);
        Assert.Equal(healed.ContentHash,
            driver.Authority.ReadRecordedStateForHost(healed.Revision).State.ContentHash);
        Assert.Equal(WorldReceiptCodecV1.Encode(useReceipt),
            WorldReceiptCodecV1.Encode(driver.Authority.FindReceipt(consume, driver.Delivery(player))!));
        Assert.Equal(head, driver.State.ContentHash);
        // A terminal outcome closed inside the post-transition segment is retrieved from its archive.
        MaterialProposalView archived = driver.Service.FindRecorded(withdrawn[0], player, driver.Delivery(player))!;
        Assert.NotNull(archived.ClosedAtSequence);
        Assert.Null(driver.State.Coordination!.Proposals!.FindOpen(withdrawn[0]));
        // Preserve the actual final ledger for independent SQL-prefix and later native inspection.
        // Export only when explicitly requested, to a fresh path; it cannot change the exercised world.
        if (Environment.GetEnvironmentVariable("OPENFANTASY_RETAINED_PROPOSAL_EVIDENCE") is { Length: > 0 } evidence)
        {
            Directory.CreateDirectory(evidence);
            driver.Authority.BackupForHost(Path.Combine(evidence, "journey.sqlite3"));
            File.WriteAllBytes(Path.Combine(evidence, "genesis.ofc"), fixture.GenesisBytes);
            File.WriteAllBytes(Path.Combine(evidence, "transition.ofc"), WorldProposalTransitionCodecV1.Encode(fixture.Request));
            File.WriteAllBytes(Path.Combine(evidence, "final.ofc"), WorldSnapshotCodec.Encode(driver.State));
            File.WriteAllText(Path.Combine(evidence, "metadata.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                sourceSha256 = RetainedTransitionFixture.SourceSha256, sourceHead = RetainedTransitionFixture.SourceHeadHash,
                transitionRevision = fixture.UpgradedHead.Revision, cutRevision = cut.Revision,
                creationRevision = created.CommitSequence, acceptanceRevision = accepted.CommitSequence,
                useRevision = useReceipt.CommitSequence, finalRevision = driver.State.Revision,
                proposalId = offer.ToString(), creationCommand = create.CommandId.ToString(),
                acceptanceCommand = accept.CommandId.ToString(), useCommand = consume.CommandId.ToString(),
                finalHash = driver.State.ContentHash.Value, ordinaryUses = driver.State.Care!.TotalOrdinaryUses,
                archives = driver.Authority.InspectForHost().Journal.MaterialCareArchiveCount
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

    }

    private static ulong Potions(WorldSnapshot state, RestorativeRemedyDefinition remedy, EntityId? bag = null) =>
        state.Inventory.Custody.Lots.Where(value => value.Definition == remedy.Item && (bag is null || value.CustodianId == bag))
            .Aggregate(0UL, (sum, value) => checked(sum + value.Quantity))
        + state.Production!.Lots.Where(value => value.Definition == remedy.Item && (bag is null || value.CustodianId == bag))
            .Aggregate(0UL, (sum, value) => checked(sum + value.Quantity));

    /// <summary>Explicit authenticated commands against one durable owner, with unique deterministic identities.</summary>
    private sealed class Driver : IDisposable
    {
        private readonly string _path;
        private readonly byte[] _genesis;
        private ulong _next;

        internal Driver(string path, byte[] genesis)
        {
            _path = path;
            _genesis = genesis;
            Authority = SqliteWorldAuthority.OpenExisting(_path, _genesis);
            Service = new MaterialProposalService(Authority, NextId);
        }

        internal SqliteWorldAuthority Authority { get; private set; }
        internal MaterialProposalService Service { get; private set; }
        internal WorldSnapshot State => Authority.ReadStateForHost();

        internal EntityId NextId() => RetainedTransitionFixture.Cause("journey." + _next++);

        internal TrustedCommandDelivery Delivery(EntityId actor)
        {
            ActorControlBinding binding = State.Inventory.Controls.Single(value => value.ActorId == actor);
            return new(binding.PrincipalId, binding.SessionId);
        }

        internal WorldActionCommand Command(EntityId actor, WorldActionIntent intent)
        {
            WorldSnapshot state = State;
            ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == actor);
            return new(NextId(), control.PrincipalId, control.SessionId, actor, state.WorldId, state.BranchId,
                state.Space.GetActor(actor).Revision, intent);
        }

        internal WorldStepDecision Step(params WorldActionCommand[] actions)
        {
            WorldSnapshot state = State;
            return Authority.Commit(new WorldStepBatch(NextId(), state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], actions, state.ProfileVersion));
        }

        internal List<(long X, long Y)> WalkOutOfReach(EntityId actor, EntityId target)
        {
            const long stride = WorldMovementInput.DisplacementPerStep;
            (long X, long Y)[] directions = [(0, stride), (stride, 0), (0, -stride), (-stride, 0)];
            var walk = new List<(long X, long Y)>();
            while (State.Space.QueryInteraction(actor, target) == SpatialInteraction.Reachable)
            {
                Assert.True(walk.Count < 64, "Retained terrain must offer a finite earned route out of interaction range.");
                WorldSnapshot state = State;
                var presence = state.Space.GetActor(actor);
                var targetFeet = state.Space.GetActor(target).Feet;
                long Distance(SubunitPosition candidate) => checked(
                    (candidate.X - targetFeet.X) * (candidate.X - targetFeet.X)
                    + (candidate.Y - targetFeet.Y) * (candidate.Y - targetFeet.Y));
                var candidates = directions.Select(delta => (Delta: delta,
                    Result: state.Space.PrepareMove(actor, presence.Revision, delta.X, delta.Y)))
                    .Where(choice => choice.Result.Status == SpatialMoveStatus.Prepared)
                    .OrderByDescending(choice => Distance(choice.Result.Candidate!.Feet)).ToArray();
                Assert.NotEmpty(candidates);
                var chosen = candidates[0];
                Assert.True(Distance(chosen.Result.Candidate!.Feet) > Distance(presence.Feet), "No progress away from helper.");
                Move(actor, chosen.Delta.X, chosen.Delta.Y);
                Assert.Equal(chosen.Result.Candidate.Feet, State.Space.GetActor(actor).Feet);
                walk.Add(chosen.Delta);
            }
            return walk;
        }

        internal void Move(EntityId actor, long x, long y)
        {
            WorldSnapshot state = State;
            Authority.Commit(new WorldStepBatch(NextId(), state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, WorldBatchKind.AdvancingStep,
                [new WorldMovementInput(Delivery(actor), actor, x, y)], [], state.ProfileVersion));
        }

        internal void Reopen()
        {
            Authority.Dispose();
            Authority = SqliteWorldAuthority.OpenExisting(_path, _genesis);
            Service = new MaterialProposalService(Authority, NextId);
        }

        public void Dispose() => Authority.Dispose();
    }
}
