using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Simulation.CargoPresentation;

/// <summary>What one observed column offers at the floor the observer stands on.</summary>
public enum LocalGroundKind : byte
{
    /// <summary>No intact ground at the floor: an opening, a channel or an excavated cell. Nothing stands here.</summary>
    Open = 0,
    Surface = 1,
    Soil = 2,
    Stone = 3,

    /// <summary>The cell above the floor is solid: a bank, wall or blocker, never walkable ground at this floor.</summary>
    Raised = 4
}

/// <summary>
/// One admitted terrain region as the player may observe it: its exact bounds and revision, the ground kind of every
/// column at the observer's floor and, for open columns, how many open cells lie above the first solid cell below the
/// floor (zero when that floor is not known within <see cref="LocalRegionObserver.MaximumOpeningDepth"/> cells). Both
/// are in the region's row-major X-then-Y order. It carries no material identity, source lineage or excavation history.
/// </summary>
public sealed class LocalObservedRegion
{
    internal LocalObservedRegion(TerrainRegionBounds bounds, ulong revision, LocalGroundKind[] ground, byte[] openingDepth)
    {
        if (ground.Length != checked(bounds.Width * bounds.Depth) || openingDepth.Length != ground.Length)
        {
            throw new ArgumentException("An observed region needs one ground kind and one opening depth per column.");
        }
        Bounds = bounds;
        Revision = revision;
        Ground = Array.AsReadOnly(ground);
        OpeningDepth = Array.AsReadOnly(openingDepth);
    }

    public TerrainRegionBounds Bounds { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<LocalGroundKind> Ground { get; }

    /// <summary>Open cells from the floor down to known solid ground, per column; zero for ground or an unknown bottom.</summary>
    public ReadOnlyCollection<byte> OpeningDepth { get; }

    /// <summary>The ground kind of one column of this region; columns outside it are refused.</summary>
    public LocalGroundKind GroundAt(long cellX, long cellY) => Ground[IndexOf(cellX, cellY)];

    /// <summary>The known opening depth of one column of this region, in cells; columns outside it are refused.</summary>
    public int OpeningDepthAt(long cellX, long cellY) => OpeningDepth[IndexOf(cellX, cellY)];

    private int IndexOf(long cellX, long cellY)
    {
        long x = cellX - Bounds.Minimum.X;
        long y = cellY - Bounds.Minimum.Y;
        if (x < 0 || y < 0 || x >= Bounds.Width || y >= Bounds.Depth)
        {
            throw new ArgumentOutOfRangeException(nameof(cellX), "That column lies outside the observed region.");
        }
        return (int)(y * Bounds.Width + x);
    }
}

/// <summary>
/// Committed liquid in one observed column between the known bottom of an opening and the cell above the floor. The
/// surface is the exact public fill of the highest wet cell; no equilibrium, current or reservoir identity is implied.
/// </summary>
public sealed record LocalObservedWater(long CellX, long CellY, ulong VolumeMicrolitres, long SurfaceSubunits);

/// <summary>
/// The bounded terrain the player observes now: the single standing floor of its feet, every admitted region whose
/// columns reach within <see cref="LocalRegionObserver.ObservationRadiusCells"/> of them in bounds order, and the water
/// in those columns. The region holding the player's column is always included. Regions beyond that window, other
/// floors and every actor's private state are absent; this is presentation locality, not a visibility, streaming or
/// discovery system. It is projected from the same immutable root as the view that carries it.
/// </summary>
public sealed class LocalRegionObservation
{
    internal LocalRegionObservation(EntityId playerActorId, SubunitPosition playerFeet, IReadOnlyList<LocalObservedRegion> regions,
        IReadOnlyList<LocalObservedWater>? water = null)
    {
        PlayerActorId = playerActorId;
        PlayerFeet = playerFeet;
        FloorFeetZ = playerFeet.Z;
        Regions = Array.AsReadOnly(regions.ToArray());
        Water = Array.AsReadOnly((water ?? []).OrderBy(value => value.CellY).ThenBy(value => value.CellX).ToArray());
    }

    public EntityId PlayerActorId { get; }
    public SubunitPosition PlayerFeet { get; }

    /// <summary>The standing height of the observed floor, in subunits: the top face of its ground cells.</summary>
    public long FloorFeetZ { get; }
    public ReadOnlyCollection<LocalObservedRegion> Regions { get; }

    /// <summary>Wet observed columns in Y-then-X order; dry columns are absent.</summary>
    public ReadOnlyCollection<LocalObservedWater> Water { get; }

    /// <summary>The observed region holding one column, or null outside the observation.</summary>
    public LocalObservedRegion? RegionAt(long cellX, long cellY) => Regions.FirstOrDefault(region =>
        cellX >= region.Bounds.Minimum.X && cellX <= region.Bounds.Maximum.X
        && cellY >= region.Bounds.Minimum.Y && cellY <= region.Bounds.Maximum.Y);

    /// <summary>The water of one observed column, or null when it is dry or not observed.</summary>
    public LocalObservedWater? WaterAt(long cellX, long cellY) =>
        Water.FirstOrDefault(value => value.CellX == cellX && value.CellY == cellY);

    /// <summary>Whether a public position stands on the observed floor inside an observed region.</summary>
    public bool Contains(SubunitPosition feet) => feet.Z == FloorFeetZ && RegionAt(feet.Cell.X, feet.Cell.Y) is not null;
}
