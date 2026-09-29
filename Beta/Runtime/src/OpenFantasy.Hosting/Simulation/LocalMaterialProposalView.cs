using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>The controlled participant's bounded live offers and outcomes; constructing it publishes nothing.</summary>
public sealed class LocalMaterialProposalView
{
    private LocalMaterialProposalView(IEnumerable<MaterialProposalView> offers)
    {
        Offers = Array.AsReadOnly(offers.ToArray());
    }

    public ReadOnlyCollection<MaterialProposalView> Offers { get; }

    internal static LocalMaterialProposalView? Project(WorldSnapshot state, EntityId playerActorId) =>
        state.Coordination?.Proposals is null ? null
            : new(MaterialProposalService.Project(state, playerActorId));
}
