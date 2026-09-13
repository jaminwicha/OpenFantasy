using System.Security.Cryptography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Derives physical face identities from ancestor scope and ordered global endpoints, not input order.</summary>
internal sealed class WatershedNetworkFaces(WatershedNetworkInputs inputs, EntityId provinceId)
{
    internal WatershedNetworkFace[] Prepare(CancellationToken cancellation)
    {
        var faces = new List<WatershedNetworkFace>();
        foreach ((WatershedColumnAddress firstAddress, WatershedColumnReference first) in inputs.Columns.OrderBy(pair => pair.Key))
        {
            cancellation.ThrowIfCancellationRequested();
            // Positive neighbors enumerate every physical face once without overflowing signed addresses.
            if (firstAddress.X < long.MaxValue)
            {
                Add(firstAddress, first, new(firstAddress.X + 1, firstAddress.Y), faces);
            }
            if (firstAddress.Y < long.MaxValue)
            {
                Add(firstAddress, first, new(firstAddress.X, firstAddress.Y + 1), faces);
            }
        }
        return faces.ToArray();
    }

    private void Add(WatershedColumnAddress firstAddress, WatershedColumnReference first,
        WatershedColumnAddress secondAddress, List<WatershedNetworkFace> faces)
    {
        if (!inputs.Columns.TryGetValue(secondAddress, out WatershedColumnReference second) || first.BasinId == second.BasinId)
        {
            return;
        }
        if (faces.Count >= PreparedWatershedNetwork.MaximumSharedFaces)
        {
            throw new ArgumentException("The network exceeds its shared-face envelope.");
        }
        PreparedWatershed firstBasin = inputs.ByBasin[first.BasinId];
        PreparedWatershed secondBasin = inputs.ByBasin[second.BasinId];
        EntityId? from = DrainsTo(first, second) ? first.BasinId : DrainsTo(second, first) ? second.BasinId : null;
        var scope = firstBasin.Request.Scope;
        byte[] digest = SHA256.HashData(CanonicalEncodingV1.Frame("geography.watershed.network.face.v1",
            scope.WorldId.ToByteArray(), scope.RealmId.ToByteArray(), provinceId.ToByteArray(),
            TerrainRegionCodecV1.Signed64(firstAddress.X), TerrainRegionCodecV1.Signed64(firstAddress.Y),
            TerrainRegionCodecV1.Signed64(secondAddress.X), TerrainRegionCodecV1.Signed64(secondAddress.Y)));
        EntityId identity = EntityId.FromBytes(digest.AsSpan(0, EntityId.ByteLength));
        inputs.Register(identity);
        faces.Add(new WatershedNetworkFace(identity, first, second,
            firstBasin.Columns[first.ColumnIndex].NaturalElevation, secondBasin.Columns[second.ColumnIndex].NaturalElevation, from));
    }

    private bool DrainsTo(WatershedColumnReference from, WatershedColumnReference to)
    {
        WatershedOutletClosure closure = inputs.ByClosure[from.BasinId];
        return inputs.ByBasin[from.BasinId].Request.OutletIndex == from.ColumnIndex
            && closure.ReceivingBasinId == to.BasinId && closure.ReceivingColumnIndex == to.ColumnIndex;
    }
}
