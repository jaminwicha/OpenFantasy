using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting;

/// <summary>Bounded local operator inputs; these files are not authentication or player projections.</summary>
public static class LocalAuthorityFiles
{
    public static CountedAuthorityState ReadGenesis(string path) =>
        AuthorityEncodingV1.Decode(Read(path, AuthorityEncodingV1.MaximumStateByteLength));

    public static CommandEnvelope ReadCommand(string path) =>
        CommandCodecV1.Decode(Read(path, CommandCodecV1.MaximumEnvelopeByteLength));

    public static WorldSnapshot ReadComposedGenesis(string path) =>
        WorldSnapshotCodec.Decode(Read(path, WorldSnapshotCodec.MaximumStateByteLength));

    public static WorldStepBatch ReadWorldStep(string path) =>
        WorldStepCodec.Decode(Read(path, WorldStepCodec.MaximumBatchByteLength));

    public static PreparedWatershed ReadWatershed(string path) =>
        WatershedCodecV1.Decode(Read(path, WatershedCodecV1.MaximumEncodedByteLength));

    public static PreparedWatershedNetwork ReadWatershedNetwork(string path) =>
        WatershedNetworkCodecV1.Decode(Read(path, WatershedNetworkCodecV1.MaximumEncodedByteLength));

    public static PreparedWatershedTerrain ReadWatershedTerrain(string path) =>
        WatershedTerrainCodecV1.Decode(Read(path, WatershedTerrainCodecV1.MaximumEncodedByteLength));

    public static TerrainMaterialState ReadTerrainMaterials(string path) =>
        TerrainMaterialCodecV1.Decode(Read(path, TerrainMaterialCodecV1.MaximumEncodedByteLength));

    public static TerrainMaterialRegistry ReadMaterialRegistry(string path) =>
        TerrainMaterialRegistryCodecV1.Decode(Read(path, TerrainMaterialRegistryCodecV1.MaximumEncodedByteLength));

    public static TerrainMaterialRegistry ReadMaterialProduction(string path) =>
        TerrainMaterialRegistryCodecV2.Decode(Read(path, TerrainMaterialRegistryCodecV2.MaximumEncodedByteLength));

    public static PreparedWatershedRunoff ReadWatershedRunoff(string path) =>
        WatershedRunoffCodecV1.Decode(Read(path, WatershedRunoffCodecV1.MaximumEncodedByteLength));

    public static WatershedRunoffCapacityState ReadWatershedRunoffCapacity(string path) =>
        WatershedRunoffCapacityCodecV1.Decode(Read(path, WatershedRunoffCapacityCodecV1.MaximumEncodedByteLength));

    private static byte[] Read(string path, int maximum)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length < 1 || stream.Length > maximum)
        {
            throw new InvalidDataException("The local authority input exceeds its format bounds.");
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("The local authority input changed while being read.");
        }
        return bytes;
    }
}
