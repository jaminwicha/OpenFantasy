namespace OpenFantasy.Sim;

/// <summary>
/// M4 vehicles: key items that change a player's movement domain.
/// Embarking, disembarking, and per-domain tile rules — all server-side.
/// </summary>
public sealed partial class World
{
    /// <summary>Speed multiplier ×10 per vehicle kind (index = VehicleKind).</summary>
    private static readonly int[] VehicleSpeed10 = { 10, 12, 15, 18, 16, 25 };

    /// <summary>Embark priority when toggling (best first).</summary>
    private static readonly VehicleKind[] EmbarkPriority =
    {
        VehicleKind.Airship, VehicleKind.Tank, VehicleKind.Ship, VehicleKind.Boat, VehicleKind.Canoe,
    };

    private int _shallowTile = -2, _oceanTile = -2, _hillsTile = -2, _treeTile = -2;

    private void CacheVehicleTiles()
    {
        _shallowTile = Data.Map.TileIdByName("shallow");
        _oceanTile = Data.Map.TileIdByName("ocean");
        _hillsTile = Data.Map.TileIdByName("hills");
        _treeTile = Data.Map.TileIdByName("tree");
    }

    /// <summary>May this entity's current movement mode stand on the tile at (tx,ty)?</summary>
    public bool TileAllows(int tx, int ty, VehicleKind vehicle)
    {
        if (_shallowTile == -2) CacheVehicleTiles();
        int t = Data.Map.TileAt(tx, ty);
        if (t < 0) return false;
        return vehicle switch
        {
            VehicleKind.None => Data.Map.IsWalkableTile(tx, ty),
            VehicleKind.Canoe => t == _shallowTile,
            VehicleKind.Boat or VehicleKind.Ship => t == _shallowTile || t == _oceanTile,
            VehicleKind.Tank => Data.Map.IsWalkableTile(tx, ty) || t == _hillsTile || t == _treeTile,
            VehicleKind.Airship => true,
            _ => false,
        };
    }

    public bool UnitsAllow(int ux, int uy, VehicleKind vehicle)
        => TileAllows(ux / SimConstants.UnitsPerTile, uy / SimConstants.UnitsPerTile, vehicle);

    public int VehicleMoveSpeed(Entity e)
        => e.MoveSpeed * VehicleSpeed10[(int)e.Vehicle] / 10;

    /// <summary>The vehicle item ids this player owns, best first.</summary>
    private IEnumerable<(VehicleKind kind, string itemId)> OwnedVehicles(Entity player)
    {
        foreach (var kind in EmbarkPriority)
        {
            foreach (var stack in player.Inventory)
            {
                if (Data.Items.TryGetValue(stack.ItemId, out var item)
                    && item.Kind == ItemKind.Vehicle && item.Vehicle == kind)
                {
                    yield return (kind, stack.ItemId);
                    break;
                }
            }
        }
    }

    /// <summary>Board the best legal vehicle here, or step off the current one.</summary>
    public string ToggleVehicle(Entity player)
    {
        if (!player.Alive) return "The fallen travel nowhere.";
        if (player.MapId != 0) return "No room to launch anything down here.";

        if (player.Vehicle != VehicleKind.None)
            return Disembark(player);

        foreach (var (kind, _) in OwnedVehicles(player))
        {
            string result = TryEmbark(player, kind);
            if (result.Length > 0) return result;
        }
        return OwnedVehicles(player).Any()
            ? "Nowhere to launch here. (Water vehicles need adjacent water; the airship lifts off anywhere outdoors.)"
            : "You own no vehicle. Craft a canoe, buy a boat at a port, or earn stranger machines.";
    }

    /// <summary>Attempts to board; returns "" if not possible here.</summary>
    private string TryEmbark(Entity player, VehicleKind kind)
    {
        int u = SimConstants.UnitsPerTile;
        int ptx = player.X / u, pty = player.Y / u;
        if (Data.Map.IsSafeZoneTile(ptx, pty) && kind is VehicleKind.Tank or VehicleKind.Airship)
            return ""; // no launching machines inside town walls

        switch (kind)
        {
            case VehicleKind.Canoe:
            case VehicleKind.Boat:
            case VehicleKind.Ship:
                // Step onto an adjacent legal water tile.
                foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (-1, 1), (1, -1), (-1, -1) })
                {
                    int tx = ptx + dx, ty = pty + dy;
                    if (TileAllows(tx, ty, kind))
                    {
                        player.Vehicle = kind;
                        player.X = tx * u + u / 2;
                        player.Y = ty * u + u / 2;
                        Events.Add(new SimEvent(SimEventKind.AbilityUsed, player.Id, player.Id, -2, VehicleName(kind)));
                        return $"You board the {VehicleName(kind)}.";
                    }
                }
                return "";
            case VehicleKind.Tank:
            case VehicleKind.Airship:
                if (!TileAllows(ptx, pty, kind)) return "";
                player.Vehicle = kind;
                Events.Add(new SimEvent(SimEventKind.AbilityUsed, player.Id, player.Id, -2, VehicleName(kind)));
                return kind == VehicleKind.Airship
                    ? "The Brasswing rises! (Land over open ground to disembark.)"
                    : $"You mount the {VehicleName(kind)}.";
            default:
                return "";
        }
    }

    private string Disembark(Entity player)
    {
        int u = SimConstants.UnitsPerTile;
        int ptx = player.X / u, pty = player.Y / u;

        // Current tile first (tank on land, airship over land), then neighbors.
        foreach (var (dx, dy) in new[] { (0, 0), (0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (-1, 1), (1, -1), (-1, -1) })
        {
            int tx = ptx + dx, ty = pty + dy;
            if (Data.Map.IsWalkableTile(tx, ty))
            {
                player.Vehicle = VehicleKind.None;
                player.X = tx * u + u / 2;
                player.Y = ty * u + u / 2;
                return "You disembark.";
            }
        }
        return "No safe ground to disembark here.";
    }

    public static string VehicleName(VehicleKind kind) => kind switch
    {
        VehicleKind.Canoe => "canoe",
        VehicleKind.Boat => "sailboat",
        VehicleKind.Ship => "ship",
        VehicleKind.Tank => "dwarven tank",
        VehicleKind.Airship => "airship",
        _ => "vehicle",
    };
}
