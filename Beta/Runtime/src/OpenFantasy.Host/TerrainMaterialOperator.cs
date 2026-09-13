using System.Text.Json;
using OpenFantasy.Core.Geography;
using OpenFantasy.Hosting;

namespace OpenFantasy.Host;

/// <summary>Read-only operator inspection of standalone funded material representation, outside live terrain admission.</summary>
internal sealed class TerrainMaterialOperator
{
    internal string Inspect(string path)
    {
        TerrainMaterialState state = LocalAuthorityFiles.ReadTerrainMaterials(path);
        return JsonSerializer.Serialize(new
        {
            Contract = "terrain-material-state-v1", Scope = "standalone-funded-material",
            ContentHash = state.ContentHash.Value, state.Revision,
            SourceId = state.Deposit.Source.SourceId.ToString(), state.Deposit.Source.Start, state.Deposit.Source.End,
            state.CoarseQuantity, state.DetailedQuantity, state.OtherQuantity,
            ActiveCellCount = state.ActiveCells.Count, LotCount = state.Inventory.Custody.Lots.Count,
            state.Deposit.Minimum, state.Deposit.Maximum, state.Deposit.UnitsPerCell,
            MassMilligrams = state.Deposit.TotalMass.Value, VolumeMicrolitres = state.Deposit.TotalVolume.Value
        });
    }
}
