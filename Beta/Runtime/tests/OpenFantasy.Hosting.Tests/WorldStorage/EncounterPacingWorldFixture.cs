using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal sealed class EncounterPacingWorldFixture
{
    private ulong _identity = 10_000;

    internal EncounterPacingWorldFixture(bool active = true)
    {
        var authored = BreachFarmAdmission.CreateEmbedded("encounter-pacing-world", "Traveler");
        // This is an alternate fresh placement, not a live relocation: the same final 1,000-litre
        // source interval starts above the empty downstream pool, with no hole or new material.
        var original = authored.LiquidCells!;
        LiquidCellPortion moved = original.Portions.Single(portion => portion.Start == 5_000);
        LiquidCellKey receiver = original.DomainCells.Single(cell => cell.Coordinate == new CellCoordinate(117, 329, 0));
        var cells = LiquidCellState.Create(authored.Reservoirs!, original.Profile, original.Boundary,
            original.Portions.Select(portion => portion == moved
                ? new LiquidCellPortion(receiver, portion.Origin, portion.OriginId, portion.Start, portion.End) : portion),
            original.Topology);
        var gathering = new GatheringSnapshot(authored.WorldId, authored.BranchId, 0, authored.Gathering!.Nodes,
            authored.Gathering.Tools, authored.Gathering.Actors.Select(actor => new GatherActorWork(actor.ActorId, 970)));
        Basis = new WorldSnapshot(authored.Inventory, authored.Space, authored.Residents, authored.AdmissionVersion,
            authored.Calendar, 0, 0, gathering: gathering, production: authored.Production,
            progression: authored.Progression, quests: authored.Quests, construction: authored.Construction,
            reservoirs: authored.Reservoirs, excavation: authored.Excavation, reservoirSeals: authored.ReservoirSeals,
            liquidCells: cells);
        Genesis = WorldPacingAdmission.CreateFresh(Basis, active ? Id(1) : null);
        Player = Genesis.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        var binding = Genesis.Inventory.Controls.Single(control => control.ActorId == Player);
        Delivery = new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
    }

    internal WorldSnapshot Basis { get; }
    internal WorldSnapshot Genesis { get; }
    internal EntityId Player { get; }
    internal TrustedCommandDelivery Delivery { get; }
    internal static EntityId Id(ulong value) => new(0xee12200000000000, value);
    internal EntityId NextId() => Id(_identity++);

    internal WorldActionCommand Wait(WorldSnapshot state, EntityId actor, ulong? timingRevision = null)
    {
        var timing = state.Pacing!.Encounter!;
        var binding = state.Inventory.Controls.Single(control => control.ActorId == actor);
        return new WorldActionCommand(NextId(), binding.PrincipalId, binding.SessionId, actor, state.WorldId,
            state.BranchId, state.Space.GetActor(actor).Revision,
            new QueueEncounterWaitIntent(timing.EncounterId, timingRevision ?? timing.Revision));
    }

    internal WorldStepBatch Batch(WorldSnapshot state, WorldBatchKind kind,
        IEnumerable<WorldActionCommand>? actions = null) => new(NextId(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, kind, [], actions ?? [], state.ProfileVersion);
}
