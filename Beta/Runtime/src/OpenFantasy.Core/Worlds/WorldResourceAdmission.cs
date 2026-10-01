using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Recomputes retained resource operations against their named world causes. Authenticating historical
/// controls, spatial readiness and publication still requires replay from trusted host genesis.
/// </summary>
internal static class WorldResourceAdmission
{
    internal static void Validate(WorldSnapshot world)
    {
        if (world.ResourceHistory is not { } retained) return;
        var history = retained.History;
        var progression = world.Progression!;
        var lifecycle = world.EncounterLifecycle!;
        var anchor = retained.ArchiveAnchor;
        var joint = retained.CoordinatedAnchor;
        // Only the paid interpretation has currency-only causes; every other profile keeps its exact equation.
        var paidCauses = WorldPaidExchangeAdmission.CurrencyCauses(world).ToList();
        if (world.ProfileVersion is not (25 or 26 or 27 or 28 or 30 or 32 or 33 or 34) || retained.AnchorAdmission != world.AdmissionVersion
            || anchor is null && joint is null && (history.BasisWorldSequence != 0 || history.Basis.Revision != 0
                || history.Basis.Actors.Any(actor => actor.Revision != 0))
            || progression.Revision != checked(history.Basis.Revision + (ulong)history.Batches.Count + (ulong)paidCauses.Count)
            || history.Basis.Currency.Currency.DefinitionId != progression.Currency.Currency.DefinitionId
            || history.Basis.Currency.Currency.Version != progression.Currency.Currency.Version
            || world.Revision == 0 && history.Basis.ContentHash != progression.ContentHash)
            throw new ArgumentException("Resource history requires its exact admitted progression and world boundary.");
        anchor?.ValidateSuccessor(world);
        history.ValidateCurrent(progression, world.Revision);
        var heads = history.Basis.Actors.ToDictionary(actor => actor.ActorId);
        var currency = history.Basis.Currency;
        var draws = new Dictionary<EntityId, CounterRandomV1>();
        var attacks = new Dictionary<EntityId, (EntityId Encounter, EncounterActionResolution Resolution)>();
        var episodes = new Dictionary<ulong, EntityId>();
        var rewards = new HashSet<EntityId>();
        var treatments = new HashSet<EntityId>();
        var ordinaryUses = new HashSet<EntityId>();
        ulong? previousEpisode = null;
        if (anchor is not null)
        {
            rewards.UnionWith(anchor.Quests.Participations.Where(value => value.Status == ActorQuestStatus.Completed)
                .Select(value => value.CompletionCommandId!.Value));
            treatments.UnionWith(anchor.Care.Treatments.Select(value => value.TreatmentId));
            if (anchor.Care.Ordinary is { } ordinary)
                ordinaryUses.UnionWith(ordinary.Uses.Select(value => value.UseId));
            foreach (var acknowledgment in anchor.Care.Acknowledgments)
                attacks.TryAdd(acknowledgment.Defeat.DecisionCommandId, (acknowledgment.EncounterId, acknowledgment.Defeat));
            if (anchor.Lifecycle.Actions is { } archivedActions)
            {
                previousEpisode = anchor.Lifecycle.EncounterCount - 1;
                episodes.Add(previousEpisode.Value, archivedActions.Scope.EncounterId);
                var draw = BeginDraw(lifecycle, archivedActions.Scope.EncounterId);
                draw.Restore(archivedActions.Random);
                draws.Add(archivedActions.Scope.EncounterId, draw);
                if (archivedActions.LastResolution is { Status: EncounterActionResolutionStatus.Damaged } last)
                    attacks.TryAdd(last.DecisionCommandId, (archivedActions.Scope.EncounterId, last));
            }
        }
        if (joint is not null)
        {
            // The coordinated boundary archived every completed use and pair. Only completed quests,
            // the carried pending defeat and the retained episode seed the following causal segment.
            rewards.UnionWith(joint.Quests.Participations.Where(value => value.Status == ActorQuestStatus.Completed)
                .Select(value => value.CompletionCommandId!.Value));
            if (joint.Care.Pending is { } carried)
                attacks.TryAdd(carried.Defeat.DecisionCommandId, (carried.EncounterId, carried.Defeat));
            if (joint.Lifecycle.Actions is { } closedActions)
            {
                previousEpisode = joint.Lifecycle.EncounterCount - 1;
                episodes.Add(previousEpisode.Value, closedActions.Scope.EncounterId);
                var closedDraw = BeginDraw(lifecycle, closedActions.Scope.EncounterId);
                closedDraw.Restore(closedActions.Random);
                draws.Add(closedActions.Scope.EncounterId, closedDraw);
                if (closedActions.LastResolution is { Status: EncounterActionResolutionStatus.Damaged } closing)
                    attacks.TryAdd(closing.DecisionCommandId, (closedActions.Scope.EncounterId, closing));
            }
        }
        // Actor batches and currency-only paid causes form one ordered causal sequence. Each advances the
        // progression revision once; only paid causes and funded rewards move the finite ledger.
        int paidIndex = 0;
        ulong position = 0;
        for (int index = 0; index <= history.Batches.Count; index++)
        {
            while (paidIndex < paidCauses.Count
                && (index == history.Batches.Count || Precedes(paidCauses[paidIndex], history.Batches[index])))
            {
                currency = ReplayPayment(paidCauses[paidIndex], history.BasisWorldSequence, currency);
                paidIndex++;
                position++;
            }
            if (index == history.Batches.Count) break;
            var batch = history.Batches[index];
            var evidence = retained.Evidence[index];
            IReadOnlyList<ActorResourceChange> expected;
            switch (evidence)
            {
                case WorldEncounterResourceEvidence attack:
                    expected = ValidateAttack(world, batch, attack, heads, draws, episodes, ref previousEpisode);
                    attacks.Add(attack.CommandId, (attack.EncounterId, attack.Resolution));
                    break;
                case WorldRewardResourceEvidence reward:
                    if (!rewards.Add(reward.CommandId)) throw new ArgumentException("An archived quest cannot pay another reward.");
                    expected = ValidateReward(world, batch, reward, heads, checked(history.Basis.Revision + position), ref currency);
                    break;
                case WorldTreatmentResourceEvidence treatment:
                    expected = ValidateTreatment(world, batch, treatment, heads);
                    if (!treatments.Add(treatment.TreatmentId)) throw new ArgumentException("An archived treatment cannot restore resources again.");
                    break;
                case WorldRestorativeUseResourceEvidence use:
                    expected = ValidateRestorativeUse(world, batch, use, heads);
                    if (!ordinaryUses.Add(use.UseId)) throw new ArgumentException("An archived ordinary use cannot restore resources again.");
                    break;
                default:
                    throw new ArgumentException("Unsupported world resource cause.");
            }
            if (batch.Changes.Count != expected.Count || expected.Where((change, ordinal) =>
                !Same(change, batch.Changes[ordinal])).Any())
                throw new ArgumentException("Resource effects must equal the complete ordered effects of their actual domain cause.");
            foreach (var change in expected) heads[change.After.ActorId] = change.After;
            position++;
        }
        if (!rewards.SetEquals(world.Quests!.Participations.Where(value => value.Status == ActorQuestStatus.Completed)
                .Select(value => value.CompletionCommandId!.Value))
            || !treatments.SetEquals(world.Care!.Treatments.Select(value => value.TreatmentId))
            || !ordinaryUses.SetEquals(world.Care.Ordinary?.Uses.Select(value => value.UseId) ?? []))
            throw new ArgumentException("Every completed quest, treatment and ordinary use must retain exactly one resource batch.");

        if (!ProgressionCodecV1.EncodeLedger(currency).AsSpan().SequenceEqual(ProgressionCodecV1.EncodeLedger(progression.Currency)))
            throw new ArgumentException("Resource rewards must preserve their exact funded currency payments and account revisions.");

        // The permanent lifecycle also retains its released last episode. Keep its draw state and
        // original attack truth even when later healing or a level reward changes current resources.
        if (lifecycle.Actions is { } current)
        {
            var family = EncounterEpisodeIdentity.Family(lifecycle, lifecycle.ScenarioId!.Value);
            if (current.Scope.EncounterId.High != family.High
                || (current.Scope.EncounterId.Low ^ family.Low) != lifecycle.EncounterCount - 1)
                throw new ArgumentException("The retained current episode must use its authority-derived scenario and ordinal.");
            if (episodes.TryGetValue(lifecycle.EncounterCount - 1, out var historicalEpisode)
                && historicalEpisode != current.Scope.EncounterId)
                throw new ArgumentException("The retained current episode must match the historical owner of its ordinal.");
            var draw = draws.GetValueOrDefault(current.Scope.EncounterId) ?? BeginDraw(lifecycle, current.Scope.EncounterId);
            if (draw.CaptureState() != current.Random)
                throw new ArgumentException("Encounter randomness must retain exactly the recorded successful attacks.");
            if (current.LastResolution is { Status: EncounterActionResolutionStatus.Damaged } last)
                RequireAttack(attacks, current.Scope.EncounterId, last);
        }
        foreach (var acknowledgment in world.Care!.Acknowledgments)
            RequireAttack(attacks, acknowledgment.EncounterId, acknowledgment.Defeat);
    }

