using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using static OpenFantasy.Core.Tests.Progression.ProgressionFixture;
using static OpenFantasy.Core.Tests.Progression.ResourceHistory.ActorResourceHistoryTests;

namespace OpenFantasy.Core.Tests.Progression.ResourceHistory;

public sealed class ResourceHistoryBoundaryTests
{
    [Fact]
    public void Empty_history_is_canonical_and_requires_unchanged_complete_actors()
    {
        var basis = Create();
        var history = new ActorResourceHistory(basis, 17, []);
        history.ValidateCurrent(basis, 17);
        var bytes = ActorResourceHistoryCodecV1.Encode(history);
        Assert.Equal(bytes, ActorResourceHistoryCodecV1.Encode(ActorResourceHistoryCodecV1.Decode(bytes, basis, 17)));
        Assert.Throws<ArgumentException>(() => history.ValidateCurrent(Create(basis.GetActor(Id(10)).TakeDamage(1).Candidate), 17));
    }

    [Fact]
    public void Exact_envelopes_roundtrip_and_overflow_refuses_without_mutation()
    {
        var basis = Create();
        var batches = Alternating(basis, 8, 128);
        var history = new ActorResourceHistory(basis, 0, batches);
        Assert.Equal(1024, history.ChangeCount);
        var bytes = ActorResourceHistoryCodecV1.Encode(history);
        Assert.True(bytes.Length < ActorResourceHistoryCodecV1.MaximumEncodedByteLength);
        Assert.Equal(bytes, ActorResourceHistoryCodecV1.Encode(ActorResourceHistoryCodecV1.Decode(bytes, basis, 0)));
        Assert.Throws<ArgumentException>(() => history.Append(new(9, 0, Id(1008),
            [ActorResourceChange.FromTransition(0, Cause(), batches[^1].Changes[^1].After.TakeDamage(1))])));
        Assert.Equal(1024, history.ChangeCount);
        var many = new ActorResourceHistory(basis, 0, Alternating(basis, 256, 1));
        Assert.Equal(256, many.Batches.Count);
        Assert.Throws<ArgumentException>(() => many.Append(new(257, 0, Id(2000),
            [ActorResourceChange.FromTransition(0, Cause(), many.Batches[^1].Changes[0].After.TakeDamage(1))])));
        Assert.Throws<ArgumentException>(() => new ResourceChangeBatch(1, 0, Id(3000),
            batches[0].Changes.Append(batches[0].Changes[0])));
    }

