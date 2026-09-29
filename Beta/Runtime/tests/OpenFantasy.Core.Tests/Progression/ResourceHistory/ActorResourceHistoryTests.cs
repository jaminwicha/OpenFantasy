using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using static OpenFantasy.Core.Tests.Progression.ProgressionFixture;

namespace OpenFantasy.Core.Tests.Progression.ResourceHistory;

public sealed class ActorResourceHistoryTests
{
    [Fact]
    public void Complete_chain_retains_no_level_reward_and_one_revision_for_level_refill()
    {
        var basis = Create();
        var state = basis;
        var history = new ActorResourceHistory(basis, 20, []);
        ActorResourceTransition[] effects = BuildChain(basis.GetActor(Id(10)));
        for (int i = 0; i < effects.Length; i++)
        {
            history = history.Append(new((ulong)(21 + i), 0, Id((ulong)(1000 + i)),
                [ActorResourceChange.FromTransition(0, Cause(), effects[i])]));
            state = Replace(state, effects[i].Candidate);
        }
        history.ValidateCurrent(state, 26);
        Assert.Equal(6, history.ChangeCount);
        Assert.Equal(6UL, state.GetActor(Id(10)).Revision);
        Assert.Equal(63, state.GetActor(Id(10)).CurrentHp);
        Assert.False(effects[3].ChangesResources);
        Assert.Equal(4UL, effects[3].Candidate.Revision);
        Assert.Equal(5UL, effects[4].Candidate.Revision);
        Assert.Equal(69, effects[4].Candidate.CurrentHp);
        Assert.Equal(12, effects[4].Candidate.CurrentMp);
        byte[] bytes = ActorResourceHistoryCodecV1.Encode(history);
        // Pinned from the independently retained original six-effect history fixture.
        Assert.Equal("94a19734c7e22c4acf58ca293473fbde11c724b6b74073258a9fa89e25e0b47a",
            ContentHash.Compute(bytes).ToString());
        var restoredBasis = ProgressionCodecV1.Decode(ProgressionCodecV1.Encode(basis));
        var restored = ActorResourceHistoryCodecV1.Decode(bytes, restoredBasis, 20);
        restored.ValidateCurrent(state, 26);
        Assert.Equal(bytes, ActorResourceHistoryCodecV1.Encode(restored));
        if (Environment.GetEnvironmentVariable("OPENFANTASY_RESOURCE_HISTORY_FIXTURES") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "basis.bin"), ProgressionCodecV1.Encode(basis));
            File.WriteAllBytes(Path.Combine(output, "current.bin"), ProgressionCodecV1.Encode(state));
            File.WriteAllBytes(Path.Combine(output, "history.bin"), bytes);
        }
    }

    [Fact]
    public void Multiple_actors_and_commands_can_share_one_world_boundary()
    {
        var basis = Create();
        var player = basis.GetActor(Id(10)); var npc = basis.GetActor(Id(11));
        var first = new ResourceChangeBatch(1, 2, Id(1000),
            [ActorResourceChange.FromTransition(0, Cause(), player.TakeDamage(5)),
             ActorResourceChange.FromTransition(1, Cause(), npc.TakeDamage(1))]);
        var second = new ResourceChangeBatch(1, 4, Id(1001),
            [ActorResourceChange.FromTransition(0, Cause(), player.TakeDamage(5).Candidate.SpendMagic(1))]);
        var history = new ActorResourceHistory(basis, 0, [first, second]);
        var current = Replace(Replace(basis, first.Changes[0].After, first.Changes[1].After), second.Changes[0].After);
        history.ValidateCurrent(current, 1);
        Assert.Equal(3, history.ChangeCount);
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0, [second, first]));
        Assert.Throws<ArgumentException>(() => new ResourceChangeBatch(1, 2, Id(1000), first.Changes.Reverse()));
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0, [first,
            new ResourceChangeBatch(2, 0, Id(1000), second.Changes)]));
    }

    [Fact]
    public void Missing_actor_change_or_unexplained_current_actor_is_refused()
    {
        var basis = Create(); var effects = BuildChain(basis.GetActor(Id(10)));
        var batches = effects.Select((e, i) => new ResourceChangeBatch((ulong)i + 1, 0, Id((ulong)i + 1000),
            [ActorResourceChange.FromTransition(0, Cause(), e)])).ToArray();
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0, batches.Where((_, i) => i != 3)));
        var history = new ActorResourceHistory(basis, 0, [batches[0]]);
        Assert.Throws<ArgumentException>(() => history.ValidateCurrent(Replace(basis, effects[1].Candidate), 2));
        Assert.Throws<ArgumentException>(() => history.ValidateCurrent(Replace(basis, effects[0].Candidate,
            basis.GetActor(Id(11)).TakeDamage(1).Candidate), 1));
        Assert.Throws<ArgumentException>(() => history.ValidateCurrent(Replace(basis, effects[0].Candidate), 0));
        Assert.Throws<ArgumentException>(() => history.Append(batches[2]));
        Assert.Single(history.Batches);
        Assert.Equal(60, basis.GetActor(Id(10)).CurrentHp);
    }

    [Fact]
    public void Claimed_arithmetic_no_op_unknown_actor_and_wrong_rule_pins_refuse()
    {
        var basis = Create(); var actor = basis.GetActor(Id(10));
        Assert.Throws<ArgumentException>(() => new ActorResourceChange(0, Cause(), ActorResourceOperation.Damage,
            5, actor, actor.TakeDamage(6).Candidate));
        Assert.Throws<ArgumentException>(() => ActorResourceChange.FromTransition(0, Cause(), actor.SpendMagic(0)));
        var unknown = new ActorProgressionState(Id(900), 0, actor.Profile, actor.Curve, 1, 0, 60, 10);
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0,
            [new(1, 0, Id(1000), [ActorResourceChange.FromTransition(0, Cause(), unknown.TakeDamage(1))])]));
        var first = new ResourceChangeBatch(1, 0, Id(1000), [ActorResourceChange.FromTransition(0, Cause(), actor.TakeDamage(1))]);
        var conflicting = new ResourceChangeCause(Id(301), Id(300), ContentHash.Compute("other-rule"u8));
        Assert.Throws<ArgumentException>(() => new ActorResourceHistory(basis, 0, [first,
            new(2, 0, Id(1001), [ActorResourceChange.FromTransition(0, conflicting, actor.TakeDamage(1).Candidate.TakeDamage(1))])]));
    }

    private static ActorResourceTransition[] BuildChain(ActorProgressionState actor)
    {
        var damage = actor.TakeDamage(50); var healing = damage.Candidate.RestoreHealth(7);
        var magic = healing.Candidate.SpendMagic(3);
        var xp = ActorRewardPlanner.Plan(Create(magic.Candidate), actor.ActorId, magic.Candidate.Revision, Reward(25, 0)).ActorTransition!;
        var level = ActorRewardPlanner.Plan(Create(xp.Candidate), actor.ActorId, xp.Candidate.Revision, Reward(75, 0)).ActorTransition!;
        return [damage, healing, magic, xp, level, level.Candidate.TakeDamage(6)];
    }

    internal static ResourceChangeCause Cause() => new(Id(301), Id(300), ContentHash.Compute("resource-rule"u8));

    internal static ProgressionSnapshot Replace(ProgressionSnapshot basis, params ActorProgressionState[] actors)
        => new(basis.WorldId, basis.BranchId, basis.Revision + 1, basis.Profiles, basis.Curves,
            basis.Actors.Select(a => actors.FirstOrDefault(replacement => replacement.ActorId == a.ActorId) ?? a), basis.Currency);
}
