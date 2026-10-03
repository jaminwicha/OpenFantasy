using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.DelegatedRecovery;

public sealed partial class DelegatedRecoveryFixture
{
    internal WorldActionCommand GiftCommand { get; private set; } = null!;
    internal WorldActionCommand GiftWithdrawal { get; private set; } = null!;
    internal WorldMaterialProposalOutcome GiftOutcome { get; private set; } = null!;

    private void Prepare()
    {
        using var authority = SqliteWorldAuthority.CreateNew(DatabasePath, Genesis);
        var state = authority.ReadStateForHost();
        // Approach before delegation, using the owner's real home delivery and ordinary movement.
        WalkBeside(authority, Owner, Opponent);
        state = authority.ReadStateForHost();
        var grant = Command(state, Owner, new OfferControlPermissionIntent(Grantee, 0, WorldControlCapabilities.All));
        state = Commit(authority, Batch(state, WorldBatchKind.ControlPermission, [grant])).Candidate;
        var slot = state.ControlPermissions!.Find(Owner, Grantee)!;
        state = Commit(authority, Batch(state, WorldBatchKind.ControlPermission,
            [Command(state, Grantee, new AcceptControlPermissionIntent(Owner, slot.Revision, grant.CommandId, slot.TermsHash!))])).Candidate;
        state = ControllerEvent(authority, WorldControllerEventKind.Disconnected);
        var segment = authority.BeginControllerRecoverySegment();
        authority.CheckpointControllerAllowances(segment.Generation, segment.Allowances.Select(row =>
            row.ActorId == Owner ? row with { ElapsedHostMilliseconds = WorldControllerState.ReconnectAllowanceMilliseconds } : row).ToArray());
        state = ControllerEvent(authority, WorldControllerEventKind.AllowanceExpired);
        slot = state.ControlPermissions!.Find(Owner, Grantee)!;
        var owner = state.Controllers!.Get(Owner);
        Witness = new(Owner, Grantee, slot.GrantCommandId!.Value, slot.AcceptanceCommandId!.Value,
            slot.Revision, slot.Terms!.Capabilities, slot.TermsHash!, NextId(), 1, state.Revision + 1,
            owner.DisconnectId!.Value, owner.Revision);
        state = Commit(authority, new(Witness.AssignmentId, state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.ControlAssignment, [], [], state.ProfileVersion,
            controlAssignmentEvent: new(Owner, WorldControlAssignmentEventKind.Acquire, 0, Witness))).Candidate;
        CloseDelegatedGift(authority);
        state = authority.ReadStateForHost();
        var lifecycle = state.EncounterLifecycle!;
        var scenario = lifecycle.GetScenario(ScenarioId);
        var enter = Command(state, Owner, new EnterEncounterIntent(ScenarioId,
            EncounterEpisodeIdentity.Derive(lifecycle, ScenarioId), lifecycle.EncounterCount,
            state.Space.GetActor(scenario.TargetActorId).Revision), Witness);
        state = Commit(authority, Batch(state, WorldBatchKind.EncounterLifecycle, [enter])).Candidate;
        for (int transition = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; transition++)
        {
            Assert.True(transition < 3);
            state = Commit(authority, Batch(state, WorldBatchKind.EncounterLifecycle)).Candidate;
        }

        bool archived = false;
        for (int boundary = 0; ; boundary++)
        {
            Assert.True(boundary < 3_000, "Actual delegated attack/retreat exceeds the inherited 3,000-boundary budget.");
            state = authority.ReadStateForHost();
            Assert.True(state.Progression!.GetActor(Owner).CurrentHp > 0);
            var batch = NextCombatBatch(state, mayChooseRetreat: true);
            var expected = authority.Preview(batch);
            if (expected.SealedMaterialCareSegment is not null)
            {
                Assert.False(archived);
                Assert.NotNull(PaidCommand);
                Assert.Equal(255, state.ResourceHistory!.History.Batches.Count);
                var active = Assert.IsType<EncounterRetreatContinuation>(state.Retreat!.Active);
                Assert.True(active.HasStarted);
                Assert.Equal(PaidCommand.ContentHash, active.Command.ContentHash);
                Assert.Equal(Witness, state.EffectiveControl!.Find(Owner)!.Witness);
                PublishBoundary(authority, "archive", batch);
                archived = true;
                state = authority.ReadStateForHost();
                var revoke = Command(state, Owner, new RevokeControlPermissionIntent(Grantee,
                    state.ControlPermissions!.Find(Owner, Grantee)!.Revision));
                PublishBoundary(authority, "revoke", Batch(state, WorldBatchKind.ControlPermission, [revoke]));
                Assert.NotNull(authority.ReadStateForHost().Retreat!.Active);
                Assert.Null(authority.ReadStateForHost().EffectiveControl!.Find(Owner)!.Witness);
                break;
            }
            var accepted = Commit(authority, batch);
            if (PaidCommand is not null && batch.Actions.Any(command => command.CommandId == PaidCommand.CommandId))
                PaidReceipt = accepted.Receipts.Single(receipt => receipt.CommandId == PaidCommand.CommandId);
        }

        for (int boundary = 0; ; boundary++)
        {
            Assert.True(boundary < 2_000, "Paid retreat must finish within its original finite continuation budget.");
            state = authority.ReadStateForHost();
            Assert.NotNull(state.Retreat!.Active);
            var batch = NextCombatBatch(state, mayChooseRetreat: false);
            var expected = authority.Preview(batch);
            if (expected.Candidate.Retreat!.Active is null)
            {
                Assert.Equal(EncounterRetreatStatus.Disengaged, expected.Candidate.Retreat.Last!.Status);
                PublishBoundary(authority, "resolution", batch);
                break;
            }
            Commit(authority, batch);
        }
        Final = authority.ReadStateForHost();
        AssertOriginalOwnership(Final);
        AssertPaidReceipt(authority);
        AssertPaidCompletion(Final);
        File.WriteAllBytes(Path.Combine(DirectoryPath, "final.ofc"), WorldSnapshotCodec.Encode(Final));
    }

