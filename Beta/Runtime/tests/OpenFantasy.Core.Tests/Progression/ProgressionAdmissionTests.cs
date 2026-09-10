using OpenFantasy.Core.Progression;
using static OpenFantasy.Core.Tests.Progression.ProgressionFixture;

namespace OpenFantasy.Core.Tests.Progression;

public sealed class ProgressionAdmissionTests
{
    [Fact]
    public void ClassCalculationAppliesFlatModifiersBeforeSourceMinima()
    {
        ActorStatProfileDefinition profile = new(Id(100), "calculation", "Calculation", ActorStatProfileKind.Class,
            new(1, 1, 1, 1, 1, 1), new(-3, -3, -3, -3, -3, -3), new(5, 5, 5, 5, 5, 5));
        Assert.Equal(new ActorStatValues(3, 3, 3, 3, 3, 3), profile.Calculate(2));
        Assert.Equal(new ActorStatValues(1, 0, 1, 0, 0, 1), profile.Calculate(4));
        Assert.Throws<ArgumentException>(() => new ActorStatProfileDefinition(Id(1), "fixed", "Fixed",
            ActorStatProfileKind.Unassigned, new(1, 0, 0, 0, 0, 1), new(1, 0, 0, 0, 0, 0), ActorStatModifiers.Zero));
    }

    [Fact]
    public void UnnormalizedExperienceInvalidVitalsAndUnknownEnumsFailAdmission()
    {
        Assert.Throws<ArgumentException>(() => new ActorProgressionState(Id(10), 0, Squire(), Curve(), 1, 100, 60, 10));
        Assert.Throws<ArgumentException>(() => new ActorProgressionState(Id(10), 0, Squire(), Curve(), 0, 0, 60, 10));
        Assert.Throws<ArgumentException>(() => new ActorProgressionState(Id(10), 0, Squire(), Curve(), 11, 0, 60, 10));
        Assert.Throws<ArgumentException>(() => new ActorProgressionState(Id(10), 0, Squire(), Curve(), 1, 0, 61, 10));
        Assert.Throws<ArgumentException>(() => new ActorProgressionState(Id(10), 0, Squire(), Curve(), 1, 0, -1, 10));
        Assert.Throws<ArgumentException>(() => new ActorProgressionState(Id(10), 0, Squire(), Curve(), 1, 0, 60, 11));
        Assert.Throws<ArgumentException>(() => new CurrencyAccount(Id(200), (CurrencyAccountKind)99, null, 0, 0));
        Assert.Throws<ArgumentException>(() => new ActorStatProfileDefinition(Id(100), "unknown", "Unknown",
            (ActorStatProfileKind)99, new(1, 0, 0, 0, 0, 1), ActorStatModifiers.Zero, ActorStatModifiers.Zero));
    }

    [Fact]
    public void ThresholdsAreDefensivelyCopiedAndFiniteWithoutAUniversalLevelTenLimit()
    {
        ulong[] thresholds = [100, 282];
        LevelProgressionDefinition curve = new(Id(110), "small", thresholds);
        thresholds[0] = 1;
        Assert.Equal(100UL, curve.RequiredExperience(1));
        Assert.Throws<NotSupportedException>(() => ((IList<ulong>)curve.Thresholds)[0] = 1);
        Assert.Throws<ArgumentException>(() => new LevelProgressionDefinition(Id(110), "zero", [0]));
        Assert.Throws<ArgumentException>(() => new LevelProgressionDefinition(Id(110), "too-many", Enumerable.Repeat(1UL, 256)));
        var maximum = new LevelProgressionDefinition(Id(110), "maximum", Enumerable.Repeat(1UL, 255));
        Assert.Equal(256, maximum.MaximumLevel);
        Assert.Equal(maximum.Version, ProgressionCodecV1.DecodeCurve(ProgressionCodecV1.EncodeCurve(maximum)).Version);
        Assert.Equal(1, new LevelProgressionDefinition(Id(110), "capped", []).MaximumLevel);
    }

