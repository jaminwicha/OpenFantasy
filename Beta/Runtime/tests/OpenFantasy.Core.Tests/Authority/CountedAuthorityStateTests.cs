using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Tests.Authority;

public sealed class CountedAuthorityStateTests
{
    [Fact]
    public void Source_admission_requires_positive_finite_budgets_and_exact_definition_pins()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdmittedCountedSource(AuthorityFixture.Id(6), AuthorityFixture.Wood, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Replace(state, capacity: 0));
        Assert.Throws<ArgumentException>(() => Replace(state, sources: []));
        Assert.Throws<ArgumentException>(() => Replace(state, sources: [new(AuthorityFixture.Id(6), AuthorityFixture.Wood, 17)]));
        var differentPin = new CountedItemDefinition(AuthorityFixture.Wood.DefinitionId, ContentHash.Compute("different"u8));
        Assert.Throws<ArgumentException>(() => Replace(state, sources: [new(AuthorityFixture.Id(6), differentPin, 20)]));
        Assert.Throws<ArgumentException>(() => Replace(state, sources:
            [state.Sources[0], new(AuthorityFixture.Id(7), differentPin, 20)]));
        Assert.Throws<ArgumentException>(() => Replace(state, capacity: 19));
        Assert.Throws<ArgumentException>(() => Replace(state, capacity: ulong.MaxValue, sources:
            [new(AuthorityFixture.Id(6), AuthorityFixture.Wood, ulong.MaxValue), new(AuthorityFixture.Id(7), AuthorityFixture.Wood, 1)]));
    }

    [Fact]
    public void Every_storage_has_one_admitted_owner_and_positive_capacity()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        Assert.Throws<ArgumentException>(() => Replace(state, policies: [state.Policies[0]]));
        Assert.Throws<ArgumentException>(() => Replace(state, policies: [state.Policies[0], state.Policies[0]]));
        Assert.Throws<ArgumentException>(() => Replace(state, policies:
            [state.Policies[0], new(AuthorityFixture.Id(4), AuthorityFixture.Id(99), CustodyDepositRule.OwnerOnly)]));
        Assert.Throws<ArgumentException>(() => Replace(state, policies:
            [state.Policies[0], new(AuthorityFixture.Id(99), AuthorityFixture.Id(24), CustodyDepositRule.OwnerOnly)]));
        Assert.Throws<ArgumentException>(() => AuthorityFixture.Create(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CustodianAccessPolicy(AuthorityFixture.Id(3),
            AuthorityFixture.Id(23), (CustodyDepositRule)3));
    }

    [Fact]
    public void Actor_control_is_unique_and_sessions_cannot_name_multiple_principals()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        Assert.Throws<ArgumentException>(() => Replace(state, controls: [state.Controls[0], state.Controls[0]]));
        Assert.Throws<ArgumentException>(() => Replace(state, controls: [new(AuthorityFixture.Id(21), AuthorityFixture.Id(22), AuthorityFixture.Id(99))]));
        Assert.Throws<ArgumentException>(() => Replace(state, controls:
            [state.Controls[0], new(AuthorityFixture.Id(31), AuthorityFixture.Id(22), AuthorityFixture.Id(24))]));
        CountedAuthorityState party = Replace(state, controls:
            [state.Controls[0], new(AuthorityFixture.Id(21), AuthorityFixture.Id(22), AuthorityFixture.Id(24))]);
        Assert.True(party.CanControl(AuthorityFixture.Delivery, AuthorityFixture.Id(24)));
    }

    [Fact]
    public void All_known_identity_kinds_are_disjoint_including_unused_sources_and_reservations()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        foreach (EntityId identity in new[] { AuthorityFixture.Id(1), AuthorityFixture.Id(2), AuthorityFixture.Id(3),
            AuthorityFixture.Id(5), AuthorityFixture.Id(6), AuthorityFixture.Id(10), AuthorityFixture.Id(21), AuthorityFixture.Id(22) })
        {
            Assert.Throws<ArgumentException>(() => Replace(state, actors: [.. state.Actors, identity]));
        }
        Assert.Throws<ArgumentException>(() => Replace(state, reservationIds: [AuthorityFixture.Id(23)]));
        Assert.Throws<ArgumentException>(() => Replace(state, sources:
            [state.Sources[0], new(AuthorityFixture.Id(23), AuthorityFixture.Wood, 1)]));
        var sameWorldAndBranch = new CustodySnapshot(state.WorldId, state.WorldId, state.Custody.Revision,
            state.Custody.Storages, state.Custody.Lots);
        Assert.Throws<ArgumentException>(() => Replace(state, custody: sameWorldAndBranch));
        CountedAuthorityState reserved = AuthorityFixture.Create(reservedBy: AuthorityFixture.Id(80));
        Assert.Throws<ArgumentException>(() => Replace(reserved, reservationIds: []));
        Assert.True(reserved.ContainsIdentity(AuthorityFixture.Id(80)));
    }

    [Fact]
    public void Collections_are_defensively_copied_and_permutations_encode_identically()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        EntityId[] actors = state.Actors.Reverse().ToArray();
        ActorControlBinding[] controls = state.Controls.ToArray();
        CustodianAccessPolicy[] policies = state.Policies.Reverse().ToArray();
        AdmittedCountedSource[] sources = state.Sources.ToArray();
        CountedAuthorityState copied = new(state.Custody, state.AdmittedUnitCapacity, actors, controls, policies, sources);
        actors[0] = AuthorityFixture.Id(90);
        controls[0] = new(AuthorityFixture.Id(91), AuthorityFixture.Id(92), AuthorityFixture.Id(93));
        policies[0] = new(AuthorityFixture.Id(94), AuthorityFixture.Id(95), CustodyDepositRule.OwnerOnly);
        sources[0] = new(AuthorityFixture.Id(96), AuthorityFixture.Wood, 100);
        Assert.Equal(AuthorityEncodingV1.Encode(state), AuthorityEncodingV1.Encode(copied));
        Assert.Throws<NotSupportedException>(() => ((IList<EntityId>)copied.Actors)[0] = AuthorityFixture.Id(90));
        Assert.Throws<NotSupportedException>(() => ((IList<ActorControlBinding>)copied.Controls).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<CustodianAccessPolicy>)copied.Policies).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<AdmittedCountedSource>)copied.Sources).Clear());
    }

    [Fact]
    public void Maximum_profile_counts_roundtrip_and_one_extra_rejects_without_truncation()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        EntityId[] actors = [.. state.Actors, .. Enumerable.Range(0, CountedAuthorityState.MaximumActors - 2)
            .Select(index => AuthorityFixture.Id((ulong)index + 1_000_000))];
        ActorControlBinding[] controls = actors.Select(actor => new ActorControlBinding(AuthorityFixture.Id(21), AuthorityFixture.Id(22), actor)).ToArray();
        AdmittedCountedSource[] sources = [.. state.Sources, .. Enumerable.Range(0, CountedAuthorityState.MaximumSources - 1)
            .Select(index => new AdmittedCountedSource(AuthorityFixture.Id((ulong)index + 3_000_000), AuthorityFixture.Wood, 1))];
        EntityId[] reservations = Enumerable.Range(0, CountedAuthorityState.MaximumReservations)
            .Select(index => AuthorityFixture.Id((ulong)index + 2_000_000)).ToArray();
        CountedAuthorityState maximum = Replace(state, actors: actors, controls: controls, sources: sources,
            reservationIds: reservations, capacity: 2048);
        byte[] encoded = AuthorityEncodingV1.Encode(maximum);
        Assert.Equal(encoded, AuthorityEncodingV1.Encode(AuthorityEncodingV1.Decode(encoded)));
        Assert.Throws<ArgumentException>(() => Replace(maximum, actors: [.. actors, AuthorityFixture.Id(99)]));
        Assert.Throws<ArgumentException>(() => Replace(maximum, controls: [.. controls, new(AuthorityFixture.Id(21), AuthorityFixture.Id(22), actors[0])]));
        Assert.Throws<ArgumentException>(() => Replace(maximum, sources: [.. sources, new(AuthorityFixture.Id(99), AuthorityFixture.Wood, 1)]));
        Assert.Throws<ArgumentException>(() => Replace(maximum, reservationIds: [.. reservations, AuthorityFixture.Id(99)]));
    }

    [Fact]
    public void Over_limit_identity_enumeration_stops_at_first_excess_entry()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        int read = 0;
        IEnumerable<EntityId> Excess()
        {
            for (int index = 0; index <= CountedAuthorityState.MaximumActors; index++)
            {
                read++;
                yield return AuthorityFixture.Id((ulong)index + 1_000_000);
            }
            throw new InvalidOperationException("Enumeration continued past the first excess actor.");
        }
        Assert.Throws<ArgumentException>(() => Replace(state, actors: Excess()));
        Assert.Equal(CountedAuthorityState.MaximumActors + 1, read);
    }

    private static CountedAuthorityState Replace(CountedAuthorityState state, CustodySnapshot? custody = null,
        ulong? capacity = null, IEnumerable<EntityId>? actors = null, IEnumerable<ActorControlBinding>? controls = null,
        IEnumerable<CustodianAccessPolicy>? policies = null, IEnumerable<AdmittedCountedSource>? sources = null,
        IEnumerable<EntityId>? reservationIds = null) => new(custody ?? state.Custody, capacity ?? state.AdmittedUnitCapacity,
            actors ?? state.Actors, controls ?? state.Controls, policies ?? state.Policies, sources ?? state.Sources,
            reservationIds ?? state.ReservationIds);
}
