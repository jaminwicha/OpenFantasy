using System.Collections.ObjectModel;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The immutable publication identities for one accepted excavation's complete completion interval.
/// Forward cursors share this owner; restored commands reconstruct it from their own admitted inputs.
/// </summary>
internal sealed class PendingExcavationReservations
{
    internal PendingExcavationReservations(WorldActionCommand command, EntityId triggerCauseId,
        ulong basisRevision, ulong maximumContinuations)
    {
        var results = new List<EntityId>();
        // Reserve both admissible outcomes at every possible terminal commit, without issuing them.
        for (ulong offset = 2; offset <= checked(maximumContinuations + 1); offset++)
        {
            ulong sequence = checked(basisRevision + offset);
            results.Add(WorldActionReceipt.Create(command, sequence, WorldActionRejection.None).ResultId);
            results.Add(WorldActionReceipt.Create(command, sequence, WorldActionRejection.Unavailable).ResultId);
        }
        Results = results.AsReadOnly();
        SourceId = EntityId.DeriveSpawned(command.CommandId, 0);
        EntityId[] identities = results.Append(SourceId).Append(command.CommandId).Append(triggerCauseId).ToArray();
        if (command.Intent is not ExcavateIntent || identities.Distinct().Count() != identities.Length)
        {
            throw new ArgumentException("A pending excavation needs unique command, cause and publication identities.");
        }
    }

    internal EntityId SourceId { get; }
    internal ReadOnlyCollection<EntityId> Results { get; }
}
