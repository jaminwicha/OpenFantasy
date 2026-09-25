using System.Text.Json.Serialization;
using OpenFantasy.Core;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Host;

/// <summary>Privileged actual liquid placement; no equilibrium reservoir surface is reported as a moving cell level.</summary>
internal sealed class LiquidCellDiagnosticReport
{
    internal LiquidCellDiagnosticReport(LiquidCellState state)
    {
        TopologyHash = state.Topology?.ContentHash.Value;
        OpenCapacityMicrolitres = state.Topology?.OpenCapacity.Value;
        ContentHash = state.ContentHash.Value;
        ReservoirHash = state.ReservoirHash.Value;
        ProfileHash = state.Profile.Version.Value;
        Boundary = state.Boundary;
        Revision = state.Revision;
        DownwardUnitsPerFace = state.Profile.DownwardUnitsPerFace;
        LateralUnitsPerFace = state.Profile.LateralUnitsPerFace;
        UnitsPerCell = state.UnitsPerCell;
        MicrolitresPerUnit = state.VolumePerUnit.Value;
        Cells = state.DomainCells.Select(cell =>
        {
            ulong units = state.Quantities.GetValueOrDefault(cell);
            return new LiquidCellQuantityDiagnostic(cell.ReservoirId.ToString(), cell.Coordinate.X,
                cell.Coordinate.Y, cell.Coordinate.Z, units, checked(units * MicrolitresPerUnit));
        }).ToArray();
        Contents = Cells.GroupBy(cell => cell.ReservoirId).Select(group => new LiquidCellReservoirDiagnostic(
            group.Key, group.Count(), group.Aggregate(0UL, (sum, cell) => checked(sum + cell.Units)),
            group.Aggregate(0UL, (sum, cell) => checked(sum + cell.Microlitres)))).ToArray();
        Portions = state.Portions.Select(portion => new LiquidCellPortionDiagnostic(portion.Cell.ReservoirId.ToString(),
            portion.Cell.Coordinate.X, portion.Cell.Coordinate.Y, portion.Cell.Coordinate.Z,
            portion.Origin.ToString(), portion.OriginId.ToString(), portion.Start, portion.End, portion.Quantity)).ToArray();
        ActualUnits = Contents.Aggregate(0UL, (sum, contents) => checked(sum + contents.ActualUnits));
        ActualMicrolitres = Contents.Aggregate(0UL, (sum, contents) => checked(sum + contents.ActualMicrolitres));
    }

    public string PlacementInterpretation => TopologyHash is null ? "retained-liquid-cells-v1" : "retained-terrain-liquid-cells-v2";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopologyHash { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? OpenCapacityMicrolitres { get; }
    public string ContentHash { get; }
    public string ReservoirHash { get; }
    public string ProfileHash { get; }
    public WorldInstant Boundary { get; }
    public ulong Revision { get; }
    public ulong DownwardUnitsPerFace { get; }
    public ulong LateralUnitsPerFace { get; }
    public ulong UnitsPerCell { get; }
    public ulong MicrolitresPerUnit { get; }
    public int DomainCellCount => Cells.Count;
    public int PortionCount => Portions.Count;
    public ulong ActualUnits { get; }
    public ulong ActualMicrolitres { get; }
    public IReadOnlyList<LiquidCellReservoirDiagnostic> Contents { get; }
    public IReadOnlyList<LiquidCellQuantityDiagnostic> Cells { get; }
    public IReadOnlyList<LiquidCellPortionDiagnostic> Portions { get; }
}

internal sealed record LiquidCellReservoirDiagnostic(string ReservoirId, int DomainCellCount,
    ulong ActualUnits, ulong ActualMicrolitres);

internal sealed record LiquidCellQuantityDiagnostic(string ReservoirId, long X, long Y, long Z,
    ulong Units, ulong Microlitres);

internal sealed record LiquidCellPortionDiagnostic(string ReservoirId, long X, long Y, long Z,
    string Origin, string OriginId, ulong Start, ulong End, ulong Quantity);
