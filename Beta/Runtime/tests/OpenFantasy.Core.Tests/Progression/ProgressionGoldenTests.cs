using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using static OpenFantasy.Core.Tests.Progression.ProgressionFixture;

namespace OpenFantasy.Core.Tests.Progression;

public sealed class ProgressionGoldenTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Progression", "progression-v1.json")));

    [Fact]
    public void IndependentFramesMatchDefinitionsInitialStateAndBothActorRewardResults()
    {
        using JsonDocument fixture = Fixture();
        Assert.Equal(Curve().Thresholds, fixture.RootElement.GetProperty("thresholds").EnumerateArray()
            .Select(value => value.GetUInt64()));
        ProgressionSnapshot initial = Create();
        Dictionary<string, byte[]> actual = new()
        {
            ["human_squire"] = ProgressionCodecV1.EncodeProfile(Squire()),
            ["unassigned"] = ProgressionCodecV1.EncodeProfile(Unassigned()),
            ["elf_squire"] = ProgressionCodecV1.EncodeProfile(new(Id(102), "elf-squire.unequipped",
                "Elf Squire (unequipped)", ActorStatProfileKind.Class, Squire().BaseStats,
                Squire().Growth, new(-8, 4, 0, 3, 0, 2))),
            ["source_curve"] = ProgressionCodecV1.EncodeCurve(Curve()),
            ["gil"] = ProgressionCodecV1.EncodeCurrency(Gil()),
            ["wren_reward"] = ProgressionCodecV1.EncodeReward(Reward()),
            ["initial_ledger"] = ProgressionCodecV1.EncodeLedger(initial.Currency),
            ["initial"] = ProgressionCodecV1.Encode(initial),
            ["rewarded_player"] = ProgressionCodecV1.Encode(ActorRewardPlanner.Plan(initial, Id(10), 0, Reward()).Candidate),
            ["rewarded_npc"] = ProgressionCodecV1.Encode(ActorRewardPlanner.Plan(initial, Id(11), 0, Reward()).Candidate)
        };
        foreach (JsonElement example in fixture.RootElement.GetProperty("examples").EnumerateArray())
        {
            byte[] expected = Convert.FromHexString(example.GetProperty("hex").GetString()!);
            Assert.Equal(expected, actual[example.GetProperty("name").GetString()!]);
            Assert.Equal(example.GetProperty("sha256").GetString(), ContentHash.Compute(expected).Value);
            Assert.Equal(expected, Restore(example.GetProperty("kind").GetString()!, expected));
        }
    }

    [Fact]
    public void EveryTruncatedPrefixAndIndependentSemanticCorruptionFailsClosed()
    {
        using JsonDocument fixture = Fixture();
        foreach (JsonElement example in fixture.RootElement.GetProperty("examples").EnumerateArray())
        {
            byte[] complete = Convert.FromHexString(example.GetProperty("hex").GetString()!);
            string kind = example.GetProperty("kind").GetString()!;
            for (int length = 0; length < complete.Length; length++)
            {
                byte[] prefix = complete[..length];
                Assert.Throws<ProgressionFormatException>(() => Restore(kind, prefix));
            }
        }
        foreach (JsonElement example in fixture.RootElement.GetProperty("malformed").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(example.GetProperty("hex").GetString()!);
            Assert.Throws<ProgressionFormatException>(() => Restore(example.GetProperty("kind").GetString()!, bytes));
        }
    }

    [Fact]
    public void PublicDecoderLimitsRejectOversizedInputBeforeRestoration()
    {
        (string Kind, int Maximum)[] limits =
        [
            ("snapshot", ProgressionCodecV1.MaximumSnapshotByteLength),
            ("profile", ProgressionCodecV1.MaximumProfileByteLength),
            ("curve", ProgressionCodecV1.MaximumCurveByteLength),
            ("currency", ProgressionCodecV1.MaximumCurrencyByteLength),
            ("reward", ProgressionCodecV1.MaximumRewardByteLength),
            ("ledger", ProgressionCodecV1.MaximumLedgerByteLength)
        ];
        foreach (var limit in limits)
        {
            byte[] bytes = new byte[limit.Maximum + 1];
            Assert.Throws<ProgressionFormatException>(() => Restore(limit.Kind, bytes));
        }
    }

    private static byte[] Restore(string kind, byte[] bytes) => kind switch
    {
        "snapshot" => ProgressionCodecV1.Encode(ProgressionCodecV1.Decode(bytes)),
        "profile" => ProgressionCodecV1.EncodeProfile(ProgressionCodecV1.DecodeProfile(bytes)),
        "curve" => ProgressionCodecV1.EncodeCurve(ProgressionCodecV1.DecodeCurve(bytes)),
        "currency" => ProgressionCodecV1.EncodeCurrency(ProgressionCodecV1.DecodeCurrency(bytes)),
        "reward" => ProgressionCodecV1.EncodeReward(ProgressionCodecV1.DecodeReward(bytes)),
        "ledger" => ProgressionCodecV1.EncodeLedger(ProgressionCodecV1.DecodeLedger(bytes)),
        _ => throw new InvalidOperationException("Unknown test fixture kind.")
    };
}
