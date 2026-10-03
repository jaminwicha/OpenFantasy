using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

internal static class ConstructionObservationValidation
{
    internal static void Validate(LocalWorldView view)
    {
        ValidateStructures(view.Structures);
        ValidateWork(view.PlayerActorId, view.Construction, view.ConstructionWork, view.Actors,
            view.HasReadingHold, view.IsResolvingConsequences);
    }

    internal static void Validate(LocalControlledActorView view)
    {
        ValidateStructures(view.Structures);
        if (view.Production is { } production)
            ValidateWork(view.ActorId, production.Construction, production.ConstructionWorkViews, view.Actors,
                view.HasReadingHold, view.IsResolvingConsequences);
    }

    private static void ValidateStructures(IReadOnlyList<LocalPublicStructureView>? structures)
    {
        if (structures is null || structures.Count > LocalSpatialSnapshot.MaximumStructures
            || structures.Select(value => value.StructureId).Distinct().Count() != structures.Count
            || !structures.Select(value => value.StructureId).SequenceEqual(structures.Select(value => value.StructureId).Order()))
            throw new FormatException("The construction presentation requires a complete ordered bounded public structure observation.");
        foreach (var structure in structures)
        {
            // Uses the same spatial span/unique-box bound; this admits presentation only, never a world collider.
            _ = new SpatialStructure(structure.StructureId, structure.Members.Select(member => member.Collision));
        }
    }

    private static void ValidateWork(EntityId actorId, IReadOnlyList<LocalConstructionView> projects,
        IReadOnlyList<LocalConstructionWorkView> work, IReadOnlyList<LocalActorView> actors, bool held, bool resolving)
    {
        if (work.Count > ConstructionSnapshot.MaximumProjects || work.Select(value => value.Project.ProjectId).Distinct().Count() != work.Count)
            throw new FormatException("Construction work observations must be bounded and unique.");
        var actor = actors.Single(value => value.ActorId == actorId);
        foreach (var row in work)
        {
            if (row.ActorId != actorId || !projects.Contains(row.Project)
                || row.IsAtWorkStance != (actor.Feet == row.Project.WorkFeet)
                || row.CanWork && (held || resolving || actor.IsIncapacitated)
                || row.Blocker == LocalConstructionWorkBlocker.ReadingHold && !held
                || row.Blocker == LocalConstructionWorkBlocker.ResolvingConsequences && !resolving
                || row.Blocker == LocalConstructionWorkBlocker.NotFunded && row.IsFunded
                || row.Blocker == LocalConstructionWorkBlocker.MissingTool && row.Choices.Count != 0
                || row.Blocker == LocalConstructionWorkBlocker.WrongWorkStance && row.IsAtWorkStance
                || row.Blocker == LocalConstructionWorkBlocker.SiteUnreachable && row.IsSiteReachable
                || row.Blocker == LocalConstructionWorkBlocker.ActorUnavailable && row.CanAct
                || row.Blocker == LocalConstructionWorkBlocker.BodyUnavailable && row.CanManipulate)
                throw new FormatException("Construction choices disagree with their operated actor and observed work facts.");
        }
    }
}
