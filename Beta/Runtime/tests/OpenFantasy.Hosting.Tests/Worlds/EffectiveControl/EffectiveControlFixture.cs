using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Tests.Worlds.ControllerPermissions;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;

/// <summary>Fresh two-home calibration; permissions, elapsed allowance and acquisition use real journal boundaries.</summary>
internal sealed class EffectiveControlFixture
{
    private readonly PermissionStorageFixture _basis = new();
    internal EffectiveControlFixture() => Genesis = WorldDelegatedControlAdmission.CreateFresh(_basis.Genesis);
    internal WorldSnapshot Genesis { get; }
    internal EntityId Owner => _basis.Owner;
    internal EntityId Grantee => _basis.Grantee;
    internal EntityId Other => _basis.Other;
    internal RecoveryJournalFixture Journey => _basis.Journey;
    internal EncounterJourneyWorldFixture Commands => _basis.Commands;
    internal TrustedCommandDelivery Delivery(EntityId actor) => _basis.Delivery(actor);

    internal WorldSnapshot Grant(SqliteWorldAuthority authority,
        WorldControlCapabilities capabilities = WorldControlCapabilities.CombatAndSurvival)
    {
        var state = authority.ReadStateForHost();
        var offered = Commands.Command(state, Owner, new OfferControlPermissionIntent(Grantee,
            state.ControlPermissions!.Find(Owner, Grantee)?.Revision ?? 0, capabilities));
        state = Apply(authority, WorldBatchKind.ControlPermission, offered);
        var slot = state.ControlPermissions!.Find(Owner, Grantee)!;
        return Apply(authority, WorldBatchKind.ControlPermission, Commands.Command(state, Grantee,
            new AcceptControlPermissionIntent(Owner, slot.Revision, offered.CommandId, slot.TermsHash!)));
    }

    internal WorldSnapshot Revoke(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        return Apply(authority, WorldBatchKind.ControlPermission, Commands.Command(state, Owner,
            new RevokeControlPermissionIntent(Grantee, state.ControlPermissions!.Find(Owner, Grantee)!.Revision)));
    }

    internal WorldSnapshot Event(SqliteWorldAuthority authority, EntityId actor, WorldControllerEventKind kind) =>
        _basis.Event(authority, actor, kind);

    internal WorldSnapshot LoseAndExpire(SqliteWorldAuthority authority)
    {
        Event(authority, Owner, WorldControllerEventKind.Disconnected);
        var segment = authority.BeginControllerRecoverySegment();
        authority.CheckpointControllerAllowances(segment.Generation, segment.Allowances.Select(row =>
            row.ActorId == Owner ? row with { ElapsedHostMilliseconds = 30_000 } : row).ToArray());
        return Event(authority, Owner, WorldControllerEventKind.AllowanceExpired);
    }

    internal WorldControlWitness NextWitness(WorldSnapshot state)
    {
        var grant = state.ControlPermissions!.Find(Owner, Grantee)!;
        var owner = state.Controllers!.Get(Owner);
        return new(Owner, Grantee, grant.GrantCommandId!.Value, grant.AcceptanceCommandId!.Value,
            grant.Revision, grant.Terms!.Capabilities, grant.TermsHash!, Commands.NextId(),
            checked((state.EffectiveControl!.Find(Owner)?.Revision ?? 0) + 1), checked(state.Revision + 1),
            owner.DisconnectId!.Value, owner.Revision);
    }

    internal WorldSnapshot Acquire(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var witness = NextWitness(state);
        return authority.Commit(new WorldStepBatch(witness.AssignmentId, state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.ControlAssignment, [], [], state.ProfileVersion,
            controlAssignmentEvent: new(Owner, WorldControlAssignmentEventKind.Acquire,
                witness.AssignmentRevision - 1, witness))).Candidate;
    }

    internal WorldSnapshot Release(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        return authority.Commit(new WorldStepBatch(Commands.NextId(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.ControlAssignment, [], [], state.ProfileVersion,
            controlAssignmentEvent: new(Owner, WorldControlAssignmentEventKind.Release,
                state.EffectiveControl!.Find(Owner)!.Revision))).Candidate;
    }

    internal WorldActionCommand Delegated(WorldSnapshot state, WorldActionIntent intent,
        WorldControlWitness? witness = null)
    {
        var delivery = Delivery(Grantee);
        return new(Commands.NextId(), delivery.PrincipalId, delivery.SessionId, Owner,
            state.WorldId, state.BranchId, state.Space.GetActor(Owner).Revision, intent,
            witness ?? state.EffectiveControl!.Find(Owner)!.Witness);
    }

    internal WorldSnapshot Apply(SqliteWorldAuthority authority, WorldBatchKind kind, WorldActionCommand command)
    {
        var state = authority.ReadStateForHost();
        var result = authority.Commit(Commands.Batch(state, kind, [command]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(result.Receipts).Rejection);
        return result.Candidate;
    }
}
