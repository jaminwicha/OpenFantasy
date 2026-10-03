using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    internal Task<LocalSelectedQueryResult<LocalPrivateHistoryPage>> ReadSelectedPrivateHistoryAsync(
        LocalPlayerConnection player, LocalActorSelection selection, LocalPrivateHistoryPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(request);
        lock (_inputs)
        {
            RequireSelectedQuery(player, selection, WorldControlCapabilities.PrivateHistory, false);
            try { request.Validate(); }
            catch (ArgumentException)
            {
                throw new AuthorityDispatchException("The history directory cursor is malformed.");
            }
            var key = new LocalInspectionKey(null, selection, LocalSelectedQueryOperation.ReadPrivateHistory, request);
            return player.PrivateHistoryInspection.Admit(key, () => ReadHistoryAsync(() =>
            {
                ValidateSelectedQueryAuthority(player, selection, WorldControlCapabilities.PrivateHistory, false);
                LocalPrivateHistoryPage page = _authority.ReadSelectedPrivateHistory(selection.ActorId,
                    player.Delivery, selection.ControlWitness, request);
                WorldSnapshot state = ValidateSelectedQueryAuthority(player, selection,
                    WorldControlCapabilities.PrivateHistory, false);
                return new LocalSelectedQueryResult<LocalPrivateHistoryPage>(state.WorldId, state.BranchId,
                    selection, LocalSelectedQueryOperation.ReadPrivateHistory, null, page);
            }));
        }
    }
}
