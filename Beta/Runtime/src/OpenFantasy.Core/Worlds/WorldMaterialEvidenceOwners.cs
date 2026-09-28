using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The running owner candidates at one instant of a private transition. It exists only while the
/// decision is prepared; observations keep digests of these owners and never the owners themselves.
/// </summary>
internal readonly record struct WorldMaterialEvidenceOwners(CountedAuthorityState Inventory,
    ProductionSnapshot? Production, GatheringSnapshot? Gathering, ExcavationSnapshot? Excavation,
    ConstructionSnapshot? Construction, LocalSpatialSnapshot Space, QuestSnapshot? Quests,
    ProgressionSnapshot? Progression, CareSnapshot? Care, LiquidReservoirState? Reservoirs,
    LiquidCellState? LiquidCells)
{
    internal static WorldMaterialEvidenceOwners Of(WorldSnapshot world) => new(world.Inventory, world.Production,
        world.Gathering, world.Excavation, world.Construction, world.Space, world.Quests, world.Progression,
        world.Care, world.Reservoirs, world.LiquidCells);
}