    private static IReadOnlyList<ActorResourceChange> ValidateAttack(WorldSnapshot world, ResourceChangeBatch batch,
        WorldEncounterResourceEvidence evidence, Dictionary<EntityId, ActorProgressionState> heads,
        Dictionary<EntityId, CounterRandomV1> draws, Dictionary<ulong, EntityId> episodes, ref ulong? previousEpisode)
    {
        var lifecycle = world.EncounterLifecycle!;
        var scenario = lifecycle.GetScenario(evidence.ScenarioId);
        var family = EncounterEpisodeIdentity.Family(lifecycle, scenario.DefinitionId);
        ulong episode = family.Low ^ evidence.EncounterId.Low;
        var request = evidence.Request;
        var intent = (QueueEncounterAbilityIntent)request.Intent;
        var result = evidence.Resolution;
        var attacker = scenario.Combatants.Single(value => value.ActorId == request.ActorId);
        var target = scenario.Combatants.Single(value => value.ActorId == intent.TargetActorId);
        if (request.WorldId != world.WorldId || request.BranchId != world.BranchId
            || !world.Inventory.CanControl(new TrustedCommandDelivery(request.PrincipalId, request.OriginSessionId), request.ActorId)
            || family.High != evidence.EncounterId.High || episode >= lifecycle.EncounterCount
            || previousEpisode is { } previous && episode < previous
            || episodes.TryGetValue(episode, out var entered) && entered != evidence.EncounterId
            || batch.CommandOrdinal != 0 || batch.WorldSequence != result.WorldSequence
            || intent.ExpectedTimingRevision >= result.TimingRevision
            || request.ExpectedActorRevision > world.Space.GetActor(request.ActorId).Revision
            || attacker.Side == 0 || target.Side == 0 || attacker.Side == target.Side
            || !attacker.AbilityIds.Contains(intent.AbilityDefinitionId))
            throw new ArgumentException("An attack requires its actual scope, entered episode, command position and opposing ability grant.");
        previousEpisode = episode;
        episodes[episode] = evidence.EncounterId;
        var ability = lifecycle.Abilities.Single(value => value.DefinitionId == intent.AbilityDefinitionId);
        var source = RequireActor(heads, attacker.ActorId);
        var recipient = RequireActor(heads, target.ActorId);
        if (source.CurrentHp <= 0 || recipient.CurrentHp <= 0 || source.Level < ability.MinimumLevel
            || result.Delay != ability.Delay(checked((uint)source.Stats.Speed * 10)))
            throw new ArgumentException("A successful historical attack requires capable living participants at its own boundary.");
        if (!draws.TryGetValue(evidence.EncounterId, out var draw))
        {
            draw = BeginDraw(lifecycle, evidence.EncounterId);
            draws.Add(evidence.EncounterId, draw);
        }
        uint variation = checked(900U + (uint)draw.NextUInt64(201));
        var damage = recipient.TakeDamage(EncounterActionRules.ComputePhysicalDamage(source.Stats.Attack,
            recipient.Stats.Defense, ability.PowerTenths, variation));
        if (result.VariationPermille != variation || result.ManaSpent != ability.ManaCost
            || result.TargetHpBefore != damage.Before.CurrentHp || result.TargetHpAfter != damage.Candidate.CurrentHp
            || result.Damage != -damage.Health.Delta)
            throw new ArgumentException("Attack evidence must preserve the exact seeded draw, computed damage and complete magic cost.");
        var cause = new ResourceChangeCause(request.CommandId, ability.DefinitionId, ability.Version);
        List<ActorResourceChange> changes = [ActorResourceChange.FromTransition(0, cause, damage)];
        if (ability.ManaCost != 0) changes.Add(ActorResourceChange.FromTransition(1, cause, source.SpendMagic(ability.ManaCost)));
        return changes;
    }

