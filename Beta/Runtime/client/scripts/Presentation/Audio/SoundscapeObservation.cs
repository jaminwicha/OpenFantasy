using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Presentation.Audio;

/// <summary>
/// One listener's identity for sound: the world, its branch incarnation and the participant whose permitted view is
/// presented. A different listener starts a new presentation visit; its one-shot history never carries over.
/// </summary>
internal sealed record SoundscapeListener(EntityId WorldId, EntityId BranchId, EntityId PlayerActorId);

/// <summary>An actor the listener may see, with its committed presence revision.</summary>
internal readonly record struct SoundscapeActor(EntityId ActorId, SubunitPosition Feet, ulong PresenceRevision,
    bool IsIncapacitated);

/// <summary>A gathering source the listener may see; a decrease in remaining swings is an actual committed contact.</summary>
internal readonly record struct SoundscapeGatherNode(EntityId NodeId, GatherNodeKind Kind, SubunitPosition Feet,
    ulong Revision, uint RemainingSwings);

/// <summary>Which kind of committed labor advanced a work site.</summary>
internal enum SoundscapeWorkKind { Construction = 1, Excavation = 2 }

/// <summary>A visible work site whose completed work only increases when real labor commits.</summary>
internal readonly record struct SoundscapeWorkSite(EntityId SiteId, SoundscapeWorkKind Kind, SubunitPosition Origin,
    ulong CompletedWork);

/// <summary>The listener's own encounter situation, as its permitted view states it.</summary>
internal enum SoundscapeEncounterPhase { None = 0, Engaged = 1, Concluded = 2 }

/// <summary>
/// The permitted facts of one presented view that sound may use. It is built only from the participant's own
/// <see cref="LocalWorldView"/>, so sound can never disclose a source, actor or event that view withholds.
/// It is a presentation projection: nothing here is written back to the world.
/// </summary>
internal sealed class SoundscapeObservation
{
    internal SoundscapeObservation(SoundscapeListener listener, IEnumerable<SoundscapeActor> actors,
        IEnumerable<SoundscapeGatherNode> gatherNodes, IEnumerable<SoundscapeWorkSite> workSites,
        SoundscapeEncounterPhase encounter, bool isConversing)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(gatherNodes);
        ArgumentNullException.ThrowIfNull(workSites);
        if (!Enum.IsDefined(encounter)) throw new ArgumentOutOfRangeException(nameof(encounter));
        Listener = listener;
        Actors = actors.ToList().AsReadOnly();
        GatherNodes = gatherNodes.ToList().AsReadOnly();
        WorkSites = workSites.ToList().AsReadOnly();
        Encounter = encounter;
        IsConversing = isConversing;
        RequireDistinct(Actors.Select(actor => actor.ActorId), nameof(actors));
        RequireDistinct(GatherNodes.Select(node => node.NodeId), nameof(gatherNodes));
        RequireDistinct(WorkSites.Select(site => (site.SiteId, site.Kind)), nameof(workSites));
    }

    internal SoundscapeListener Listener { get; }
    internal ReadOnlyCollection<SoundscapeActor> Actors { get; }
    internal ReadOnlyCollection<SoundscapeGatherNode> GatherNodes { get; }
    internal ReadOnlyCollection<SoundscapeWorkSite> WorkSites { get; }
    internal SoundscapeEncounterPhase Encounter { get; }
    /// <summary>An open conversation or reading hold; speech keeps priority over music and ambience.</summary>
    internal bool IsConversing { get; }

    /// <summary>Projects the listener's presented view without reading any state that view does not expose.</summary>
    internal static SoundscapeObservation From(LocalWorldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var sites = new List<SoundscapeWorkSite>();
        sites.AddRange(view.Construction.Select(project => new SoundscapeWorkSite(project.ProjectId,
            SoundscapeWorkKind.Construction, project.SiteFeet, project.CompletedWork)));
        if (view.Worksite is { } worksite)
        {
            sites.AddRange(worksite.ExcavationTargets.Select(target => new SoundscapeWorkSite(target.SiteId,
                SoundscapeWorkKind.Excavation, CellCenter(target.Cell), target.CompletedWork)));
        }
        return new SoundscapeObservation(
            new SoundscapeListener(view.WorldId, view.BranchId, view.PlayerActorId),
            view.Actors.Select(actor => new SoundscapeActor(actor.ActorId, actor.Feet, actor.PresenceRevision,
                actor.IsIncapacitated)),
            view.GatheringNodes.Select(node => new SoundscapeGatherNode(node.NodeId, node.Kind, node.Feet, node.Revision,
                node.RemainingSwings)),
            sites,
            EncounterPhaseOf(view),
            view.Dialogue is not null || view.HasReadingHold);
    }

    private static SoundscapeEncounterPhase EncounterPhaseOf(LocalWorldView view)
    {
        if (view.Encounter is { } encounter)
            return encounter.IsOutcome ? SoundscapeEncounterPhase.Concluded : SoundscapeEncounterPhase.Engaged;
        return view.EncounterLifecycle?.Phase switch
        {
            EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging or EncounterLifecyclePhase.Active
                => SoundscapeEncounterPhase.Engaged,
            EncounterLifecyclePhase.Outcome => SoundscapeEncounterPhase.Concluded,
            _ => SoundscapeEncounterPhase.None,
        };
    }

    /// <summary>The center of an excavated cell: the place a tool strikes, not where its user stands.</summary>
    private static SubunitPosition CellCenter(CellCoordinate cell)
    {
        const long half = SubunitPosition.SubunitsPerCell / 2;
        return new SubunitPosition(
            checked(cell.X * SubunitPosition.SubunitsPerCell + half),
            checked(cell.Y * SubunitPosition.SubunitsPerCell + half),
            checked(cell.Z * SubunitPosition.SubunitsPerCell));
    }

    private static void RequireDistinct<T>(IEnumerable<T> keys, string parameter)
    {
        var seen = new HashSet<T>();
        foreach (T key in keys)
        {
            if (!seen.Add(key)) throw new ArgumentException("A presented view cannot list one source twice.", parameter);
        }
    }
}
