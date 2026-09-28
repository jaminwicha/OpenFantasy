using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.OrdinaryCareFixture;

namespace OpenFantasy.Core.Tests.Care;

public sealed class CareLegacyCompatibilityTests
{
    [Theory]
    [InlineData("aftermath-empty", "48366460b032007d5bdb0fed7c09e1fb5fe3a970734adeb5656bd39cfc0145ca")]
    [InlineData("aftermath-pending", "d219dbc035a33443d76659b441f707020f9bf9e2cbb505404f3b1716ff48e89d")]
    [InlineData("aftermath-complete", "ad896aa6a5b75daf7e176a86f814405e8ec8137960725db3a2bd02179325a014")]
    [InlineData("aftermath-full", "82f05003b0873a3b27740cff2eedcb2d45c5ffd150313eb5742d728a2e4fe4be")]
    [InlineData("ordinary-empty", "9cff0a1d0e415851e069bbd2836bd81781ab40bc6917704bbf8fb6eb56131e8f")]
    [InlineData("ordinary-mixed-revoked", "376b7b56af6571c3a06b6a0838c68cd076efd0fda5c5711b8859d6084af2f4c5")]
    [InlineData("ordinary-full", "ca1a65d65ca92535c0d093a88cbc2fb46388fcd137799f99bb90a97d67f46d45")]
    public void Original_care_frames_keep_their_exact_bytes(string name, string expectedHash)
    {
        var acknowledgment = Acknowledgment();
        var state = name switch
        {
            "aftermath-empty" => CareModelFixture.Create(),
            "aftermath-pending" => CareModelFixture.Create([acknowledgment]),
            "aftermath-complete" => CareModelFixture.History(2),
            "aftermath-full" => CareModelFixture.History(256, maximumCancelled: true),
            "ordinary-empty" => CreateOrdinary(),
            "ordinary-mixed-revoked" => CreateOrdinary(Ordinary().RecordUse(Use()).Revoke(Revocation()), CareModelFixture.History(1)),
            "ordinary-full" => CreateOrdinary(Ordinary(Enumerable.Range(0, 256).Select(index =>
                Use(sequence: 20 + (ulong)index, command: 10_000 + (ulong)index, patientRevision: 4 + (ulong)index)))),
            _ => throw new ArgumentException("Unknown retained care fixture.")
        };
        byte[] bytes = CareCodec.Encode(state);
        Assert.Equal(bytes, CareCodec.Encode(CareCodec.Decode(bytes)));
        Assert.Equal(expectedHash, ContentHash.Compute(bytes).ToString());
        if (Environment.GetEnvironmentVariable("OPENFANTASY_CARE_COMPATIBILITY_EVIDENCE") is { } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, name + ".bin"), bytes);
        }
    }
}