    [Fact]
    public void CurrencyAdmissionRequiresExactSupplyAndSeparateActorReserveIdentities()
    {
        var initial = Create();
        CurrencyAccount[] accounts = initial.Currency.Accounts.ToArray();
        Assert.Throws<ArgumentException>(() => new CurrencyLedger(Id(1), Id(2), 0, Gil(), 201, accounts));
        Assert.Throws<ArgumentException>(() => new CurrencyLedger(Id(1), Id(2), 0, Gil(), 200, accounts.Append(accounts[0])));
        Assert.Throws<ArgumentException>(() => new CurrencyAccount(Id(200), CurrencyAccountKind.Actor, null, 0, 0));
        Assert.Throws<ArgumentException>(() => new CurrencyAccount(Id(200), CurrencyAccountKind.Reserve, Id(10), 0, 0));
        Assert.Throws<ArgumentException>(() => new CurrencyAccount(Id(10), CurrencyAccountKind.Actor, Id(10), 0, 0));
        Assert.Throws<ArgumentException>(() => new CurrencyLedger(Id(1), Id(2), 0, Gil(), 200,
            accounts.Append(new CurrencyAccount(Id(203), CurrencyAccountKind.Actor, Id(10), 0, 0))));
        Assert.Throws<OverflowException>(() => new CurrencyLedger(Id(1), Id(2), 0, Gil(), ulong.MaxValue,
            [new(Id(200), CurrencyAccountKind.Reserve, null, ulong.MaxValue, 0),
             new(Id(201), CurrencyAccountKind.Actor, Id(10), 1, 0)]));
    }

    [Fact]
    public void CompleteActorJoinsPinsAndCrossKindIdentitiesAreRequired()
    {
        var initial = Create();
        initial.ValidateActorSet([Id(11), Id(10)]);
        Assert.Throws<ArgumentException>(() => initial.ValidateActorSet([Id(10)]));
        Assert.Throws<ArgumentException>(() => initial.ValidateActorSet([Id(10), Id(11), Id(10)]));
        Assert.Throws<ArgumentException>(() => new ProgressionSnapshot(Id(1), Id(3), 0,
            initial.Profiles, initial.Curves, initial.Actors, initial.Currency));
        Assert.Throws<ArgumentException>(() => new ProgressionSnapshot(Id(1), Id(2), 0,
            [Unassigned()], initial.Curves, initial.Actors, initial.Currency));
        ActorStatProfileDefinition changed = new(Id(100), "changed", "Changed", ActorStatProfileKind.Class,
            Squire().BaseStats, Squire().Growth, ActorStatModifiers.Zero);
        Assert.Throws<ArgumentException>(() => new ProgressionSnapshot(Id(1), Id(2), 0,
            [changed, Unassigned()], initial.Curves, initial.Actors, initial.Currency));
        ActorStatProfileDefinition alias = new(Id(10), "alias", "Alias", ActorStatProfileKind.Unassigned,
            Unassigned().BaseStats, ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        Assert.Throws<ArgumentException>(() => new ProgressionSnapshot(Id(1), Id(2), 0,
            initial.Profiles.Append(alias), initial.Curves, initial.Actors, initial.Currency));
    }

    [Fact]
    public void PermutedInputsProduceIdenticalCanonicalSnapshotsAndCollectionsCannotMutate()
    {
        var original = Create();
        var ledger = new CurrencyLedger(Id(1), Id(2), 0, Gil(), 200, original.Currency.Accounts.Reverse());
        ActorProgressionState[] actors = original.Actors.Reverse().ToArray();
        var permuted = new ProgressionSnapshot(Id(1), Id(2), 0, original.Profiles.Reverse(),
            original.Curves.Reverse(), actors, ledger);
        actors[0] = original.GetActor(Id(10));
        Assert.Equal(original.ContentHash, permuted.ContentHash);
        Assert.Equal(2, permuted.Actors.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<ActorProgressionState>)permuted.Actors).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<CurrencyAccount>)permuted.Currency.Accounts).Clear());
    }
}
