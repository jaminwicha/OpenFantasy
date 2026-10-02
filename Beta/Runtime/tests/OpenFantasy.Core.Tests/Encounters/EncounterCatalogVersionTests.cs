using System.Text;
using OpenFantasy.Core.Encounters;
using static OpenFantasy.Core.Tests.Encounters.EncounterLifecycleFixture;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterCatalogVersionTests
{
    [Theory]
    [InlineData(1, 8)]
    [InlineData(2, 16)]
    public void Each_version_retains_its_own_bounded_enumeration(int version, int capacity)
    {
        int seen = 0;
        IEnumerable<EncounterScenarioDefinition> Scenarios()
        {
            for (ulong i = 0; ; i++) { seen++; yield return Scenario(500 + i); }
        }
        Assert.Throws<ArgumentException>(() => EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed,
            Abilities, Scenarios(), EncounterLifecycleFixture.Actors, catalogVersion: version));
        Assert.Equal(capacity + 1, seen);
        Assert.Equal(capacity, FreshCatalog(version, capacity).Scenarios.Count);
    }

    [Fact]
    public void Default_catalog_keeps_the_explicit_version_one_archive_exactly()
    {
        var old = Fresh();
        var explicitVersion = EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed, Abilities,
            old.Scenarios, EncounterLifecycleFixture.Actors, catalogVersion: 1);
        Assert.Equal(1, old.CatalogVersion);
        Assert.Equal(EncounterLifecycleCodecV1.Encode(old), EncounterLifecycleCodecV1.Encode(explicitVersion));
        Assert.Equal("encounter.lifecycle.v1", Encoding.UTF8.GetString(Fields(EncounterLifecycleCodecV1.Encode(old))[0]));
        Assert.Throws<ArgumentException>(() => FreshCatalog(1, 9));
    }

    [Fact]
    public void Version_two_roundtrips_full_capacity_and_canonicalizes_input_order()
    {
        var state = FreshCatalog(2, 16);
        var reverse = EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed, Abilities.Reverse(),
            state.Scenarios.Reverse(), EncounterLifecycleFixture.Actors.Reverse(), catalogVersion: 2);
        Assert.Equal(state.ContentHash, reverse.ContentHash);
        Assert.Equal("encounter.lifecycle.v2", Encoding.UTF8.GetString(Fields(EncounterLifecycleCodecV1.Encode(state))[0]));
        Assert.Equal(state.ContentHash, Restore(state).ContentHash);
        Assert.Equal(2, Restore(state).CatalogVersion);
        Assert.NotEqual(FreshCatalog(1, 8).ContentHash, FreshCatalog(2, 8).ContentHash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Unsupported_versions_are_rejected_before_catalog_enumeration(int version)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FreshCatalog(version, 1));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    public void Wire_tag_and_version_must_name_the_same_supported_contract(int tag, int version)
    {
        var fields = Fields(EncounterLifecycleCodecV1.Encode(FreshCatalog(2, 12)));
        fields[0] = Encoding.UTF8.GetBytes($"encounter.lifecycle.v{tag}");
        Number(fields[1], (ulong)version);
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(Frame(fields), World, Branch, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(2, 16)]
    public void Decoder_rejects_one_extra_otherwise_valid_scenario(int version, int capacity)
    {
        var fields = Fields(EncounterLifecycleCodecV1.Encode(FreshCatalog(version, capacity)));
        var catalog = Fields(fields[12]);
        var extra = Fields(EncounterLifecycleCodecV1.Encode(EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed,
            Abilities, [Scenario(999)], EncounterLifecycleFixture.Actors)));
        catalog.Add(Fields(extra[12])[1]);
        fields[12] = Frame(catalog);
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(Frame(fields), World, Branch, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Extended_catalog_survives_entry_staging_outcome_release_and_exploration()
    {
        var state = FreshCatalog(2, 12).RetainBoundary().AdvanceExploration()
            .Begin(Id(1_000), Id(500)).AdvanceStaging().AdvanceStaging();
        var timing = Timing(state);
        var command = EncounterActionFixture.Command(scope: timing.Scope);
        timing = timing.QueueDecision(timing.Revision,
            new(command.ActorId, command.CommandId, EncounterDecisionKind.Action, command.ContentHash));
        state = state.ObserveActive(timing, state.Actions!.WithPending([command]), tacticalTick: false, isOutcome: false);
        state = Complete(state, timing);
        Assert.Equal(2, Restore(state).CatalogVersion);
        state = Restore(state).Release().AdvanceExploration().RetainBoundary();
        Assert.Equal(2, state.CatalogVersion);
        Assert.Equal(12, state.Scenarios.Count);
        Assert.Equal(state.ContentHash, Restore(state).ContentHash);
    }

    [Fact]
    public void Archive_byte_budget_does_not_grow_with_catalog_capacity()
    {
        Assert.Equal(512 * 1_024, EncounterLifecycleCodecV1.MaximumEncodedByteLength);
        var oversized = new byte[EncounterLifecycleCodecV1.MaximumEncodedByteLength + 1];
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(oversized, World, Branch, TestContext.Current.CancellationToken));
    }

    private static EncounterLifecycleSnapshot FreshCatalog(int version, int count) =>
        EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed, Abilities,
            Enumerable.Range(0, count).Select(i => Scenario((ulong)(500 + i))), EncounterLifecycleFixture.Actors, catalogVersion: version);
}
