using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>Replays typed material operations against one bounded captured frontier.</summary>
internal sealed class MaterialContinuationReplay
{
    private readonly ProductionSnapshot _state;
    private readonly CountedAuthorityState _expected;
    private readonly ProductionSnapshot _baseline;
    private readonly HashSet<EntityId> _occupied = [];
    private readonly HashSet<EntityId> _commands;
    private readonly Dictionary<EntityId, TransformedLot> _derived;
    private CountedAuthorityState _inventory;

    internal MaterialContinuationReplay(ProductionSnapshot state, CountedAuthorityState inventory)
    {
        _state = state;
        _expected = inventory;
        ProductionContinuationContext context = state.Continuation!;
        _inventory = inventory.WithCustody(new CustodySnapshot(state.WorldId, state.BranchId,
            context.Basis.CustodyRevision, context.Storages, context.Basis.RawLots));
        if (_inventory.ContentHash != context.Basis.InventoryHash)
            throw new ArgumentException("The material replay baseline must reconstruct the exact captured inventory.");
        _baseline = new ProductionSnapshot(state.WorldId, state.BranchId, context.BaseProductionRevision,
            context.Recipes, [], context.Basis.TransformedLots, state.SupportsConsumption ? [] : null,
            context, new MaterialOperationHistory(context, []));
        _derived = context.Basis.TransformedLots.ToDictionary(value => value.LotId);
        _commands = state.MaterialHistory!.Operations.Select(value => value.CommandId).ToHashSet();
    }

    internal void Validate()
    {
        foreach (MaterialOperation operation in _state.MaterialHistory!.Operations)
        {
            if (Occupied(operation.CommandId)) throw new ArgumentException("A material operation cannot reuse a retained identity.");
            switch (operation)
            {
                case MaterialCraftOperation craft: Craft(craft.Batch); break;
                case MaterialConsumptionOperation consume: Consume(consume.Claim); break;
                case MaterialGatherOperation gather: Gather(gather.Evidence); break;
                case MaterialTransferOperation transfer: Transfer(transfer); break;
                case MaterialDeliveryOperation delivery: Deliver(delivery); break;
                default: throw new ArgumentException("Unsupported ordered material operation.");
            }
            _occupied.Add(operation.CommandId);
            ValidateCapacity(_inventory);
        }
        if (_inventory.ContentHash != _expected.ContentHash
            || _derived.Count != _state.Lots.Count
            || _state.Lots.Any(lot => !_derived.TryGetValue(lot.LotId, out TransformedLot? expected)
                || !ProductionCodecV1.EncodeLot(lot).AsSpan().SequenceEqual(ProductionCodecV1.EncodeLot(expected))))
            throw new ArgumentException("Current material owners must exactly equal the ordered operation replay.");
    }

    private bool Occupied(EntityId id) => _occupied.Contains(id) || _inventory.ContainsIdentity(id)
        || _baseline.ContainsIdentity(id) || _derived.ContainsKey(id);

    private void Created(EntityId id)
    {
        if (Occupied(id) || _commands.Contains(id))
            throw new ArgumentException("A material operation cannot reuse a command or created identity.");
        _occupied.Add(id);
    }

    private void Craft(ProductionBatch batch)
    {
        RequireOwner(batch.ActorId, batch.CustodianId, true);
        Created(batch.BatchId);
        foreach (RetiredIngredient ingredient in batch.Ingredients) Spend(batch.CustodianId, ingredient);
        CraftRecipeDefinition recipe = _state.GetRecipe(batch.RecipeId);
        for (int index = 0; index < recipe.Outputs.Count; index++)
        {
            EntityId id = EntityId.DeriveSpawned(batch.CommandId, checked((ulong)index + 1));
            Created(id);
            RecipeTerm output = recipe.Outputs[index];
            _derived.Add(id, new(id, batch.CustodianId, recipe.ItemFor(output),
                new(batch.BatchId, (uint)index, 0, output.Count), 0));
        }
        AdvanceCustody(batch.CustodianId);
    }

    private void Consume(ConsumedItemClaim claim)
    {
        RequireOwner(claim.ActorId, claim.CustodianId, false);
        if (!_inventory.Actors.Contains(claim.RecipientActorId))
            throw new ArgumentException("Consumption requires an admitted actual recipient.");
        Created(claim.ConsumptionId);
        Spend(claim.CustodianId, claim.Ingredient);
        AdvanceCustody(claim.CustodianId);
    }

    private void RequireOwner(EntityId actorId, EntityId custodianId, bool deposit)
    {
        CustodianAccessPolicy? policy = _inventory.Policies.FirstOrDefault(value => value.CustodianId == custodianId);
        if (policy is null || policy.OwnerActorId != actorId || deposit && !policy.AllowsDeposit(actorId))
            throw new ArgumentException("Material retirement requires the actual custodian's admitted actor permission.");
    }

