using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// A declared fresh calibration of an unplayed genesis, pinned in its admission identity: named actors start wounded
/// (current HP lowered, never raised, never to zero) and named host residents are bound as human-controlled. It is a
/// fixture parameter for qualification, never lived history, production balance or a transition of a lived save.
/// </summary>
public sealed record EncounterOutcomeCalibration(IReadOnlyDictionary<EntityId, int> WoundedHp,
    IReadOnlyCollection<EntityId> HumanControlled);

/// <summary>
/// Explicit fresh selection of the encounter-outcome interpretation (profile 35): an unplayed retreat-capable world
/// (profile 34) plus the pinned completion law. It never upgrades a lived world, never reinterprets a profile-34 root and
/// grants no position, health, time, reward or material. Structural validation authenticates no history.
/// </summary>
public static class WorldEncounterOutcomeAdmission
{
    public const ushort ProfileVersion = 35;

    public static WorldSnapshot CreateFresh(WorldSnapshot retreatGenesis)
    {
        RequireUnplayed(retreatGenesis);
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.encounter-outcome-admission.v1",
            retreatGenesis.ContentHash.ToByteArray(), EncounterOutcomeState.Pin.ToByteArray()));
        return Compose(retreatGenesis, retreatGenesis.Residents, retreatGenesis.Progression!, admission);
    }

    /// <summary>
    /// The same fresh admission over a declared calibration. The calibration's exact rows are framed into the admission
    /// identity, so a calibrated world can never be mistaken for, or opened as, the uncalibrated selection.
    /// </summary>
    public static WorldSnapshot CreateCalibratedFresh(WorldSnapshot retreatGenesis, EncounterOutcomeCalibration calibration)
    {
        RequireUnplayed(retreatGenesis);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(calibration.WoundedHp);
        ArgumentNullException.ThrowIfNull(calibration.HumanControlled);
        ProgressionSnapshot basis = retreatGenesis.Progression!;
        EntityId[] humans = [.. calibration.HumanControlled.Distinct().Order()];
        KeyValuePair<EntityId, int>[] wounds = [.. calibration.WoundedHp.OrderBy(row => row.Key)];
        if (wounds.Length + humans.Length == 0 || humans.Length != calibration.HumanControlled.Count
            || wounds.Any(row => row.Value < 1 || basis.Actors.All(actor => actor.ActorId != row.Key)
                || row.Value > basis.GetActor(row.Key).CurrentHp)
            || humans.Any(actor => retreatGenesis.Residents.All(resident => resident.ActorId != actor)
                || retreatGenesis.GetResident(actor).ControllerKind != ResidentControllerKind.Host))
        {
            throw new ArgumentException("A calibration only wounds existing actors (1..current HP) or binds host residents to humans.", nameof(calibration));
        }
        var progression = new ProgressionSnapshot(basis.WorldId, basis.BranchId, basis.Revision, basis.Profiles, basis.Curves,
            basis.Actors.Select(actor => calibration.WoundedHp.TryGetValue(actor.ActorId, out int hp)
                ? new ActorProgressionState(actor.ActorId, actor.Revision, actor.Profile, actor.Curve, actor.Level, actor.Experience, hp, actor.CurrentMp)
                : actor), basis.Currency);
        var residents = retreatGenesis.Residents.Select(resident => humans.Contains(resident.ActorId)
            ? new WorldResident(resident.ActorId, resident.InventoryCustodianId, resident.DisplayName, ResidentControllerKind.Player,
                resident.HomeFeet, resident.WanderRadiusCells, resident.Comprehension, resident.ExperiencedTime, resident.OpeningUtterance)
            : resident).ToArray();
        byte[] rows = CanonicalEncodingV1.Frame("world.encounter-outcome-calibration.v1",
            wounds.SelectMany(row => row.Key.ToByteArray().Concat(CanonicalEncodingV1.Unsigned64((ulong)row.Value))).ToArray(),
            humans.SelectMany(actor => actor.ToByteArray()).ToArray());
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.encounter-outcome-calibrated-admission.v1",
            retreatGenesis.ContentHash.ToByteArray(), EncounterOutcomeState.Pin.ToByteArray(), rows));
        return Compose(retreatGenesis, residents, progression, admission);
    }

    private static void RequireUnplayed(WorldSnapshot retreatGenesis)
    {
        ArgumentNullException.ThrowIfNull(retreatGenesis);
        if (retreatGenesis.ProfileVersion != WorldEncounterRetreatAdmission.ProfileVersion || retreatGenesis.Revision != 0
            || retreatGenesis.AdvancingSteps != 0 || retreatGenesis.HasReadingHold
            || retreatGenesis.Retreat is not { AcceptedCount: 0, Active: null, Last: null }
            || retreatGenesis.Coordination is not { Anchor: null, PaidExchange: { } paid, Proposals: { } proposals, Markets: not null } coordination
            || paid.Open.Count != 0 || paid.Outcomes.Count != 0 || proposals.Open.Count != 0 || proposals.Outcomes.Count != 0
            || coordination.ConsentedMaterial!.Transfers.Count != 0 || retreatGenesis.Care?.Pending is not null
            || retreatGenesis.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration, EncounterCount: 0 })
        {
            throw new ArgumentException("Outcome completion is admitted only into an unplayed fresh retreat-capable genesis.", nameof(retreatGenesis));
        }
    }

    private static WorldSnapshot Compose(WorldSnapshot basis, IEnumerable<WorldResident> residents, ProgressionSnapshot progression,
        ContentHash admission)
    {
        var coordination = basis.Coordination!;
        var world = new WorldSnapshot(basis.Inventory, basis.Space, residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, progression),
            coordination: new WorldCoordinatedContinuation(coordination.Sources,
                consentedMaterial: new WorldConsentedMaterialHistory([]), proposals: new WorldMaterialProposalBook([], []),
                paidExchange: WorldPaidExchangeBook.Empty, markets: coordination.Markets),
            retreat: basis.Retreat, encounterOutcome: EncounterOutcomeState.Fresh());
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    /// <summary>
    /// Structural admission of the outcome owner within its world (it authenticates no history; replay of the journal
    /// does). The owner belongs only to the retreat-capable local-market family. Readings exist only while an Outcome
    /// awaits completion and name its exact episode, final archive and a required human; a finalized episode is never
    /// the current one again. When the lifecycle still retains the record's own episode archive (nothing has replaced
    /// it since completion), the record names that archive's exact final hash: this cross-owner relation is checkable
    /// from the root alone. After a later entry or retreat replaces the archive, only replay can authenticate the record.
    /// </summary>
    internal static void Validate(WorldSnapshot world)
    {
        if (world.EncounterOutcome is not { } outcome) return;
        if (world.Retreat is null || world.Coordination?.Markets is null || world.EncounterLifecycle is not { } lifecycle)
        {
            throw new ArgumentException("Outcome completion requires the complete retreat-capable local-market world.");
        }
        if (outcome.Last is { } last && (last.WorldSequence > world.Revision || outcome.FinalizedCount > lifecycle.EncounterCount
            || last.AcknowledgedActorIds.Any(actor => world.Residents.All(resident => resident.ActorId != actor))
            || lifecycle.HasEncounter && lifecycle.Actions!.Scope.EncounterId == last.EncounterId))
        {
            throw new ArgumentException("A finalization record names residents within committed time and is never resumed.");
        }
        if (outcome.Last is { } retained && !lifecycle.HasEncounter && lifecycle.Actions is { } retainedArchive
            && retainedArchive.Scope.EncounterId == retained.EncounterId && retainedArchive.ContentHash != retained.ActionHash)
        {
            throw new ArgumentException("A finalization record names the exact final archive of its retained episode.");
        }
        if (outcome.Acknowledgments.Count == 0) return;
        if (lifecycle.Phase != EncounterLifecyclePhase.Outcome || lifecycle.Actions is not { } actions)
        {
            throw new ArgumentException("Outcome readings exist only while an outcome awaits completion.");
        }
        IReadOnlyList<EntityId> required = WorldEncounterOutcomes.RequiredHumans(world, actions);
        ContentHash archive = actions.ContentHash;
        if (outcome.Acknowledgments.Any(row => row.EncounterId != actions.Scope.EncounterId || row.ActionHash != archive
            || !required.Contains(row.ActorId) || row.WorldSequence > world.Revision))
        {
            throw new ArgumentException("An outcome reading names the exact current outcome and one of its required humans.");
        }
    }
}
