using OpenFantasy.Core.Quests;

namespace OpenFantasy.Client;

/// <summary>One open wheel: stable cardinal slots and target identities, refreshed permitted previews.</summary>
internal sealed class FarmQuickWheelModel
{
    private static readonly FarmSemanticAction[] Order =
        [FarmSemanticAction.Speak, FarmSemanticAction.Gather, FarmSemanticAction.Craft, FarmSemanticAction.Give];
    private readonly FarmWheelSlot?[] _slots = new FarmWheelSlot?[4];
    private readonly IReadOnlyList<FarmWheelSlot?> _view;

    internal IReadOnlyList<FarmWheelSlot?> Slots => _view;
    internal int? SelectedIndex { get; private set; }
    internal FarmWheelSlot? Selected => SelectedIndex is { } index ? _slots[index] : null;

    internal FarmQuickWheelModel(IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        _view = Array.AsReadOnly(_slots);
        FarmSemanticAction[] order = offers.ContainsKey(FarmSemanticAction.Excavate)
            ? [FarmSemanticAction.Speak, FarmSemanticAction.Excavate, FarmSemanticAction.Traverse, FarmSemanticAction.Give]
            : Order;
        for (int index = 0; index < order.Length; index++)
        {
            if (offers.TryGetValue(order[index], out FarmActionOffer? offer))
            {
                _slots[index] = new FarmWheelSlot(offer);
            }
        }
    }

    internal void Select(int index)
    {
        if (index < 0 || index >= _slots.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (_slots[index] is not null) SelectedIndex = index;
    }

    internal void Refresh(Func<FarmTarget?, IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer>> readOffers)
    {
        foreach (FarmWheelSlot? slot in _slots)
        {
            if (slot is null) continue;
            IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers = readOffers(slot.PinnedTarget);
            slot.Refresh(offers.GetValueOrDefault(slot.Action));
        }
    }
}

/// <summary>A known action never becomes a different person, plant, recipe or operation while inspecting it.</summary>
internal sealed class FarmWheelSlot
{
    private readonly FarmActionOffer _original;
    internal FarmSemanticAction Action => _original.Action;
    internal FarmActionOffer Offer { get; private set; }
    internal FarmTarget? PinnedTarget { get; }

    internal FarmWheelSlot(FarmActionOffer offer)
    {
        _original = Offer = offer;
        if (offer.TargetId is { } target)
        {
            PinnedTarget = new FarmTarget(target, offer.TargetName,
                offer.Action switch
                {
                    FarmSemanticAction.Gather => FarmTargetKind.Harvestable,
                    FarmSemanticAction.Excavate or FarmSemanticAction.Traverse => FarmTargetKind.Excavation,
                    _ => FarmTargetKind.Person
                }, null);
        }
    }

    internal void Refresh(FarmActionOffer? candidate)
    {
        FarmActionScope scope = _original.Scope;
        bool same = candidate is not null && candidate.Action == Action && candidate.Label == _original.Label
            && candidate.TargetId == _original.TargetId && candidate.DialogueId == _original.DialogueId
            && candidate.Scope.PrincipalId == scope.PrincipalId && candidate.Scope.SessionId == scope.SessionId
            && candidate.Scope.ActorId == scope.ActorId && candidate.Scope.WorldId == scope.WorldId
            && candidate.Scope.BranchId == scope.BranchId;
        if (Action == FarmSemanticAction.Traverse && (candidate?.Scope.ExpectedActorRevision != scope.ExpectedActorRevision
            || candidate?.Intent != _original.Intent))
            same = false;
        if (_original.Intent is DeliveryQuestIntent quest
            && (candidate?.Intent is not DeliveryQuestIntent currentQuest || currentQuest.QuestId != quest.QuestId
                || currentQuest.QuestVersion != quest.QuestVersion)) same = false;
        Offer = same ? candidate! : new FarmActionOffer(Action, scope, _original.Label, _original.TargetName,
            _original.TargetId, _original.CostText, "", null,
            "This choice changed or is no longer visible. Close the wheel and choose again.");
    }
}
