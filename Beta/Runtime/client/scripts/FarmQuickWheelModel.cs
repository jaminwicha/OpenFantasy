using OpenFantasy.Core.Quests;

namespace OpenFantasy.Client;

/// <summary>One open wheel: stable cardinal slots and target identities, refreshed permitted previews.</summary>
internal sealed class FarmQuickWheelModel
{
    private static readonly FarmSemanticAction[] Order =
        [FarmSemanticAction.Speak, FarmSemanticAction.Gather, FarmSemanticAction.Craft, FarmSemanticAction.Give];
    private readonly List<IReadOnlyList<FarmWheelSlot?>> _pages = [];
    private readonly List<string> _pageNames = [];
    private int _pageIndex;

    internal IReadOnlyList<FarmWheelSlot?> Slots => _pages[_pageIndex];
    internal int PageCount => _pages.Count;
    internal string PageName => _pageNames[_pageIndex];
    internal string NextPageName => _pageNames[(_pageIndex + 1) % PageCount];
    internal int? SelectedIndex { get; private set; }
    internal FarmWheelSlot? Selected => SelectedIndex is { } index ? Slots[index] : null;

    internal FarmQuickWheelModel(IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        FarmSemanticAction[] order = offers.Keys.Any(IsEncounterAction)
            ? [FarmSemanticAction.EnterEncounter, FarmSemanticAction.Strike, FarmSemanticAction.Wait,
                offers.ContainsKey(FarmSemanticAction.AcknowledgeCare) ? FarmSemanticAction.AcknowledgeCare : FarmSemanticAction.ReleaseEncounter]
            : offers.ContainsKey(FarmSemanticAction.Excavate)
            ? [FarmSemanticAction.Speak, FarmSemanticAction.Excavate, FarmSemanticAction.Traverse, FarmSemanticAction.Give]
            : Order;
        AddPage("Actions", order, offers);
        if (offers.ContainsKey(FarmSemanticAction.UseRestorative) || offers.ContainsKey(FarmSemanticAction.RevokeCarePermission))
        {
            AddPage("Care", [FarmSemanticAction.UseRestorative, FarmSemanticAction.RevokeCarePermission,
                FarmSemanticAction.None, FarmSemanticAction.None], offers);
        }
        if (offers.Keys.Any(MaterialProposalActionOffers.IsGiftAction))
        {
            AddPage("Gifts", [FarmSemanticAction.ProposeMaterial, FarmSemanticAction.AcceptMaterial,
                FarmSemanticAction.DeclineMaterial, FarmSemanticAction.WithdrawMaterial], offers);
        }
    }

    private void AddPage(string name, FarmSemanticAction[] order, IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        FarmWheelSlot?[] slots = new FarmWheelSlot?[4];
        for (int index = 0; index < order.Length; index++)
        {
            if (offers.TryGetValue(order[index], out var offer)) slots[index] = new FarmWheelSlot(offer);
        }
        _pages.Add(Array.AsReadOnly(slots));
        _pageNames.Add(name);
    }

    internal void NextPage()
    {
        if (PageCount == 1) return;
        _pageIndex = (_pageIndex + 1) % PageCount;
        SelectedIndex = null;
    }

    internal static bool IsEncounterAction(FarmSemanticAction action) => action is FarmSemanticAction.EnterEncounter
        or FarmSemanticAction.Strike or FarmSemanticAction.Wait or FarmSemanticAction.ReleaseEncounter
        or FarmSemanticAction.AcknowledgeCare;

    internal void Select(int index)
    {
        if (index < 0 || index >= Slots.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (Slots[index] is not null) SelectedIndex = index;
    }

    internal void Refresh(Func<FarmTarget?, IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer>> readOffers)
    {
        foreach (FarmWheelSlot? slot in _pages.SelectMany(page => page))
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
                    FarmSemanticAction.EnterEncounter or FarmSemanticAction.Strike => FarmTargetKind.Encounter,
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
        if (FarmQuickWheelModel.IsEncounterAction(Action) && (candidate?.Scope.ExpectedActorRevision != scope.ExpectedActorRevision
            || candidate?.Intent != _original.Intent)) same = false;
        if (Action is FarmSemanticAction.UseRestorative or FarmSemanticAction.RevokeCarePermission
            && (candidate?.Scope.ExpectedActorRevision != scope.ExpectedActorRevision || candidate?.Intent != _original.Intent))
            same = false;
        if (MaterialProposalActionOffers.IsGiftAction(Action)
            && (candidate?.Scope.ExpectedActorRevision != scope.ExpectedActorRevision || candidate?.Intent != _original.Intent))
            same = false;
        if (_original.Intent is DeliveryQuestIntent quest
            && (candidate?.Intent is not DeliveryQuestIntent currentQuest || currentQuest.QuestId != quest.QuestId
                || currentQuest.QuestVersion != quest.QuestVersion)) same = false;
        Offer = same ? candidate! : new FarmActionOffer(Action, scope, _original.Label, _original.TargetName,
            _original.TargetId, _original.CostText, "", null,
            "This choice changed or is no longer visible. Close the wheel and choose again.", detailText: _original.DetailText);
    }
}
