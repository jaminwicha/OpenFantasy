using OpenFantasy.Core.Construction;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>One explicit project and one Host-issued tool capture; the client never reconstructs project stock.</summary>
internal static class ConstructionActionOffers
{
    internal static void Add(IDictionary<FarmSemanticAction, FarmActionOffer> offers,
        IReadOnlyList<LocalConstructionWorkView> choices, FarmActionScope scope, FarmTarget? target)
    {
        if (target?.Kind != FarmTargetKind.Construction) return;
        LocalConstructionWorkView? work = choices.FirstOrDefault(value => value.Project.ProjectId == target.Id);
        WorkConstructionIntent? candidate = work?.Choices.FirstOrDefault();
        WorkConstructionIntent? intent = candidate is null ? null : work!.Prepare(candidate.ToolLotId);
        string? blocker = work is null ? "This selected project is no longer available for your work."
            : Explain(work.Blocker);
        if (intent is null && blocker is null) blocker = "No matching work tool is currently available.";
        string details = work is null ? "Choose a visible project assigned to this character."
            : $"Work {work.Project.CompletedWork}/{work.Project.RequiredWork}. "
                + "Requires the project's funded supplies and an owned matching tool. "
                + WorkStance(work.Project) + " Final work installs the structure.";
        string cost = work is null ? "One work action · world time advances"
            : $"{work.WorkPerAction} work · {work.WorkMillisecondsPerAction / 1000.0:0.###} s world time";
        offers[FarmSemanticAction.Build] = new(FarmSemanticAction.Build, scope, "Build", target.Name, target.Id,
            cost, "You worked on " + target.Name + ".", intent, blocker,
            detailText: details);
    }

    private static string WorkStance(LocalConstructionView project)
    {
        long dx = project.WorkFeet.X - project.SiteFeet.X;
        long dy = project.WorkFeet.Y - project.SiteFeet.Y;
        long dz = project.WorkFeet.Z - project.SiteFeet.Z;
        var directions = new List<string>();
        if (dx != 0) directions.Add($"{Math.Abs(dx) / 256.0:0.##} m {(dx < 0 ? "west" : "east")}");
        if (dy != 0) directions.Add($"{Math.Abs(dy) / 256.0:0.##} m {(dy < 0 ? "north" : "south")}");
        if (dz != 0) directions.Add($"{Math.Abs(dz) / 256.0:0.##} m {(dz < 0 ? "below" : "above")}");
        return directions.Count == 0 ? "Work stance: at the site."
            : "Work stance: " + string.Join(", ", directions) + " of the site.";
    }

    internal static string? Explain(LocalConstructionWorkBlocker blocker) => blocker switch
    {
        LocalConstructionWorkBlocker.None => null,
        LocalConstructionWorkBlocker.ReadingHold => "Finish the current conversation before building.",
        LocalConstructionWorkBlocker.ResolvingConsequences => "Wait for the accepted world change to finish.",
        LocalConstructionWorkBlocker.NotExploration => "Build during ordinary exploration.",
        LocalConstructionWorkBlocker.ActorUnavailable => "Wait for this character's current recovery or commitment.",
        LocalConstructionWorkBlocker.BodyUnavailable => "This character's current condition does not permit building.",
        LocalConstructionWorkBlocker.NotFunded => "The project's required supplies have not been funded.",
        LocalConstructionWorkBlocker.MissingTool => "This character needs the project's matching tool.",
        LocalConstructionWorkBlocker.WrongWorkStance => "Stand at the project's work stance before building.",
        LocalConstructionWorkBlocker.SiteUnreachable => "The project site is not reachable from this stance.",
        LocalConstructionWorkBlocker.PlacementBlocked => "The installation space is blocked. Clear it before building.",
        _ => "This work is currently unavailable."
    };
}
