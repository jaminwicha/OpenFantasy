using System.Text.Json;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Tests.Authority.ConsentedMaterial;

public sealed class ConsentedMaterialGoldenTests
{
    [Theory]
    [InlineData("countedSource", ConsentedMaterialLotKind.CountedSource)]
    [InlineData("produced", ConsentedMaterialLotKind.Produced)]
    public void Independent_fixed_frames_pin_every_field_and_both_content_hashes(
        string example, ConsentedMaterialLotKind kind)
    {
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "consented-material-v1.json")));
        JsonElement expected = vectors.RootElement.GetProperty("examples").GetProperty(example);
        var definition = new CountedItemDefinition(Id(10),
            ContentHash.Compute("consented-gift-golden-material-v1"u8));
        var terms = new ConsentedMaterialTerms(Id(1), Id(2), Id(23), Id(24), kind, Id(5),
            definition, Id(3), Id(4), 0x0102030405060708UL, 0x8182838485868788UL);
        var command = new ConsentedMaterialCommand(Id(20), Id(21), Id(22), terms);
        var acceptance = ConsentedMaterialAcceptance.Approving(command, Id(30), Id(31), Id(32));

        byte[] commandBytes = AssertVector(expected.GetProperty("command"), ConsentedMaterialCodecV1.Encode(command));
        byte[] acceptanceBytes = AssertVector(expected.GetProperty("acceptance"), ConsentedMaterialCodecV1.Encode(acceptance));
        Assert.Equal(expected.GetProperty("command").GetProperty("sha256").GetString(), command.ComputeContentHash().ToString());
        Assert.Equal(expected.GetProperty("acceptance").GetProperty("sha256").GetString(), acceptance.ComputeContentHash().ToString());

        ConsentedMaterialCommand restoredCommand = ConsentedMaterialCodecV1.DecodeCommand(commandBytes);
        ConsentedMaterialAcceptance restoredAcceptance = ConsentedMaterialCodecV1.DecodeAcceptance(acceptanceBytes);
        Assert.Equal(terms, restoredCommand.Terms);
        Assert.Equal(terms, restoredAcceptance.Terms);
        Assert.Equal((Id(20), Id(21), Id(22)),
            (restoredCommand.CommandId, restoredCommand.PrincipalId, restoredCommand.OriginSessionId));
        Assert.Equal((Id(30), Id(31), Id(32), Id(20)),
            (restoredAcceptance.AcceptanceId, restoredAcceptance.PrincipalId,
                restoredAcceptance.OriginSessionId, restoredAcceptance.CommandId));
        Assert.True(restoredAcceptance.Binds(restoredCommand));
        Assert.Equal(commandBytes, ConsentedMaterialCodecV1.Encode(restoredCommand));
        Assert.Equal(acceptanceBytes, ConsentedMaterialCodecV1.Encode(restoredAcceptance));
    }

    private static EntityId Id(ulong suffix) => new(0x0102030405060708UL, 0x1122334455660000UL + suffix);

    private static byte[] AssertVector(JsonElement expected, byte[] actual)
    {
        byte[] bytes = Convert.FromHexString(expected.GetProperty("hex").GetString()!);
        Assert.Equal(expected.GetProperty("length").GetInt32(), actual.Length);
        Assert.Equal(bytes, actual);
        Assert.Equal(expected.GetProperty("sha256").GetString(), ContentHash.Compute(actual).Value);
        return bytes;
    }
}
