using System.Collections.ObjectModel;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Owned immutable boolean cells; no caller retains the array behind the readonly collection.</summary>
internal sealed class LocalTerrainOccupancy(IEnumerable<bool> cells) : ReadOnlyCollection<bool>(cells.ToArray())
{
}
