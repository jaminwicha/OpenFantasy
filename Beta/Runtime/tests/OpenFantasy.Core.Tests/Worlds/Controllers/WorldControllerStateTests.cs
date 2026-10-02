using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds.Controllers;

public sealed class WorldControllerStateTests
{
    internal static EntityId Id(ulong value) => new(0x434f4e54524f4c, value);
    private static WorldControllerEvent Disconnect(ulong actor = 1, ulong revision = 0) =>
        new(Id(actor), WorldControllerEventKind.Disconnected, revision, reason: WorldControllerDisconnectReason.TransportLost);

    [Fact]
    public void Thirty_second_allowance_and_exact_episode_reconnection_preserve_other_actors()
    {
        var fresh = WorldControllerState.Fresh([Id(2), Id(1), Id(3), Id(4)]);
        var untouched = fresh.Get(Id(2));
        var disconnected = fresh.Apply(Disconnect(), Id(10));
        Assert.Equal(new WorldControllerAssignment(Id(1), 1, WorldControllerMode.ReconnectAllowance, Id(10)), disconnected.Get(Id(1)));
        Assert.Equal(WorldControllerMode.Human, fresh.Get(Id(1)).Mode);
        Assert.Same(untouched, disconnected.Get(Id(2)));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), WorldControllerEventKind.AllowanceExpired,
            1, Id(10), 29_999));
        var expired = disconnected.Apply(new(Id(1), WorldControllerEventKind.AllowanceExpired, 1, Id(10), 30_000), Id(11));
        Assert.Equal(new WorldControllerAssignment(Id(1), 2, WorldControllerMode.Autonomous, Id(10)), expired.Get(Id(1)));
        var reconnected = expired.Apply(new(Id(1), WorldControllerEventKind.Reconnected, 2, Id(10)), Id(12));
        Assert.Equal(new WorldControllerAssignment(Id(1), 3, WorldControllerMode.Human, null), reconnected.Get(Id(1)));
        Assert.Same(untouched, reconnected.Get(Id(2)));
        Assert.Equal(fresh.Assignments.Skip(1), reconnected.Assignments.Skip(1));
        var next = reconnected.Apply(Disconnect(revision: 3), Id(13));
        Assert.Throws<InvalidOperationException>(() => next.Apply(new(Id(1), WorldControllerEventKind.Reconnected, 4, Id(10)), Id(14)));
        var timely = next.Apply(new(Id(1), WorldControllerEventKind.Reconnected, 4, Id(13)), Id(14));
        Assert.Equal(5UL, timely.Get(Id(1)).Revision);
        Assert.Equal(WorldControllerMode.Human, timely.Get(Id(1)).Mode);
    }

    [Fact]
    public void Independent_disconnects_do_not_relabel_each_others_control_or_episode()
    {
        var first = WorldControllerState.Fresh([Id(2), Id(1)]).Apply(Disconnect(), Id(10));
        var second = first.Apply(Disconnect(actor: 2), Id(20));
        Assert.Same(first.Get(Id(1)), second.Get(Id(1)));
        Assert.Throws<InvalidOperationException>(() => second.Apply(new(Id(1), WorldControllerEventKind.AllowanceExpired,
            1, Id(20), 30_000), Id(30)));
        var joined = second.Apply(new(Id(1), WorldControllerEventKind.Reconnected, 1, Id(10)), Id(30));
        Assert.Equal(WorldControllerMode.Human, joined.Get(Id(1)).Mode);
        Assert.Same(second.Get(Id(2)), joined.Get(Id(2)));
        Assert.Equal(Id(20), joined.Get(Id(2)).DisconnectId);
    }

    [Fact]
    public void Stale_duplicate_impossible_and_current_identity_alias_events_are_refused()
    {
        var fresh = WorldControllerState.Fresh([Id(1), Id(2)]);
        Assert.Throws<ArgumentException>(() => fresh.Apply(Disconnect(), Id(2)));
        Assert.Throws<ArgumentException>(() => fresh.Apply(Disconnect(actor: 99), Id(10)));
        Assert.Throws<InvalidOperationException>(() => fresh.Apply(Disconnect(revision: 1), Id(10)));
        Assert.Throws<InvalidOperationException>(() => fresh.Apply(new(Id(1), WorldControllerEventKind.Reconnected, 0, Id(10)), Id(11)));
        var disconnected = fresh.Apply(Disconnect(), Id(10));
        Assert.Throws<InvalidOperationException>(() => disconnected.Apply(Disconnect(), Id(11)));
        Assert.Throws<InvalidOperationException>(() => disconnected.Apply(Disconnect(revision: 1), Id(11)));
        Assert.Throws<ArgumentException>(() => disconnected.Apply(Disconnect(actor: 2), Id(10)));
        var expired = disconnected.Apply(new(Id(1), WorldControllerEventKind.AllowanceExpired, 1, Id(10), ulong.MaxValue), Id(11));
        Assert.Throws<InvalidOperationException>(() => expired.Apply(new(Id(1), WorldControllerEventKind.AllowanceExpired, 2, Id(10), 30_000), Id(12)));
    }

    [Fact]
    public void Roster_is_bounded_sorted_copied_and_structural_checks_do_not_claim_history_authentication()
    {
        var input = new List<WorldControllerAssignment> { new(Id(2), 0, WorldControllerMode.Human, null), new(Id(1), 0, WorldControllerMode.Human, null) };
        var state = new WorldControllerState(input);
        input.Clear();
        Assert.Equal(new[] { Id(1), Id(2) }, state.Assignments.Select(row => row.ActorId));
        Assert.Throws<ArgumentException>(() => WorldControllerState.Fresh([]));
        Assert.Throws<ArgumentException>(() => WorldControllerState.Fresh([Id(1), Id(1)]));
        Assert.Throws<ArgumentException>(() => WorldControllerState.Fresh(Enumerable.Range(1, 5).Select(value => Id((ulong)value))));
        Assert.Throws<ArgumentException>(() => new WorldControllerState([
            new(Id(1), 1, WorldControllerMode.ReconnectAllowance, Id(10)), new(Id(2), 2, WorldControllerMode.Autonomous, Id(10))]));
        Assert.Throws<ArgumentException>(() => new WorldControllerState([
            new(Id(1), 1, WorldControllerMode.ReconnectAllowance, Id(2)), new(Id(2), 0, WorldControllerMode.Human, null)]));
        // A structurally lawful restored value still needs the world's authenticated journal.
        Assert.Equal(500UL, new WorldControllerAssignment(Id(1), 500, WorldControllerMode.Human, null).Revision);
    }

    [Theory]
    [InlineData(0UL, WorldControllerMode.ReconnectAllowance, true)]
    [InlineData(0UL, WorldControllerMode.Autonomous, true)]
    [InlineData(1UL, WorldControllerMode.Human, false)]
    [InlineData(1UL, WorldControllerMode.Autonomous, true)]
    [InlineData(2UL, WorldControllerMode.Human, true)]
    [InlineData(2UL, WorldControllerMode.ReconnectAllowance, false)]
    [InlineData(2UL, (WorldControllerMode)0, false)]
    public void Assignment_rejects_impossible_initial_modes_and_episode_pairing(ulong revision, WorldControllerMode mode, bool episode)
    {
        Assert.Throws<ArgumentException>(() => new WorldControllerAssignment(Id(1), revision, mode, episode ? Id(10) : null));
    }

    [Fact]
    public void Event_payloads_reject_unknown_kinds_reasons_and_extraneous_fields()
    {
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), (WorldControllerEventKind)0, 0));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), WorldControllerEventKind.Disconnected, 0));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), WorldControllerEventKind.Disconnected, 0,
            reason: (WorldControllerDisconnectReason)0));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), WorldControllerEventKind.Disconnected, 0,
            Id(10), reason: WorldControllerDisconnectReason.PeerClosed));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), WorldControllerEventKind.Disconnected, 0,
            elapsedHostMilliseconds: 1, reason: WorldControllerDisconnectReason.PeerClosed));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), WorldControllerEventKind.AllowanceExpired, 1,
            elapsedHostMilliseconds: 30_000));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), WorldControllerEventKind.AllowanceExpired, 1,
            Id(10), 30_000, WorldControllerDisconnectReason.PeerClosed));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), WorldControllerEventKind.Reconnected, 1, Id(10), 1));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(Id(1), WorldControllerEventKind.Reconnected, 1, Id(1)));
    }

    [Fact]
    public void Revision_overflow_refuses_without_changing_the_original_assignment()
    {
        var state = new WorldControllerState([new(Id(1), ulong.MaxValue, WorldControllerMode.Human, null)]);
        byte[] before = WorldControllerCodecV1.Encode(state);
        Assert.Throws<OverflowException>(() => state.Apply(Disconnect(revision: ulong.MaxValue), Id(10)));
        Assert.Equal(before, WorldControllerCodecV1.Encode(state));
    }
}
