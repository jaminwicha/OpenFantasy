using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed record LocalActorView(EntityId ActorId, string DisplayName, SubunitPosition Feet,
    ulong PresenceRevision, ResidentControllerKind ControllerKind);
public sealed record LocalItemLotView(EntityId LotId, EntityId DefinitionId, ulong Revision, ulong Quantity);
public sealed record LocalDialogueView(EntityId DialogueId, EntityId SpeakerActorId, string SpeakerName, string Text);

public sealed class LocalInventoryView
{
    internal LocalInventoryView(EntityId custodianId, ulong revision, IEnumerable<LocalItemLotView> lots)
    {
        CustodianId = custodianId;
        Revision = revision;
        Lots = Array.AsReadOnly(lots.OrderBy(lot => lot.LotId).ToArray());
        Quantity = Lots.Aggregate(0UL, (total, lot) => checked(total + lot.Quantity));
    }

    public EntityId CustodianId { get; }
    public ulong Revision { get; }
    public ulong Quantity { get; }
    public ReadOnlyCollection<LocalItemLotView> Lots { get; }
}

/// <summary>
/// Public projection of the admitted sight-complete local profile. This is not a global visibility
/// system; other actors' inventory, source claims, private commit counters and hashes are excluded.
/// </summary>
public sealed class LocalWorldView
{
    internal LocalWorldView(WorldSnapshot state, EntityId playerActorId)
    {
        WorldId = state.WorldId;
        BranchId = state.BranchId;
        PlayerActorId = playerActorId;
        Calendar = state.Calendar;
        HasReadingHold = state.HasReadingHold;
        Actors = Array.AsReadOnly(state.Residents.Select(resident =>
        {
            var presence = state.Space.GetActor(resident.ActorId);
            return new LocalActorView(resident.ActorId, resident.DisplayName, presence.Feet,
                presence.Revision, resident.ControllerKind);
        }).ToArray());
        EntityId storage = state.GetResident(playerActorId).InventoryCustodianId;
        PlayerInventory = new LocalInventoryView(storage, state.Inventory.Custody.GetStorage(storage).Revision,
            state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == storage)
                .Select(lot => new LocalItemLotView(lot.LotId, lot.Definition.DefinitionId, lot.Revision, lot.Quantity)));
        GatheringNodes = Array.AsReadOnly(state.Gathering is { } gathering
            ? gathering.Nodes.Where(node => node.VisibilityAt(state.AdvancingSteps) != GatherNodeVisibility.Absent)
                .Select(node => new LocalGatherNodeView(node.NodeId, node.Definition.DisplayName, node.Feet,
                    node.Revision, node.Definition.Kind, node.Definition.RequiredToolKind,
                    (uint)node.RemainingSwings, node.VisibilityAt(state.AdvancingSteps)))
                .OrderBy(node => node.NodeId).ToArray()
            : []);
        List<LocalGatherToolLotView> tools = [];
        if (state.Gathering is { } admittedGathering)
        {
            PlayerGatherEffort = (uint)admittedGathering.GetActorWork(playerActorId).Effort;
            foreach (var lot in state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == storage))
            {
                var tool = admittedGathering.Tools.FirstOrDefault(value => value.Definition == lot.Definition);
                if (tool is not null)
                {
                    tools.Add(new LocalGatherToolLotView(lot.LotId, lot.Revision, tool.Kind, tool.Power));
                }
            }
        }
        GatheringTools = Array.AsReadOnly(tools.OrderBy(tool => tool.LotId).ToArray());
        DialogueSession? dialogue = state.Dialogues.FirstOrDefault(value => value.ListenerActorId == playerActorId);
        if (dialogue is not null)
        {
            WorldResident speaker = state.GetResident(dialogue.SpeakerActorId);
            Dialogue = new LocalDialogueView(dialogue.DialogueId, speaker.ActorId, speaker.DisplayName,
                speaker.OpeningUtterance!.Text);
        }
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId PlayerActorId { get; }
    public WorldInstant Calendar { get; }
    public bool HasReadingHold { get; }
    public ReadOnlyCollection<LocalActorView> Actors { get; }
    public LocalInventoryView PlayerInventory { get; }
    public ReadOnlyCollection<LocalGatherNodeView> GatheringNodes { get; }
    public ReadOnlyCollection<LocalGatherToolLotView> GatheringTools { get; }
    public uint? PlayerGatherEffort { get; }
    public LocalDialogueView? Dialogue { get; }
}
