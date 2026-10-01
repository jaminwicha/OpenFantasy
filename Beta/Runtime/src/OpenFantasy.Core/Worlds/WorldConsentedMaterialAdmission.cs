using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit fresh selection and structural admission of complete-world consent evidence.</summary>
public static class WorldConsentedMaterialAdmission
{
    public static WorldSnapshot CreateFresh(WorldSnapshot ordinaryCareGenesis)
    {
        // The original fresh admission checks every unearned counter and all complete-world owners.
        WorldSnapshot basis = WorldCoordinatedAdmission.CreateFresh(ordinaryCareGenesis);
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.consented-material-admission.v1",
            basis.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: new WorldCoordinatedContinuation(basis.Coordination!.Sources,
                consentedMaterial: new WorldConsentedMaterialHistory([])));
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        WorldConsentedMaterialHistory? history = world.Coordination?.ConsentedMaterial;
        if (history is null)
        {
            if (world.Production?.Continuation?.SupportsConsentedWorld == true)
            {
                throw new ArgumentException("Consented production requires its complete world evidence owner.");
            }
            return;
        }
        ulong boundary = world.Coordination!.Anchor?.ClosingWorldSequence ?? 0;
        if (world.Production is null || world.Production.Continuation is { SupportsConsentedWorld: false })
        {
            throw new ArgumentException("Consented world history requires its matching production interpretation.");
        }
        foreach (WorldConsentedTransferEvidence transfer in history.Transfers)
        {
            if (transfer.WorldSequence <= boundary || transfer.WorldSequence > world.Revision
                || !transfer.Approval.IsApprovedByBothParticipants(world.Inventory))
            {
                throw new ArgumentException("Consent evidence must follow its boundary with exact admitted participants and scope.");
            }
            if (transfer.Approval.Command.Terms.LotKind == ConsentedMaterialLotKind.CountedSource)
            {
                var source = world.Inventory.Sources.SingleOrDefault(value => value.SourceId == transfer.RightsOwnerId);
                if (source is null || source.Definition != transfer.Approval.Command.Terms.Definition
                    || transfer.MovedEnd > source.UnitCapacity)
                {
                    throw new ArgumentException("Raw transfer evidence must name its actual admitted source and bounded rights.");
                }
                var node = world.Gathering!.Nodes.FirstOrDefault(value => value.SourceId == source.SourceId);
                if (node is not null && transfer.MovedEnd > node.NextUnissuedUnit)
                {
                    throw new ArgumentException("Raw transfer evidence cannot exceed actual source issuance.");
                }
            }
        }
        if (world.Coordination!.Proposals is not { } proposals)
        {
            world.Production.ValidateCustodyEvidence(history.ProducedMovements);
            return;
        }
        // The saved-proposal interpretation admits settlements only; production replays those.
        WorldMaterialProposalAdmission.Validate(world, history, proposals, boundary);
        if (world.Coordination.PaidExchange is not { } paid)
        {
            world.Production.ValidateCustodyEvidence(proposals.ProducedMovements);
            return;
        }
        // The paid interpretation replays gifts and paid fills together in their authoritative world order.
        WorldPaidExchangeAdmission.Validate(world, paid, boundary);
        world.Production.ValidateCustodyEvidence(WorldPaidExchangeAdmission.OrderedProducedMovements(proposals, paid));
        if (world.Coordination.Markets is { } markets)
        {
            // Market quotes are paid quotes; their records and knowledge are checked witnesses over them.
            WorldMarketAdmission.Validate(world, markets, boundary);
        }
    }
}
