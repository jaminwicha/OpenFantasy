using System.Text.Json;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Host;

/// <summary>Privileged read-only inspection of a finite network archive, independent of any saved-world authority.</summary>
internal sealed class WatershedNetworkOperator
{
    internal string Inspect(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > WatershedNetworkCodecV1.MaximumEncodedByteLength)
        {
            throw new InvalidDataException("Watershed network input exceeds its byte limit.");
        }
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Watershed network input changed length during inspection.");
        }
        PreparedWatershedNetwork network = WatershedNetworkCodecV1.Decode(bytes);
        return JsonSerializer.Serialize(new
        {
            Contract = "watershed-network-v1", Scope = "finite-macro-candidate",
            ContentHash = network.ComputeContentHash().Value, ProvinceId = network.ProvinceId.ToString(),
            WorldId = network.Scope.WorldId.ToString(), BranchId = network.Scope.BranchId.ToString(),
            RealmId = network.Scope.RealmId.ToString(), BasinCount = network.Basins.Count,
            network.ColumnCount, SharedFaceCount = network.SharedFaces.Count,
            DrainageLinkCount = network.Closures.Count(closure => closure.Kind == WatershedOutletKind.Basin),
            TerminalContributions = network.Terminals.Select(terminal => new
            {
                BasinId = terminal.BasinId.ToString(), ExternalBoundaryId = terminal.ExternalBoundaryId.ToString(),
                terminal.ContributingColumns, terminal.ColumnCapacity
            })
        });
    }
}
