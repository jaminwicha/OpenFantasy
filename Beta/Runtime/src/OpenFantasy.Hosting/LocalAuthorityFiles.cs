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