    private static IReadOnlyList<ActorResourceChange> ValidateReward(WorldSnapshot world, ResourceChangeBatch batch,
        WorldRewardResourceEvidence evidence, Dictionary<EntityId, ActorProgressionState> heads, ulong revision, ref CurrencyLedger currency)
    {
        var participation = world.Quests!.Participations.SingleOrDefault(value => value.CompletionCommandId == evidence.CommandId)
            ?? throw new ArgumentException("A resource reward requires its actual completed quest.");
        var quest = world.Quests.FindDefinition(participation.QuestId)!;
        var actor = RequireActor(heads, participation.ActorId);
        if (batch.CommandOrdinal >= WorldStepBatch.MaximumActorsPerBatch || actor.Level < quest.MinimumLevel)
            throw new ArgumentException("The reward requires an admitted command position and earned quest level.");
        var replay = new ProgressionSnapshot(world.WorldId, world.BranchId, revision,
            world.ResourceHistory!.History.Basis.Profiles, world.ResourceHistory.History.Basis.Curves, heads.Values, currency);
        var reward = ActorRewardPlanner.Plan(replay, actor.ActorId, actor.Revision, quest.Reward);
        if (!reward.Succeeded) throw new ArgumentException("The retained quest reward must be funded by its actual prior reserve.");
        currency = reward.Candidate.Currency;
        return [ActorResourceChange.FromTransition(0,
            new(evidence.CommandId, quest.Reward.DefinitionId, quest.Reward.Version), reward.ActorTransition!)];
    }

