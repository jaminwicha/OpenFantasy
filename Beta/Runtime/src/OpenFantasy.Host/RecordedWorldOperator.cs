using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Host;

/// <summary>Privileged local inspection of a closed composed-world save at an exact commit.</summary>
internal sealed class RecordedWorldOperator
{
    internal string Inspect(string databasePath, string commitText)
    {
        if (!ulong.TryParse(commitText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong revision))
        {
            throw new ArgumentException("The recorded commit must be an unsigned decimal integer.", nameof(commitText));
        }
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(databasePath);
        RecordedWorldState recorded = authority.ReadRecordedStateForHost(revision);
        WorldSnapshot state = recorded.State;
        return JsonSerializer.Serialize(new
        {
            Contract = "recorded-world-state-v1",
            WorldId = state.WorldId.ToString(),
            BranchId = state.BranchId.ToString(),
            recorded.SourceHeadRevision,
            SourceHeadHash = recorded.SourceHeadHash.ToString(),
            RecordedRevision = state.Revision,
            StateHash = recorded.StateHash.ToString(),
            state.ProfileVersion,
            state.Calendar,
            state.AdvancingSteps,
            state.HasReadingHold,
            Watershed = state.Watershed is { } plan ? new WatershedDiagnosticReport(plan) : null,
            WatershedNetwork = state.WatershedNetwork is { } network ? new WatershedNetworkDiagnosticReport(network) : null,
            Reservoirs = state.Reservoirs is { } reservoirs ? new LiquidReservoirDiagnosticReport(reservoirs) : null,
            Transport = state.Transport is { } transport ? new ReservoirTransportDiagnosticReport(transport) : null,
            Actors = state.Residents.Select(resident => new
            {
                ActorId = resident.ActorId.ToString(),
                resident.DisplayName,
                Position = state.Space.GetActor(resident.ActorId).Feet,
                PresenceRevision = state.Space.GetActor(resident.ActorId).Revision,
                SourceItemQuantity = state.Inventory.Custody.GetStoredQuantity(resident.InventoryCustodianId)
            }).ToArray(),
            SourceLotCount = state.Inventory.Custody.Lots.Count,
            CraftedLotCount = state.Production?.Lots.Count ?? 0,
            StructureCount = state.Space.Structures.Count
        });
    }
}
