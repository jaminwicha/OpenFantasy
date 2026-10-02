using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

internal sealed record ProposalDestinationData(EntityId ActorId, EntityId CustodianId);

internal sealed record MaterialProposalPreparationData(EntityId WorldId, EntityId BranchId,
    EntityId SenderActorId, EntityId SourceCustodianId, ReadOnlyCollection<ProposalDestinationData> Destinations,
    ReadOnlyCollection<LocalMaterialProposalLot> Lots, ulong ExpiresAtAdvancingStep)
{
    internal LocalMaterialProposalPreparation ToView() => new(WorldId, BranchId, SenderActorId, SourceCustodianId,
        Destinations.ToDictionary(value => value.ActorId, value => value.CustodianId), Lots, ExpiresAtAdvancingStep);

    internal static MaterialProposalPreparationData From(LocalMaterialProposalPreparation value) => new(
        value.CapturedWorldId, value.CapturedBranchId, value.CapturedSenderActorId, value.CapturedSourceCustodianId,
        Array.AsReadOnly(value.CapturedDestinations.Select(pair => new ProposalDestinationData(pair.Key, pair.Value)).ToArray()),
        value.Lots, value.ExpiresAtAdvancingStep);
}

internal sealed record RestorativeAvailabilityData(RestorativeAvailabilityReason Reason,
    WorldActionRejection Rejection, UseRestorativeItemIntent? Intent)
{
    internal RestorativeAvailability ToView() => RestorativeAvailability.FromObservation(Reason, Rejection, Intent);
    internal static RestorativeAvailabilityData From(RestorativeAvailability value) => new(value.Reason, value.Rejection, value.Intent);
}
