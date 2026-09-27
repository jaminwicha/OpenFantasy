using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Selects one finite treatment under the admitted helper's own control.</summary>
internal sealed class LocalCareController
{
    private static readonly byte[] SelectionDomain = Encoding.ASCII.GetBytes("local.care-selection.v1");

    internal WorldActionCommand? SelectTreatment(WorldSnapshot state, Func<EntityId, bool> isOccupied)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(isOccupied);
        if (state.Care?.Pending is not { } pending)
        {
            return null;
        }
        EntityId helperId = state.Care.GetAgreement(pending.AgreementId).HelperActorId;
        ActorControlBinding? binding = HostControl(state, helperId);
        if (binding is null || WorldCareActions.PrepareRemedy(state) is not { } intent)
        {
            return null;
        }
        EntityId commandId = SelectionId(state);
        if (isOccupied(commandId))
        {
            // A refused or colliding selection stays visible for attention across recovery.
            // Allocating another identity here would silently retry the same obligation.
            return null;
        }
        return new WorldActionCommand(commandId, binding.PrincipalId, binding.SessionId, helperId,
            state.WorldId, state.BranchId, state.Space.GetActor(helperId).Revision, intent);
    }

    internal static ActorControlBinding? HostControl(WorldSnapshot state, EntityId helperId) =>
        state.GetResident(helperId).ControllerKind == ResidentControllerKind.Host
            ? state.Inventory.Controls.SingleOrDefault(value => value.ActorId == helperId) : null;

    internal static EntityId SelectionId(WorldSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var pending = state.Care?.Pending
            ?? throw new InvalidOperationException("A helper selection requires a retained care acknowledgment.");
        EntityId helperId = state.Care!.GetAgreement(pending.AgreementId).HelperActorId;
        Span<byte> input = stackalloc byte[SelectionDomain.Length + 4 * EntityId.ByteLength];
        SelectionDomain.CopyTo(input);
        int offset = SelectionDomain.Length;
        foreach (EntityId identity in new[] { state.WorldId, state.BranchId, pending.AcknowledgmentId, helperId })
        {
            identity.WriteBytes(input.Slice(offset, EntityId.ByteLength));
            offset += EntityId.ByteLength;
        }
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return EntityId.FromBytes(hash[..EntityId.ByteLength]);
    }
}
