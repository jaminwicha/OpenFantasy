using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    internal PlayerWorldCompatibility DescribePlayerCompatibility(ContentHash packagesVersion)
    {
        WorldSnapshot state = _authority.ReadStateForHost();
        return new PlayerWorldCompatibility(state.WorldId, state.BranchId, state.ProfileVersion,
            state.AdmissionVersion, packagesVersion);
    }

    internal void ValidatePlayerCredentialBinding(EntityId actorId, TrustedCommandDelivery delivery, PlayerWorldCompatibility compatibility)
    {
        lock (_inputs) { EnsureInputAvailable(); }
        WorldSnapshot state = _authority.ReadStateForHost();
        if (!compatibility.Matches(state) || !state.Inventory.CanControl(delivery, actorId)
            || state.GetResident(actorId).ControllerKind != ResidentControllerKind.Player)
            throw new PlayerAdmissionException();
    }

    internal Task<LocalPlayerConnection> ConnectNetworkPlayerAsync(EntityId actorId, TrustedCommandDelivery delivery,
        PlayerWorldCompatibility compatibility, CancellationToken cancellationToken, CargoPresentation.LocalRegionObserver? regions = null) =>
        ConnectPlayerCoreAsync(actorId, delivery, regions, compatibility, cancellationToken);
}
