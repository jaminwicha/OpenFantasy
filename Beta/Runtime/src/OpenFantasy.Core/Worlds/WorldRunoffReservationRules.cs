namespace OpenFantasy.Core.Worlds;

/// <summary>Prevents one admitted reservation identity from representing unrelated world obligations.</summary>
internal static class WorldRunoffReservationRules
{
    internal static bool HasCompetingPurpose(WorldSnapshot world, EntityId reservationId) =>
        world.Quests?.Definitions.Any(q => q.MaterialReservationId == reservationId) == true
        || world.Construction?.Projects.Any(p => p.ReservationId == reservationId) == true;
}
