using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Tests.Authority;

public sealed class AuthorityEncodingTests
{
    [Fact]
    public void Independent_oracle_matches_state_hashes_transfer_command_and_receipts()
    {
        using JsonDocument fixture = Load();
        JsonElement root = fixture.RootElement;
        CountedAuthorityState original = AuthorityFixture.Create();
        AuthorityDecision decision = AuthorityFixture.Decide(original);
        Assert.Equal(Bytes(root, "originalHex"), AuthorityEncodingV1.Encode(original));
        Assert.Equal(root.GetProperty("originalHash").GetString(), original.ContentHash.Value);
        Assert.Equal(Bytes(root, "candidateHex"), AuthorityEncodingV1.Encode(decision.Candidate));
        Assert.Equal(root.GetProperty("candidateHash").GetString(), decision.Candidate.ContentHash.Value);
        Assert.Equal(root.GetProperty("childId").GetString(), decision.MovedLotId.ToString());
        Assert.Equal(Bytes(root, "commandHex"), CommandCodecV1.Encode(AuthorityFixture.Command(original)));
        Assert.Equal(Bytes(root, "appliedReceiptHex"), CommandReceiptCodecV1.Encode(decision.Receipt));
        CountedAuthorityState denied = AuthorityFixture.Create(CustodyDepositRule.OwnerOnly);
        Assert.Equal(Bytes(root, "ownerOnlyHex"), AuthorityEncodingV1.Encode(denied));
        Assert.Equal(Bytes(root, "notAuthorizedReceiptHex"), CommandReceiptCodecV1.Encode(AuthorityFixture.Decide(denied).Receipt));
    }

    [Fact]
    public void Durable_reload_preserves_actual_access_sources_and_reservations()
    {
        using JsonDocument fixture = Load();
        foreach (string key in new[] { "originalHex", "candidateHex", "ownerOnlyHex", "reservedHex" })
        {
            byte[] encoded = Bytes(fixture.RootElement, key);
            CountedAuthorityState restored = AuthorityEncodingV1.Decode(encoded);
            Assert.Equal(encoded, AuthorityEncodingV1.Encode(restored));
            Assert.True(restored.CanControl(AuthorityFixture.Delivery, AuthorityFixture.Id(23)));
            Assert.False(restored.CanControl(new(AuthorityFixture.Id(21), AuthorityFixture.Id(99)), AuthorityFixture.Id(23)));
            Assert.Equal(20UL, Assert.Single(restored.Sources).UnitCapacity);
        }
        CountedAuthorityState denied = AuthorityEncodingV1.Decode(Bytes(fixture.RootElement, "ownerOnlyHex"));
        Assert.Equal(CommandRejectionReason.NotAuthorized, AuthorityFixture.Decide(denied).Receipt.RejectionReason);
        CountedAuthorityState reserved = AuthorityEncodingV1.Decode(Bytes(fixture.RootElement, "reservedHex"));
        Assert.Equal(AuthorityFixture.Id(80), Assert.Single(reserved.ReservationIds));
        Assert.Equal(CommandRejectionReason.ResourceUnavailable, AuthorityFixture.Decide(reserved).Receipt.RejectionReason);
    }

    [Fact]
    public void Independent_malformed_and_semantically_invalid_states_fail_closed()
    {
        using JsonDocument fixture = Load();
        int rejected = 0;
        foreach (JsonElement entry in fixture.RootElement.GetProperty("invalidStates").EnumerateArray())
        {
            byte[] encoded = Convert.FromHexString(entry.GetProperty("hex").GetString()!);
            Assert.Throws<AuthorityFormatException>(() => AuthorityEncodingV1.Decode(encoded));
            rejected++;
        }
        Assert.Equal(47, rejected);
    }

    [Fact]
    public void Every_incomplete_prefix_and_oversized_state_rejects_before_reload()
    {
        using JsonDocument fixture = Load();
        byte[] original = Bytes(fixture.RootElement, "originalHex");
        for (int length = 0; length < original.Length; length++)
        {
            int prefixLength = length;
            Assert.Throws<AuthorityFormatException>(() => AuthorityEncodingV1.Decode(original.AsSpan(0, prefixLength)));
        }
        var oversized = new byte[AuthorityEncodingV1.MaximumStateByteLength + 1];
        Assert.Equal(AuthorityFormatFailure.LimitExceeded,
            Assert.Throws<AuthorityFormatException>(() => AuthorityEncodingV1.Decode(oversized)).Failure);
    }

    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "authority-v1.json")));

    private static byte[] Bytes(JsonElement root, string name) => Convert.FromHexString(root.GetProperty(name).GetString()!);
}