    private void Spend(EntityId custodianId, RetiredIngredient ingredient)
    {
        if (ingredient.Source is { } source)
        {
            CountedSourceLot lot = _inventory.Custody.GetLot(ingredient.LotId);
            if (lot.CustodianId != custodianId || lot.Definition != ingredient.Definition || lot.Revision != ingredient.Revision
                || lot.Protection != LotProtection.None || lot.ReservedBy is not null || lot.Source.SourceId != source.SourceId
                || lot.Source.Start != source.Start || source.End > lot.Source.End)
                throw new ArgumentException("Raw disposition must spend the exact available owned prefix.");
            var raw = _inventory.Custody.Lots.Where(value => value.LotId != lot.LotId).ToList();
            if (source.End != lot.Source.End) raw.Add(new(lot.LotId, lot.CustodianId, lot.Definition,
                new(source.SourceId, source.End, lot.Source.End), checked(lot.Revision + 1), lot.Protection, lot.ReservedBy));
            ReplaceRaw(raw);
        }
        else
        {
            ProductionOutputInterval output = ingredient.Output!;
            if (!_derived.TryGetValue(ingredient.LotId, out TransformedLot? lot)
                || lot.CustodianId != custodianId || lot.Definition != ingredient.Definition || lot.Revision != ingredient.Revision
                || lot.Protection != LotProtection.None || lot.ReservedBy is not null || lot.Output.BatchId != output.BatchId
                || lot.Output.OutputIndex != output.OutputIndex || lot.Output.Start != output.Start || output.End > lot.Output.End)
                throw new ArgumentException("Produced disposition must spend the exact available owned prefix.");
            _derived.Remove(lot.LotId);
            if (output.End != lot.Output.End) _derived.Add(lot.LotId, new(lot.LotId, lot.CustodianId, lot.Definition,
                new(output.BatchId, output.OutputIndex, output.End, lot.Output.End), checked(lot.Revision + 1), lot.Protection, lot.ReservedBy));
        }
    }

    private void Gather(GatherIssueEvidence evidence)
    {
        if (_inventory.ContentHash != evidence.BeforeInventoryHash || evidence.Command.WorldId != _inventory.WorldId
            || evidence.Command.BranchId != _inventory.BranchId
            || !_inventory.CanControl(new(evidence.Command.PrincipalId, evidence.Command.OriginSessionId), evidence.Command.ActorId))
            throw new ArgumentException("Actual gather evidence must match the exact current inventory and controller.");
        CountedSourceLot lot = evidence.IssuedLot;
        Created(lot.LotId);
        ReplaceRaw([.. _inventory.Custody.Lots, lot]);
        AdvanceCustody(lot.CustodianId);
        if (_inventory.ContentHash != evidence.AfterInventoryHash)
            throw new ArgumentException("Gather material must match its genuine issuing plan exactly.");
    }

    private void Transfer(MaterialTransferOperation operation)
    {
        var decision = new CountedTransferAuthority().Decide(_inventory, operation.Command, operation.Delivery,
            new(_inventory.WorldId, operation.Sequence));
        if (decision.Receipt.Status != CommandReceiptStatus.Applied || decision.MovedLotId is not { } moved)
            throw new ArgumentException("The recorded raw transfer must pass its real permission and revision rules.");
        if (moved != operation.Command.Payload.LotId) Created(moved);
        _inventory = decision.Candidate;
    }

    private void Deliver(MaterialDeliveryOperation operation)
    {
        SourceDeliveryPlan result = SourceDeliveryPlanner.Prepare(_inventory, operation.Delivery, operation.ActorId,
            operation.CommandId, operation.Request, id => Occupied(id) || id != operation.CommandId && _commands.Contains(id), ValidateCapacity);
        if (!result.Succeeded) throw new ArgumentException("The recorded source delivery must pass its real reservation and revision rules.");
        foreach (EntityId id in result.CreatedIds) Created(id);
        _inventory = result.Candidate;
    }

    private void ReplaceRaw(IEnumerable<CountedSourceLot> lots) =>
        _inventory = _inventory.WithCustody(new CustodySnapshot(_inventory.WorldId, _inventory.BranchId,
            _inventory.Custody.Revision, _inventory.Custody.Storages, lots));

    private void AdvanceCustody(EntityId custodian) =>
        _inventory = _inventory.WithCustody(new CustodySnapshot(_inventory.WorldId, _inventory.BranchId,
            checked(_inventory.Custody.Revision + 1), _inventory.Custody.Storages.Select(value => value.CustodianId == custodian
                ? value with { Revision = checked(value.Revision + 1) } : value), _inventory.Custody.Lots));

    private void ValidateCapacity(CountedAuthorityState inventory)
    {
        if (inventory.Custody.Lots.Count + _derived.Count > ProductionSnapshot.MaximumLots)
            throw new ArgumentException("Combined live material exceeds its bounded owner.");
        foreach (CustodianStorage storage in inventory.Custody.Storages)
        {
            UInt128 quantity = inventory.Custody.GetStoredQuantity(storage.CustodianId);
            foreach (TransformedLot lot in _derived.Values.Where(value => value.CustodianId == storage.CustodianId)) quantity += lot.Quantity;
            if (quantity > storage.UnitCapacity) throw new ArgumentException("Combined raw and produced contents exceed the actual storage capacity.");
        }
    }
}