    [Fact]
    public void Current_revision_and_definition_admission_cannot_go_backwards()
    {
        var basis = Create(snapshotRevision: 10);
        var history = new ActorResourceHistory(basis, 0, Alternating(basis, 1, 1));
        var actor = history.Batches[0].Changes[0].After;
        Assert.Throws<ArgumentException>(() => history.ValidateCurrent(Create(actor, snapshotRevision: 10), 1));
        history.ValidateCurrent(Create(actor, snapshotRevision: 11), 1);
        // Unrelated ledger activity may advance the envelope, but is authenticated by its own owner.
        history.ValidateCurrent(Create(actor, snapshotRevision: 12), 2);
        var extra = new ActorStatProfileDefinition(Id(400), "other", "Other", ActorStatProfileKind.Class,
            new(1, 0, 0, 0, 0, 1), ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        var current = Replace(basis, actor);
        var enlarged = new ProgressionSnapshot(current.WorldId, current.BranchId, current.Revision,
            current.Profiles.Append(extra), current.Curves, current.Actors, current.Currency);
        Assert.Throws<ArgumentException>(() => history.ValidateCurrent(enlarged, 1));
        Assert.Throws<OverflowException>(() => new ActorResourceHistory(Create(snapshotRevision: ulong.MaxValue), 0,
            Alternating(basis, 1, 1)));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(10)] [InlineData(100)] [InlineData(110)] [InlineData(120)] [InlineData(200)] [InlineData(300)]
    public void Command_or_causal_evidence_cannot_alias_scope_actor_account_or_rule(int identity)
    {
        var basis = Create();
        var transition = basis.GetActor(Id(10)).TakeDamage(1);
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0,
            [new(1, 0, Id((ulong)identity), [ActorResourceChange.FromTransition(0, Cause(), transition)])]));
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0,
            [new(1, 0, Id(1000), [ActorResourceChange.FromTransition(0,
                new(Id((ulong)identity), Id(300), Cause().DefinitionVersion), transition)])]));
    }

    [Fact]
    public void Cause_references_prove_consistency_but_do_not_authenticate_an_external_action()
    {
        var basis = Create();
        var fabricated = new ResourceChangeCause(Id(9000), Id(9001), ContentHash.Compute("unadmitted-rule"u8));
        var transition = basis.GetActor(Id(10)).TakeDamage(1);
        var history = new ActorResourceHistory(basis, 0,
            [new(1, 0, Id(1000), [ActorResourceChange.FromTransition(0, fabricated, transition)])]);
        history.ValidateCurrent(Replace(basis, transition.Candidate), 1);
        // The complete world must resolve this reference and authorize the actual requested amount.
        // A mathematically consistent standalone archive deliberately does not assert that proof.
        Assert.Equal(fabricated, ActorResourceHistoryCodecV1.Decode(ActorResourceHistoryCodecV1.Encode(history), basis, 0)
            .Batches[0].Changes[0].Cause);
    }

    [Fact]
    public void Evidence_cannot_alias_another_changes_rule_in_either_order()
    {
        var basis = Create();
        var damage = basis.GetActor(Id(10)).TakeDamage(1);
        var healing = damage.Candidate.RestoreHealth(1);
        var alias = new ResourceChangeCause(Id(300), Id(302), Cause().DefinitionVersion);
        var first = new ResourceChangeBatch(1, 0, Id(1000),
            [ActorResourceChange.FromTransition(0, Cause(), damage)]);
        var second = new ResourceChangeBatch(2, 0, Id(1001),
            [ActorResourceChange.FromTransition(0, alias, healing)]);
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0, [first, second]));
        // A future rule is collected before checking the first batch's evidence identity.
        var future = new ResourceChangeCause(Id(303), Id(301), Cause().DefinitionVersion);
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0, [first,
            new(2, 0, Id(1001), [ActorResourceChange.FromTransition(0, future, healing)])]));
    }

    [Fact]
    public void Zero_experience_reward_retains_its_actor_revision_in_the_archive()
    {
        var basis = Create();
        var transition = ActorRewardPlanner.Plan(basis, Id(10), 0, Reward(0, 0)).ActorTransition!;
        Assert.False(transition.ChangesResources);
        Assert.Equal(1UL, transition.Candidate.Revision);
        var history = new ActorResourceHistory(basis, 0,
            [new(1, 0, Id(1000), [ActorResourceChange.FromTransition(0, Cause(), transition)])]);
        var decoded = ActorResourceHistoryCodecV1.Decode(ActorResourceHistoryCodecV1.Encode(history), basis, 0);
        decoded.ValidateCurrent(Replace(basis, transition.Candidate), 1);
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0, [])
            .ValidateCurrent(Replace(basis, transition.Candidate), 1));
    }

    [Fact]
    public void Level_reward_refuses_a_forged_refill_or_level_even_with_the_right_revision()
    {
        var before = new ActorProgressionState(Id(10), 4, Squire(), Curve(), 1, 25, 13, 9);
        var transition = ActorRewardPlanner.Plan(Create(before), Id(10), 4, Reward(75, 0)).ActorTransition!;
        var forged = new ActorProgressionState(Id(10), 5, before.Profile, before.Curve, 2, 0, 13, 9);
        Assert.Throws<ArgumentException>(() => new ActorResourceChange(0, Cause(),
            ActorResourceOperation.ExperienceReward, 75, before, forged));
        var basis = Create(before);
        var history = new ActorResourceHistory(basis, 0,
            [new(1, 0, Id(1000), [ActorResourceChange.FromTransition(0, Cause(), transition)])]);
        var bytes = ActorResourceHistoryCodecV1.Encode(history);
        Assert.Throws<ProgressionFormatException>(() => ActorResourceHistoryCodecV1.Decode(
            Rewrite(bytes, [6, 1, 5, 1, 7, 8], Number(3)), basis, 0));
    }

    [Fact]
    public void Segments_cannot_begin_mid_step_at_their_basis_boundary()
    {
        var basis = Create();
        var effect = ActorResourceChange.FromTransition(0, Cause(), basis.GetActor(Id(10)).TakeDamage(1));
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 20,
            [new(20, uint.MaxValue, Id(1000), [effect])]));
        var history = new ActorResourceHistory(basis, 20, [new(21, 0, Id(1000), [effect])]);
        history.ValidateCurrent(Replace(basis, effect.After), 21);
    }

    [Fact]
    public void Direct_callers_receive_insufficient_magic_and_numeric_overflow_refusals()
    {
        var actor = Create().GetActor(Id(10));
        Assert.Throws<InvalidOperationException>(() => new ActorResourceChange(0, Cause(),
            ActorResourceOperation.MagicExpenditure, 11, actor, actor.TakeDamage(1).Candidate));
        Assert.Throws<OverflowException>(() => new ActorResourceChange(0, Cause(),
            ActorResourceOperation.Damage, ulong.MaxValue, actor, actor.TakeDamage(1).Candidate));
        var capped = new ActorProgressionState(Id(10), 0, actor.Profile, actor.Curve,
            actor.Curve.MaximumLevel, ulong.MaxValue, 60, 10);
        Assert.Throws<OverflowException>(() => new ActorResourceChange(0, Cause(),
            ActorResourceOperation.ExperienceReward, 1, capped, capped));
    }

    [Theory]
    [InlineData("root-version")] [InlineData("root-world")] [InlineData("root-branch")]
    [InlineData("basis-sequence")] [InlineData("basis-hash")] [InlineData("command-ordinal-overflow")]
    [InlineData("effect-ordinal-overflow")] [InlineData("operation-overflow")] [InlineData("operation-unknown")]
    [InlineData("amount-overflow")] [InlineData("after-hp")]
    [InlineData("after-profile-pin")] [InlineData("after-curve-pin")]
    [InlineData("before-revision")] [InlineData("missing-change")]
    public void Tampered_archive_is_refused(string mutation)
    {
        var basis = Create();
        var history = new ActorResourceHistory(basis, 0, Alternating(basis, 1, 1));
        var bytes = ActorResourceHistoryCodecV1.Encode(history);
        int[] path; byte[] replacement;
        switch (mutation)
        {
            case "root-version": path = [1]; replacement = Number(2); break;
            case "root-world": path = [2]; replacement = Id(999).ToByteArray(); break;
            case "root-branch": path = [3]; replacement = Id(999).ToByteArray(); break;
            case "basis-sequence": path = [4]; replacement = Number(1); break;
            case "basis-hash": path = [5]; replacement = new byte[32]; break;
            case "command-ordinal-overflow": path = [6, 1, 3]; replacement = Number(ulong.MaxValue); break;
            case "effect-ordinal-overflow": path = [6, 1, 5, 1, 2]; replacement = Number(ulong.MaxValue); break;
            case "operation-overflow": path = [6, 1, 5, 1, 4]; replacement = Number(ulong.MaxValue); break;
            case "operation-unknown": path = [6, 1, 5, 1, 4]; replacement = Number(999); break;
            case "amount-overflow": path = [6, 1, 5, 1, 5]; replacement = Number(ulong.MaxValue); break;
            case "after-hp": path = [6, 1, 5, 1, 7, 10]; replacement = Number(58); break;
            case "after-profile-pin": path = [6, 1, 5, 1, 7, 5]; replacement = new byte[32]; break;
            case "after-curve-pin": path = [6, 1, 5, 1, 7, 7]; replacement = new byte[32]; break;
            case "before-revision": path = [6, 1, 5, 1, 6, 3]; replacement = Number(10); break;
            default: path = [6, 1, 5]; replacement = EmptyChanges(); break;
        }
        Assert.Throws<ProgressionFormatException>(() => ActorResourceHistoryCodecV1.Decode(Rewrite(bytes, path, replacement), basis, 0));
        Assert.Throws<ProgressionFormatException>(() => ActorResourceHistoryCodecV1.Decode(bytes, Create(snapshotRevision: 1), 0));
        Assert.Equal(bytes, ActorResourceHistoryCodecV1.Encode(history));
    }

    [Fact]
    public void Every_truncation_trailing_bytes_and_oversized_headers_are_refused()
    {
        var basis = Create();
        var bytes = ActorResourceHistoryCodecV1.Encode(new(basis, 0, Alternating(basis, 1, 1)));
        for (int length = 0; length < bytes.Length; length++)
        {
            var prefix = bytes[..length];
            Assert.Throws<ProgressionFormatException>(() => ActorResourceHistoryCodecV1.Decode(prefix, basis, 0));
        }
        Assert.Throws<ProgressionFormatException>(() => ActorResourceHistoryCodecV1.Decode([.. bytes, 0], basis, 0));
        var header = bytes.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), uint.MaxValue);
        Assert.Throws<ProgressionFormatException>(() => ActorResourceHistoryCodecV1.Decode(header, basis, 0));
        header = bytes.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(12), uint.MaxValue);
        Assert.Throws<ProgressionFormatException>(() => ActorResourceHistoryCodecV1.Decode(header, basis, 0));
        Assert.Throws<ProgressionFormatException>(() => ActorResourceHistoryCodecV1.Decode(
            new byte[ActorResourceHistoryCodecV1.MaximumEncodedByteLength + 1], basis, 0));
    }

    internal static ResourceChangeBatch[] Alternating(ProgressionSnapshot basis, int batchCount, int changesPerBatch)
    {
        var actor = basis.GetActor(Id(10));
        List<ResourceChangeBatch> batches = [];
        for (int batch = 0; batch < batchCount; batch++)
        {
            List<ActorResourceChange> changes = [];
            for (int effect = 0; effect < changesPerBatch; effect++)
            {
                var transition = actor.CurrentHp == actor.Stats.MaxHp ? actor.TakeDamage(1) : actor.RestoreHealth(1);
                changes.Add(ActorResourceChange.FromTransition(effect, Cause(), transition));
                actor = transition.Candidate;
            }
            batches.Add(new((ulong)batch + 1, 0, Id((ulong)batch + 1000), changes));
        }
        return batches.ToArray();
    }

    private static byte[] Number(ulong number)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, number);
        return bytes;
    }

    private static byte[] EmptyChanges()
    {
        byte[] tag = "progression.resource-changes"u8.ToArray();
        byte[] bytes = new byte[16 + tag.Length];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12), (uint)tag.Length);
        tag.CopyTo(bytes, 16);
        return bytes;
    }

    private static byte[] Rewrite(byte[] bytes, int[] path, byte[] replacement)
    {
        var fields = new List<byte[]>();
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        int position = 12;
        for (int i = 0; i < count; i++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position)));
            fields.Add(bytes.AsSpan(position + 4, length).ToArray());
            position += 4 + length;
        }
        fields[path[0]] = path.Length == 1 ? replacement : Rewrite(fields[path[0]], path[1..], replacement);
        using var output = new MemoryStream();
        output.Write(bytes.AsSpan(0, 12));
        Span<byte> lengthBytes = stackalloc byte[4];
        foreach (var field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)field.Length);
            output.Write(lengthBytes); output.Write(field);
        }
        return output.ToArray();
    }
}
