using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerPermissions;

/// <summary>Two humans are declared in fresh admission; every later permission is a real journal action.</summary>
internal sealed class PermissionStorageFixture
{
    private static readonly Lazy<WorldSnapshot> Worlds = new(() => WorldControlPermissionAdmission.CreateFresh(
        WorldCareDispositionAdmission.CreateFresh(new RecoveryJournalFixture(humanObserver: true).Genesis)));
    internal RecoveryJournalFixture Journey { get; } = new(humanObserver: true);
    internal WorldSnapshot Genesis => Worlds.Value;
    internal EntityId Owner => Journey.Patient;
    internal EntityId Grantee => Journey.Observer;
    internal EntityId Other => Journey.Helper;
    internal EncounterJourneyWorldFixture Commands => Journey.Commands;

    internal TrustedCommandDelivery Delivery(EntityId actor)
    {
        var binding = Genesis.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(binding.PrincipalId, binding.SessionId);
    }

    internal WorldActionCommand Offer(WorldSnapshot state, ulong revision = 0,
        WorldControlCapabilities capabilities = WorldControlCapabilities.CombatAndSurvival) =>
        Commands.Command(state, Owner, new OfferControlPermissionIntent(Grantee, revision, capabilities));

    internal WorldActionCommand Accept(WorldSnapshot state)
    {
        var slot = state.ControlPermissions!.Find(Owner, Grantee);
        Assert.NotNull(slot);
        return Commands.Command(state, Grantee,
            new AcceptControlPermissionIntent(Owner, slot.Revision,
                Assert.IsType<EntityId>(slot.GrantCommandId), Assert.IsType<ContentHash>(slot.TermsHash)));
    }

    internal WorldActionCommand Revoke(WorldSnapshot state) => Commands.Command(state, Owner,
        new RevokeControlPermissionIntent(Grantee, state.ControlPermissions!.Find(Owner, Grantee)!.Revision));

    internal WorldStepDecision Commit(SqliteWorldAuthority authority, WorldActionCommand command)
    {
        WorldSnapshot before = authority.ReadStateForHost();
        WorldStepDecision decision = authority.Commit(Commands.Batch(before, WorldBatchKind.ControlPermission, [command]));
        AssertGameplayUnchanged(before, decision.Candidate);
        var receipt = Assert.Single(decision.Receipts);
        Assert.Equal(command.PrincipalId, receipt.PrincipalId);
        Assert.Equal(command.CommandId, receipt.CommandId);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
        Assert.Equal(before.Revision + 1, receipt.CommitSequence);
        Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
        Assert.True(authority.IsIdentityOccupiedForHost(receipt.ResultId));
        return decision;
    }

    internal WorldSnapshot Apply(SqliteWorldAuthority authority, WorldActionCommand command)
    {
        var decision = Commit(authority, command);
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        return decision.Candidate;
    }

    internal WorldSnapshot Event(SqliteWorldAuthority authority, EntityId actor, WorldControllerEventKind kind)
    {
        var state = authority.ReadStateForHost();
        var row = state.Controllers!.Get(actor);
        var input = kind == WorldControllerEventKind.Disconnected
            ? new WorldControllerEvent(actor, kind, row.Revision, reason: WorldControllerDisconnectReason.TransportLost)
            : new WorldControllerEvent(actor, kind, row.Revision, row.DisconnectId,
                kind == WorldControllerEventKind.AllowanceExpired ? 30_000UL : 0);
        return authority.Commit(new WorldStepBatch(Commands.NextId(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.ControllerLifecycle, [], [], state.ProfileVersion, input)).Candidate;
    }

    internal static void AssertGameplayUnchanged(WorldSnapshot before, WorldSnapshot after)
    {
        byte[][] original = Fields(WorldSnapshotCodec.Encode(before));
        byte[][] current = Fields(WorldSnapshotCodec.Encode(after));
        Assert.Equal(30, original.Length);
        Assert.Equal(original.Length, current.Length);
        // Only the commit revision and permission facet differ; lifecycle revision follows the commit.
        for (int index = 0; index < original.Length; index++)
            if (index is not 8 and not 21 and not 29) Assert.Equal(original[index], current[index]);
        var oldLifecycle = Fields(original[21]);
        var newLifecycle = Fields(current[21]);
        Assert.Equal(17, oldLifecycle.Length);
        Assert.Equal(oldLifecycle.Length, newLifecycle.Length);
        for (int index = 0; index < oldLifecycle.Length; index++)
            if (index != 5) Assert.Equal(oldLifecycle[index], newLifecycle[index]);
        Assert.Equal(before.Revision, before.EncounterLifecycle!.Revision);
        Assert.Equal(after.Revision, after.EncounterLifecycle!.Revision);
        Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
        Assert.Equal(before.Calendar, after.Calendar);
    }

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8))), offset = 12;
        var fields = new byte[count][];
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }
}