    private static IReadOnlyList<ActorResourceChange> ValidateTreatment(WorldSnapshot world, ResourceChangeBatch batch,
        WorldTreatmentResourceEvidence evidence, Dictionary<EntityId, ActorProgressionState> heads)
    {
        var care = world.Care!;
        var treatment = care.Treatments.SingleOrDefault(value => value.TreatmentId == evidence.TreatmentId)
            ?? throw new ArgumentException("A restoration requires its actual retained treatment.");
        var acknowledgment = care.GetAcknowledgment(treatment.AcknowledgmentId);
        var agreement = care.GetAgreement(acknowledgment.AgreementId);
        var remedy = care.GetRemedy(agreement.RemedyDefinitionId);
        var patient = RequireActor(heads, agreement.PatientActorId);
        var helper = RequireActor(heads, agreement.HelperActorId);
        var transition = patient.RestoreHealth(remedy.RestoredHp);
        if (batch.CommandOrdinal != 0 || treatment.CommandId != evidence.CommandId
            || treatment.WorldSequence != batch.WorldSequence || patient.CurrentHp != 0
            || treatment.HelperNormalizedSpeed != checked((uint)helper.Stats.Speed * 10)
            || patient.Revision != treatment.PatientRevisionBefore || patient.Stats.MaxHp != treatment.MaximumHp
            || transition.Candidate.CurrentHp != treatment.HpAfter)
            throw new ArgumentException("Treatment must retain its exact incapacitated patient, capped remedy and resource boundary.");
        return [ActorResourceChange.FromTransition(0, new(treatment.TreatmentId, remedy.DefinitionId, remedy.Version), transition)];
    }