    private void CloseDelegatedGift(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var lot = state.Inventory.Custody.Lots.First(row => row.CustodianId == state.GetResident(Owner).InventoryCustodianId
            && row.Quantity > 0 && row.ReservedBy is null);
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, Owner, Grantee,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(Grantee).InventoryCustodianId, 1, lot.Revision);
        GiftCommand = Command(state, Owner, new ProposeMaterialIntent(terms, state.AdvancingSteps + 1_024), Witness);
        var receipt = authority.SubmitMaterialProposalCommand(GiftCommand, Delivery(Grantee), NextId());
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        state = authority.ReadStateForHost();
        GiftWithdrawal = Command(state, Owner, new WithdrawMaterialProposalIntent(WorldMaterialProposal.DeriveIdentity(GiftCommand.CommandId)), Witness);
        receipt = authority.SubmitMaterialProposalCommand(GiftWithdrawal, Delivery(Grantee), NextId());
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        GiftOutcome = Assert.Single(authority.ReadStateForHost().Coordination!.Proposals!.Outcomes);
        Assert.Equal(WorldMaterialProposalOutcomeKind.Withdrawn, GiftOutcome.Kind);
        Assert.Equal(Witness, GiftOutcome.Proposal.ControlWitness);
        Assert.Equal(Witness, GiftOutcome.Termination!.ControlWitness);
        Assert.Null(GiftOutcome.Settlement);
    }

    private WorldSnapshot ControllerEvent(SqliteWorldAuthority authority, WorldControllerEventKind kind)
    {
        var state = authority.ReadStateForHost();
        var row = state.Controllers!.Get(Owner);
        var input = kind == WorldControllerEventKind.Disconnected
            ? new WorldControllerEvent(Owner, kind, row.Revision, reason: WorldControllerDisconnectReason.TransportLost)
            : new WorldControllerEvent(Owner, kind, row.Revision, row.DisconnectId, WorldControllerState.ReconnectAllowanceMilliseconds);
        return Commit(authority, new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            WorldBatchKind.ControllerLifecycle, [], [], state.ProfileVersion, input)).Candidate;
    }

    private void PublishBoundary(SqliteWorldAuthority authority, string name, WorldStepBatch batch)
    {
        var before = authority.ReadStateForHost();
        var after = authority.Preview(batch).Candidate;
        string beforeDatabase = Path.Combine(DirectoryPath, name + "-before.sqlite3");
        string afterDatabase = Path.Combine(DirectoryPath, name + "-after.sqlite3");
        authority.BackupForHost(beforeDatabase);
        var committed = Commit(authority, batch).Candidate;
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(committed));
        authority.BackupForHost(afterDatabase);
        _boundaries.Add(new(name, beforeDatabase, afterDatabase, before, committed, batch));
        File.WriteAllBytes(Path.Combine(DirectoryPath, name + ".batch"), WorldStepCodec.Encode(batch));
        File.WriteAllBytes(Path.Combine(DirectoryPath, name + "-before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllBytes(Path.Combine(DirectoryPath, name + "-after.ofc"), WorldSnapshotCodec.Encode(committed));
    }

    private WorldStepBatch NextCombatBatch(WorldSnapshot state, bool mayChooseRetreat)
    {
        var timing = state.Pacing!.Encounter!;
        var due = WorldEncounterActions.DueActors(state);
        List<WorldActionCommand> actions = [];
        if (due.Any(actor => actor.ActorId == Owner) && !timing.Decisions.Any(value => value.ActorId == Owner)
            && !WorldEncounterRetreats.HoldsActor(state, Owner))
        {
            if (mayChooseRetreat && PaidCommand is null && state.ResourceHistory!.History.Batches.Count >= 253)
            {
                Assert.InRange(state.ResourceHistory.History.Batches.Count, 253, 254);
                var route = RouteWithOpeningContact(state);
                PaidCommand = Command(state, Owner, new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision,
                    QueueEncounterRetreatIntent.Endpoint(state.Space.GetActor(Owner).Feet, route), route), Witness);
                actions.Add(PaidCommand);
            }
            else actions.Add(Command(state, Owner, new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision),
                state.EffectiveControl!.Find(Owner)?.Witness));
        }
        actions.AddRange(new LocalEncounterController().SelectChoices(state, NextId));
        return Batch(state, actions.Count != 0 ? WorldBatchKind.EncounterDecision
            : due.Count != 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance, actions);
    }

    private IReadOnlyList<EncounterRetreatDirection> RouteWithOpeningContact(WorldSnapshot state)
    {
        var advice = WorldEncounterRetreatAdvisor.Advise(state, Owner);
        Assert.True(advice.IsFound);
        foreach (var toward in Enum.GetValues<EncounterRetreatDirection>())
        {
            var back = toward switch
            {
                EncounterRetreatDirection.North => EncounterRetreatDirection.South,
                EncounterRetreatDirection.South => EncounterRetreatDirection.North,
                EncounterRetreatDirection.East => EncounterRetreatDirection.West,
                _ => EncounterRetreatDirection.East
            };
            LocalSpatialSnapshot? next = WorldEncounterRetreats.WalkSample(state.Space, Owner, toward, out _);
            if (next is null || next.QueryInteraction(Opponent, Owner, state.EncounterActions!.Abilities.Max(value => value.RangeSubunits))
                != SpatialInteraction.Reachable) continue;
            var route = new[] { toward, back, toward, back, toward, back }.Concat(advice.Route).ToArray();
            if (route.Length > EncounterRetreatRules.MaximumRouteSamples) continue;
            var timing = state.Pacing!.Encounter!;
            var intent = new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision,
                QueueEncounterRetreatIntent.Endpoint(state.Space.GetActor(Owner).Feet, route), route);
            if (WorldEncounterRetreats.EvaluateQueue(state, Owner, intent) == WorldActionRejection.None) return route;
        }
        throw new InvalidOperationException("No lawful contact-preserving opening and escape route exists.");
    }

    private void WalkBeside(SqliteWorldAuthority authority, EntityId actor, EntityId target)
    {
        var state = authority.ReadStateForHost();
        var at = state.Space.GetActor(target).Feet;
        var goal = new[] { at.Offset(256, 0, 0), at.Offset(-256, 0, 0), at.Offset(0, 256, 0), at.Offset(0, -256, 0) }
            .First(point => state.Space.QueryGroundRoute(actor, point).Status == GroundRouteStatus.Reachable);
        for (int step = 0; state.Space.GetActor(actor).Feet != goal; step++)
        {
            Assert.True(step < 512, "Actual approach exceeds the existing 512-sample route bound.");
            var route = state.Space.QueryGroundRoute(actor, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var next = route.NextWaypoint!.Value;
            var feet = state.Space.GetActor(actor).Feet;
            long dx = Math.Clamp(next.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(next.Y - feet.Y, -32, 32) : 0;
            state = Commit(authority, new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new(Delivery(actor), actor, dx, dy)], [], state.ProfileVersion)).Candidate;
            Assert.Equal(feet.Offset(dx, dy, 0), state.Space.GetActor(actor).Feet);
        }
    }
}
