using OpenFantasy.Core.Geography;

namespace OpenFantasy.Host;

/// <summary>Privileged macro facts; not a player map or an assertion of realized liquid volume.</summary>
internal sealed class WatershedNetworkDiagnosticReport
{
    internal WatershedNetworkDiagnosticReport(PreparedWatershedNetwork network)
    {
        ContentHash = network.ComputeContentHash().Value;
        ProvinceId = network.ProvinceId.ToString();
        BasinCount = network.Basins.Count;
        ColumnCount = network.ColumnCount;
        SharedFaceCount = network.SharedFaces.Count;
    }

    public string ContentHash { get; }
    public string ProvinceId { get; }
    public int BasinCount { get; }
    public int ColumnCount { get; }
    public int SharedFaceCount { get; }
}
