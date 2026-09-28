using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit fresh opt-in and complete admission of the coordinated world interpretation. A lived
/// ordinary-care world is never upgraded; structural admission does not authenticate history.
/// </summary>
public static class WorldCoordinatedAdmission
{
    public static WorldSnapshot CreateFresh(WorldSnapshot ordinaryCareGenesis)
    {
        ArgumentNullException.ThrowIfNull(ordinaryCareGenesis);
        WorldSnapshot basis = ordinaryCareGenesis;
        if (basis.ProfileVersion != 26 || basis.Revision != 0 || basis.AdvancingSteps != 0
            || basis.Care!.Revision != 0 || basis.EncounterLifecycle!.EncounterCount != 0
            || basis.Production!.Revision != 0 || basis.Progression!.Revision != 0
            || basis.ResourceHistory!.ArchiveAnchor is not null || basis.ResourceHistory.History.Batches.Count != 0)
        {
            throw new ArgumentException("Coordinated admission requires a fresh unearned ordinary-care genesis.", nameof(ordinaryCareGenesis));
        }
        var sources = WorldSourceIssuanceBaseline.Capture(basis);
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.coordinated-admission.v1",
            basis.ContentHash.ToByteArray(), sources.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression),
            coordination: new WorldCoordinatedContinuation(sources));
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.Coordination is not { } coordination)
        {
            return;
        }
        // The rooted ordinary-care graph has no separate structural, transport or registry owner.
        if (world.BaseProfileVersion != 21 || world.Care is not { SupportsOrdinary: true } care
            || world.Production is not { } production || world.Gathering is null || world.Excavation is null
            || world.ResourceHistory is not { ArchiveAnchor: null } resources)
        {
            throw new ArgumentException("The coordinated interpretation requires the complete ordinary-care world graph.");
        }
        if (coordination.Anchor is not { } anchor)
        {
            if (production.IsContinued || care.Continuation is not null || resources.CoordinatedAnchor is not null)
            {
                throw new ArgumentException("Continued owners require their coordinated closing boundary.");
            }
        }
        else
        {
            if (production.Continuation is not { SupportsCompleteWorld: true } material
                || material.ContentHash != anchor.Production.ContentHash || !anchor.Care.Matches(care.Continuation)
                || resources.CoordinatedAnchor?.ContentHash != anchor.ContentHash)
            {
                throw new ArgumentException("Material, care and resource owners must continue one exact coordinated boundary.");
            }
            anchor.ValidateSuccessor(world);
        }
        coordination.Sources.ValidatePartition(world);
    }
}
