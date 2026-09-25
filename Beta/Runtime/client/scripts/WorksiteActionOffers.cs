using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Captures mining and a nearby one-cell ledge request from public geometry, never granting reach or clearance.</summary>
internal static class WorksiteActionOffers
{
    internal static void Add(IDictionary<FarmSemanticAction, FarmActionOffer> offers, LocalWorldView view,
        FarmActionScope scope, FarmTarget? selected)
    {
        if (view.Worksite is not { } worksite || selected?.Kind != FarmTargetKind.Excavation) return;
        LocalExcavationView? site = worksite.ExcavationTargets.FirstOrDefault(value => value.SiteId == selected.Id);
        SubunitPosition feet = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet;
        offers[FarmSemanticAction.Excavate] = Mine(worksite, scope, selected, site, feet);
        if (worksite.SupportsAssistedTraversal)
        {
            offers[FarmSemanticAction.Traverse] = Traverse(worksite, scope, selected, site, feet);
        }
    }

    private static FarmActionOffer Mine(LocalWorksiteView view, FarmActionScope scope, FarmTarget target,
        LocalExcavationView? site, SubunitPosition feet)
    {
        LocalExcavationToolView? tool = view.Tools.FirstOrDefault(value => value.DefinitionId == site?.RequiredToolDefinitionId);
        string? blocker = CommonBlocker(view, site) ?? (site!.IsComplete ? "This face has already been excavated."
            : tool is null ? "You need the matching mining tool in your own pack."
            : feet.Z != site.Stance.Z ? site.ReachMode == ExcavationReachMode.RaisedBankTop
                ? "Reach the bank work stance before mining." : "Reach the lower work stance before mining."
            : !Near(feet, site.Stance, 448) ? "Move closer to the work stance."
            : null);
        return new FarmActionOffer(FarmSemanticAction.Excavate, scope, "Excavate", target.Name, target.Id,
            $"{view.EffortPerWork} effort", "You worked the selected face.",
            site is null || tool is null ? null : new ExcavateIntent(site.SiteId, site.SiteRevision,
                site.TerrainRevision, tool.LotId, tool.Revision, site.DestinationRevision), blocker,
            detailText: MiningDetail(view, site));
    }

    private static string MiningDetail(LocalWorksiteView view, LocalExcavationView? site)
    {
        if (site is null) return "Choose a visible worksite face.";
        string consequence = site.ReachMode == ExcavationReachMode.RaisedBankTop
            ? "Final work removes this cell and may open a path for nearby water. "
            : view.HasPhysicalConstruction ? "Final work removes this cell and may settle supported material. "
            : "Final work removes this cell. ";
        return $"Work {site.CompletedWork}/{site.RequiredWork}. " + consequence
            + "Reach, obstructions and safe consequences are checked on confirmation.";
    }

    private static FarmActionOffer Traverse(LocalWorksiteView view, FarmActionScope scope, FarmTarget target,
        LocalExcavationView? site, SubunitPosition feet)
    {
        ClimbIntent? intent = site is null ? null : SelectLedge(view, site.Stance, feet);
        string? blocker = CommonBlocker(view, site)
            ?? (intent is null ? "Stand beside the lower work stance to climb in, or inside it to climb out." : null);
        string label = intent is null ? "Climb ledge" : intent.DeltaZ < 0 ? "Climb down" : "Climb out";
        return new FarmActionOffer(FarmSemanticAction.Traverse, scope, label, target.Name, target.Id,
            $"{view.EffortPerWork} effort", "You traversed the ledge.", intent, blocker,
            detailText: "An assisted one-cell traverse. The host checks your body, the entire sweep and footing; "
                + "this preview does not promise a clear route. Ordinary stairs use walking controls.");
    }

    private static string? CommonBlocker(LocalWorksiteView view, LocalExcavationView? site) =>
        site is null ? "The selected worksite is no longer visible."
        : view.IsResolvingConsequences ? "Wait for the accepted world change to finish."
        : !view.CanManipulate ? "Your current condition does not permit this work."
        : view.PlayerEffort < view.EffortPerWork ? "You are too tired; rest a moment." : null;

    /// <summary>
    /// A finite affordance around the selected site's lower stance. Terrain is only a conservative
    /// candidate filter. It deliberately does not reconstruct authoritative body or sweep rules.
    /// </summary>
    private static ClimbIntent? SelectLedge(LocalWorksiteView view, SubunitPosition stance, SubunitPosition feet)
    {
        if (feet.Z == stance.Z + 256 && Near(feet, stance, 384))
        {
            // Preserve the unchanging horizontal axis instead of snapping both coordinates to a waypoint.
            if (SubunitPosition.CellOf(feet.X) == stance.Cell.X)
            {
                long dy = stance.Y - feet.Y;
                if (dy != 0 && Math.Abs(dy) <= 256 && Grounded(view, feet.Offset(0, dy, -256)))
                    return new ClimbIntent(0, dy, -256);
            }
            if (SubunitPosition.CellOf(feet.Y) == stance.Cell.Y)
            {
                long dx = stance.X - feet.X;
                if (dx != 0 && Math.Abs(dx) <= 256 && Grounded(view, feet.Offset(dx, 0, -256)))
                    return new ClimbIntent(dx, 0, -256);
            }
        }
        if (feet.Z == stance.Z && feet.Cell.X == stance.Cell.X && feet.Cell.Y == stance.Cell.Y)
        {
            // Stable north/west/east/south order, captured once in the offer. No retry chooses another exit.
            foreach ((long dx, long dy) in new (long, long)[] { (0, -256), (-256, 0), (256, 0), (0, 256) })
            {
                if (Grounded(view, feet.Offset(dx, dy, 256))) return new ClimbIntent(dx, dy, 256);
            }
        }
        return null;
    }

    private static bool Grounded(LocalWorksiteView view, SubunitPosition feet) =>
        IsSolid(view, feet.Offset(0, 0, -1).Cell) == true && IsSolid(view, feet.Cell) == false;

    private static bool? IsSolid(LocalWorksiteView view, CellCoordinate cell) =>
        view.Terrain.FirstOrDefault(region => region.Bounds.Contains(cell))?.IsSolid(cell);

    private static bool Near(SubunitPosition a, SubunitPosition b, long distance)
    {
        Int128 dx = (Int128)a.X - b.X;
        Int128 dy = (Int128)a.Y - b.Y;
        return dx * dx + dy * dy <= (Int128)distance * distance;
    }
}
