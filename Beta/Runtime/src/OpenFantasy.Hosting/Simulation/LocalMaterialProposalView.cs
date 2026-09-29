using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>The controlled participant's bounded live offers and outcomes; constructing it publishes nothing.</summary>
public sealed class LocalMaterialProposalView
{
    private LocalMaterialProposalView(WorldSnapshot state, EntityId playerActorId)
    {
        Offers = Array.AsReadOnly(MaterialProposalService.Project(state, playerActorId).ToArray());
        Preparation = LocalMaterialProposalPreparation.Project(state, playerActorId);
    }

    public ReadOnlyCollection<MaterialProposalView> Offers { get; }
    public LocalMaterialProposalPreparation? Preparation { get; }

    internal static LocalMaterialProposalView? Project(WorldSnapshot state, EntityId playerActorId) =>
        state.Coordination?.Proposals is null ? null
            : new(state, playerActorId);
}