    private static IReadOnlyList<ActorResourceChange> ValidateRestorativeUse(WorldSnapshot world, ResourceChangeBatch batch,
        WorldRestorativeUseResourceEvidence evidence, Dictionary<EntityId, ActorProgressionState> heads)
    {
        if (world.ProfileVersion is not (26 or 27 or 28 or 30 or 32 or 33 or 34) || world.Care?.Ordinary is not { } ordinary)
            throw new ArgumentException("Ordinary restoration evidence requires its explicit ordinary-care world profile.");
        var use = ordinary.Uses.SingleOrDefault(value => value.UseId == evidence.UseId)
            ?? throw new ArgumentException("Ordinary restoration requires its actual retained completed use.");
        var request = evidence.Request;
        var intent = (UseRestorativeItemIntent)request.Intent;
        var definition = ordinary.GetDefinition(use.UseDefinitionId);
        var remedy = world.Care.GetRemedy(definition.RemedyDefinitionId);
        var patient = RequireActor(heads, use.RecipientActorId);
        var source = RequireActor(heads, use.ActorId);
        if (request.WorldId != world.WorldId || request.BranchId != world.BranchId
            || !world.Inventory.CanControl(new TrustedCommandDelivery(request.PrincipalId, request.OriginSessionId), request.ActorId)
            || use.CommandId != request.CommandId || use.ActorId != request.ActorId
            || use.WorldSequence != batch.WorldSequence || use.CommandOrdinal != batch.CommandOrdinal
            || batch.CommandOrdinal >= WorldStepBatch.MaximumActorsPerBatch
            || use.RecipientActorId != intent.RecipientActorId || use.UseDefinitionId != intent.UseDefinitionId
            || use.UseDefinitionVersion != definition.Version || intent.UseDefinitionVersion != definition.Version
            || definition.RemedyVersion != remedy.Version || use.ExplorationMilliseconds != definition.ExplorationMilliseconds
            || request.ExpectedActorRevision > world.Space.GetActor(request.ActorId).Revision
            || intent.ExpectedPatientPresenceRevision > world.Space.GetActor(use.RecipientActorId).Revision
            || source.CurrentHp <= 0 || patient.CurrentHp <= 0 || patient.CurrentHp >= patient.Stats.MaxHp
            || use.PatientRevisionBefore != patient.Revision || intent.ExpectedPatientProgressionRevision != patient.Revision
            || use.MaximumHp != patient.Stats.MaxHp || use.HpBefore != patient.CurrentHp)
        {
            throw new ArgumentException("Ordinary use requires its exact selected request, ordered command and historical living resource boundary.");
        }
        ValidateOrdinaryPermission(ordinary, definition, use, intent);
        ValidateOrdinaryDose(world, use, intent, remedy);
        var transition = patient.RestoreHealth(remedy.RestoredHp);
        if (use.PatientRevisionAfter != transition.Candidate.Revision || use.HpAfter != transition.Candidate.CurrentHp)
            throw new ArgumentException("An ordinary use must retain the exact capped restoration from its historical patient state.");
        return [ActorResourceChange.FromTransition(0, new(use.UseId, remedy.DefinitionId, remedy.Version), transition)];
    }

    private static void ValidateOrdinaryPermission(OrdinaryCareSnapshot ordinary, RestorativeUseDefinition definition,
        RestorativeUse use, UseRestorativeItemIntent intent)
    {
        if (use.PermissionId != intent.PermissionId || use.PermissionGrantVersion != intent.PermissionGrantVersion
            || use.PermissionRevision != intent.ExpectedPermissionRevision)
            throw new ArgumentException("An ordinary use retains exactly the selected permission witness.");
        if (use.ActorId == use.RecipientActorId)
        {
            if (definition.DeliveryMode != RestorativeDeliveryMode.SelfUse || use.PermissionId is not null
                || use.PermissionGrantVersion is not null || use.PermissionRevision is not null)
                throw new ArgumentException("Self-use requires its self-delivery policy without an invented assistance permission.");
            return;
        }
        if (definition.DeliveryMode != RestorativeDeliveryMode.LivingAssistance || use.PermissionId is not { } permissionId)
            throw new ArgumentException("Living assistance requires its own exact permission and delivery policy.");
        var permission = ordinary.GetPermission(permissionId);
        if (permission.PatientActorId != use.RecipientActorId || permission.HelperActorId != use.ActorId
            || permission.UseDefinitionId != definition.DefinitionId || permission.UseDefinitionVersion != definition.Version
            || permission.GrantVersion != use.PermissionGrantVersion || use.PermissionRevision != 0)
            throw new ArgumentException("Living assistance must retain its original scoped permission grant.");
        if (permission.Revocation is { } revocation
            && (revocation.WorldSequence < use.WorldSequence
                || revocation.WorldSequence == use.WorldSequence && revocation.CommandOrdinal <= use.CommandOrdinal))
        {
            throw new ArgumentException("A completed ordinary use cannot follow or share the command position of its permission revocation.");
        }
    }

