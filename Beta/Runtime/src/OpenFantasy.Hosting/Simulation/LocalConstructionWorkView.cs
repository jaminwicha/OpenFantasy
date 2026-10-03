using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Hosting.Simulation;

public enum LocalConstructionWorkBlocker
{
    None = 0,
    ReadingHold = 1,
    ResolvingConsequences = 2,
    NotExploration = 3,
    ActorUnavailable = 4,
    BodyUnavailable = 5,
    NotFunded = 6,
    MissingTool = 7,
    WrongWorkStance = 8,
    SiteUnreachable = 9,
    PlacementBlocked = 10
}

/// <summary>One builder's current work facts and exact tool choices, without exposing the project stock or quest history.</summary>
public sealed class LocalConstructionWorkView
{
    public LocalConstructionWorkView(EntityId actorId, LocalConstructionView project, ulong projectRevision, ulong workPerAction, bool isFunded,
        bool canAct, bool canManipulate, bool isAtWorkStance, bool isSiteReachable,
        LocalConstructionWorkBlocker blocker, IEnumerable<WorkConstructionIntent> choices)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(choices);
        var captured = choices.Take(257).ToArray();
        if (actorId == default || project.BuilderActorId != actorId || project.IsCompleted
            || project.CompletedWork >= project.RequiredWork || project.RequiredWork > ConstructionDefinition.MaximumRequiredWork
            || workPerAction == 0 || workPerAction > ConstructionDefinition.MaximumRequiredWork
            || !Enum.IsDefined(blocker) || captured.Length > 256 || captured.Any(choice => choice is null
                || choice.ProjectId != project.ProjectId || choice.ExpectedProjectRevision != projectRevision)
            || captured.Select(choice => choice.ToolLotId).Distinct().Count() != captured.Length
            || blocker == LocalConstructionWorkBlocker.None && (!isFunded || !canAct || !canManipulate
                || !isAtWorkStance || !isSiteReachable || captured.Length == 0))
            throw new ArgumentException("Construction work must describe one unfinished builder project and bounded exact choices.");
        ActorId = actorId;
        Project = project;
        ProjectRevision = projectRevision;
        WorkPerAction = workPerAction;
        IsFunded = isFunded;
        CanAct = canAct;
        CanManipulate = canManipulate;
        IsAtWorkStance = isAtWorkStance;
        IsSiteReachable = isSiteReachable;
        Blocker = blocker;
        Choices = Array.AsReadOnly(captured.OrderBy(choice => choice.ToolLotId).ToArray());
    }

    public EntityId ActorId { get; }
    public LocalConstructionView Project { get; }
    public bool IsFunded { get; }
    public bool CanAct { get; }
    public bool CanManipulate { get; }
    public bool IsAtWorkStance { get; }
    public bool IsSiteReachable { get; }
    public LocalConstructionWorkBlocker Blocker { get; }
    public bool CanWork => Blocker == LocalConstructionWorkBlocker.None;
    public ulong ProjectRevision { get; }
    public ulong WorkPerAction { get; }
    public ulong WorkMillisecondsPerAction => ExposurePacingV1.ExplorationMillisecondsPerStep;
    public ReadOnlyCollection<WorkConstructionIntent> Choices { get; }
    public WorkConstructionIntent? Prepare(EntityId toolLotId) => CanWork
        ? Choices.SingleOrDefault(choice => choice.ToolLotId == toolLotId) : null;
}
