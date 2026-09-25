using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

internal enum FarmTargetKind { Person, Harvestable, Excavation, Encounter }

/// <summary>A known selection. An absent position means it is no longer in the public view.</summary>
internal sealed record FarmTarget(EntityId Id, string Name, FarmTargetKind Kind, SubunitPosition? Feet);

/// <summary>Client-only target ownership; cycling and clearing never submit a world command.</summary>
internal sealed class FarmTargetSelection(EntityId speakerId)
{
    private (EntityId World, EntityId Branch, EntityId Actor)? _scope;
    private IReadOnlyList<FarmTarget> _candidates = Array.Empty<FarmTarget>();

    internal FarmTarget? Selected { get; private set; }
    internal bool CanCycle => _candidates.Count > 0;

    internal void Refresh(LocalWorldView view)
    {
        var scope = (view.WorldId, view.BranchId, view.PlayerActorId);
        if (_scope != scope)
        {
            Selected = null;
            _scope = scope;
        }
        // Stable identity order does not jump around when people move or distances change.
        IEnumerable<FarmTarget> encounterTargets = EncounterTargets(view);
        _candidates = (view.IsInEncounter ? encounterTargets : view.Actors.Where(actor => actor.ActorId == speakerId && actor.ActorId != view.PlayerActorId)
            .Select(actor => new FarmTarget(actor.ActorId, actor.DisplayName, FarmTargetKind.Person, actor.Feet))
            .Concat(view.GatheringNodes.Select(node =>
                new FarmTarget(node.NodeId, node.DisplayName, FarmTargetKind.Harvestable, node.Feet)))
            .Concat(view.Worksite?.ExcavationTargets.Select(site => new FarmTarget(site.SiteId,
                site.ReachMode == ExcavationReachMode.RaisedBankTop
                    ? site.IsComplete ? "Opening" : "Bank face" : "Worksite face", FarmTargetKind.Excavation,
                site.Stance)) ?? []).Concat(encounterTargets))
            .OrderBy(target => target.Kind).ThenBy(target => target.Id).ToArray();
        if (Selected is { } selected)
        {
            Selected = _candidates.FirstOrDefault(target => target.Id == selected.Id)
                ?? selected with { Feet = null };
        }
    }

    internal void Cycle()
    {
        if (_candidates.Count == 0)
        {
            return;
        }
        int index = -1;
        for (int candidate = 0; candidate < _candidates.Count; candidate++)
        {
            if (_candidates[candidate].Id == Selected?.Id)
            {
                index = candidate;
                break;
            }
        }
        Selected = _candidates[(index + 1) % _candidates.Count];
    }

    internal void Clear() => Selected = null;

    private static IEnumerable<FarmTarget> EncounterTargets(LocalWorldView view)
    {
        ushort? playerSide = view.EncounterActors.FirstOrDefault(actor => actor.ActorId == view.PlayerActorId)?.Side;
        IEnumerable<EntityId> targets = view.IsInEncounter
            ? view.EncounterActors.Where(actor => actor.Side != 0 && actor.Side != playerSide).Select(actor => actor.ActorId)
            : view.EncounterLifecycle?.Entries.Select(entry => entry.TargetActorId) ?? [];
        return targets.Distinct().Select(identity => view.Actors.FirstOrDefault(actor => actor.ActorId == identity))
            .Where(actor => actor is not null).Select(actor => new FarmTarget(actor!.ActorId,
                actor.DisplayName, FarmTargetKind.Encounter, actor.Feet));
    }
}