    private static void ValidateOrdinaryDose(WorldSnapshot world, RestorativeUse use, UseRestorativeItemIntent intent,
        RestorativeRemedyDefinition remedy)
    {
        var claim = world.Production!.ConsumedItems.SingleOrDefault(value => value.ConsumptionId == use.ConsumptionId)
            ?? throw new ArgumentException("An ordinary use requires its exact retained single-dose consumption.");
        var storage = world.Inventory.Custody.GetStorage(world.GetResident(use.ActorId).InventoryCustodianId);
        var ingredient = intent.Ingredient;
        if (claim.CommandId != use.CommandId || claim.ActorId != use.ActorId || claim.RecipientActorId != use.RecipientActorId
            || claim.CustodianId != storage.CustodianId || intent.ExpectedStorageRevision >= storage.Revision
            || intent.ExpectedProductionRevision >= world.Production.Revision
            || ingredient.Quantity != 1 || claim.Ingredient.Quantity != 1 || claim.Ingredient.Definition != remedy.Item
            || ingredient.Kind != claim.Ingredient.Kind || ingredient.LotId != claim.Ingredient.LotId
            || ingredient.ExpectedRevision != claim.Ingredient.Revision)
        {
            throw new ArgumentException("Ordinary restoration retains the exact selected dose, supplier custody and material revisions.");
        }
    }

    private static ActorProgressionState RequireActor(Dictionary<EntityId, ActorProgressionState> heads, EntityId actorId) =>
        heads.TryGetValue(actorId, out var actor) ? actor
                : throw new ArgumentException("A resource cause requires an actor in its admitted progression basis.");

    private static CounterRandomV1 BeginDraw(EncounterLifecycleSnapshot lifecycle, EntityId encounter) =>
        new(lifecycle.Seed.ToByteArray(), EncounterActionSnapshot.RulesPin.Value, EncounterActionSnapshot.RandomDomain, encounter);

    private static void RequireAttack(Dictionary<EntityId, (EntityId Encounter, EncounterActionResolution Resolution)> attacks,
        EntityId encounter, EncounterActionResolution resolution)
    {
        if (!attacks.TryGetValue(resolution.DecisionCommandId, out var attack) || attack.Encounter != encounter
            || !EncounterActionCodecV1.EncodeResolution(attack.Resolution).AsSpan()
                .SequenceEqual(EncounterActionCodecV1.EncodeResolution(resolution)))
            throw new ArgumentException("The original attack boundary must remain exactly represented in causal history.");
    }

    // Causes are ordered by world revision and command position. One command causes at most one revision.
    private static bool Precedes(WorldPaidCurrencyCause cause, ResourceChangeBatch batch)
    {
        if (cause.WorldSequence == batch.WorldSequence && cause.CommandOrdinal == batch.CommandOrdinal)
            throw new ArgumentException("One world command position cannot cause both a paid and an actor resource revision.");
        return cause.WorldSequence < batch.WorldSequence
            || cause.WorldSequence == batch.WorldSequence && cause.CommandOrdinal < batch.CommandOrdinal;
    }

    // The shared spot arithmetic redistributes the buyer's existing balance; an unfunded fill fails here.
    private static CurrencyLedger ReplayPayment(WorldPaidCurrencyCause cause, ulong basisWorldSequence, CurrencyLedger currency)
    {
        if (cause.WorldSequence <= basisWorldSequence || cause.CommandOrdinal >= WorldStepBatch.MaximumActorsPerBatch
            || currency.Currency.DefinitionId != cause.Quote.CurrencyDefinitionId
            || currency.Currency.Version != cause.Quote.CurrencyVersion)
            throw new ArgumentException("A paid currency cause requires its segment position and exact currency pin.");
        return currency.Transfer(cause.Quote.BuyerAccountId, cause.Quote.PaymentsFor(cause.Acceptance.Quantity));
    }

    private static bool Same(ActorResourceChange expected, ActorResourceChange actual) =>
        expected.Ordinal == actual.Ordinal && expected.Cause == actual.Cause && expected.Operation == actual.Operation
        && expected.RequestedAmount == actual.RequestedAmount && ActorResourceChange.Same(expected.Before, actual.Before)
        && ActorResourceChange.Same(expected.After, actual.After);
}
