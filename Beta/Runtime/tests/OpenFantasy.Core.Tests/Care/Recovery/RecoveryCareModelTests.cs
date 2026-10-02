using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.Recovery.RecoveryCareFixture;

namespace OpenFantasy.Core.Tests.Care.Recovery;

/// <summary>Structural recovery evidence; world policy, actual HP transitions and finite supply are tested by the world owner.</summary>
public sealed class RecoveryCareModelTests
{
    [Fact]
    public void Recovery_has_an_exact_distinct_catalog_and_permission_without_aftermath_history()
    {
        var state = RecoveryCareFixture.Create([Use()]);
        Assert.True(state.Ordinary!.SupportsIncapacitatedAssistance);
        Assert.Empty(state.Agreements);
        Assert.Empty(state.Acknowledgments);
        Assert.Empty(state.Treatments);
        Assert.Equal(1_800UL, Assert.Single(state.Ordinary.Uses).ExplorationMilliseconds);
        Assert.True(Definition().SupportsForm(OrdinaryCareFixture.Form.DefinitionId, OrdinaryCareFixture.Form.Version));
        Assert.False(Definition().SupportsForm(OrdinaryCareFixture.Form.DefinitionId, ContentHash.Compute("different form"u8)));
        Assert.Throws<ArgumentException>(() => RestorativeUseDefinition.CreateIncapacitatedAssistance(Id(502),
            Remedy().DefinitionId, Remedy().Version, []));
        Assert.Throws<ArgumentException>(() => RestorativeUseDefinition.CreateIncapacitatedAssistance(Id(502),
            Remedy().DefinitionId, Remedy().Version, [OrdinaryCareFixture.Form], 0));
        var living = new RestorativeUseDefinition(Definition().DefinitionId, Remedy().DefinitionId, Remedy().Version,
            RestorativeDeliveryMode.LivingAssistance, [OrdinaryCareFixture.Form]);
        Assert.NotEqual(living.Version, Definition().Version);
        var wrongPermission = new CarePermission(Permission().PermissionId, Patient, Helper, living.DefinitionId, living.Version);
        Assert.Throws<ArgumentException>(() => new OrdinaryCareSnapshot([Definition()], [wrongPermission], []));
        Assert.Throws<ArgumentException>(() => new OrdinaryCareSnapshot([Definition()], [Permission()],
            [Use(grant: OrdinaryCareFixture.Permission())]));
    }

    [Theory]
    [InlineData("self")]
    [InlineData("permission")]
    [InlineData("positive-before")]
    [InlineData("negative-before")]
    [InlineData("zero-after")]
    [InlineData("above-cap")]
    [InlineData("duration")]
    public void Recovery_factory_requires_zero_hp_separate_supplier_full_permission_and_positive_capped_effect(string invalid)
    {
        Assert.Throws<ArgumentException>(() => invalid switch
        {
            "self" => Use(self: true),
            "permission" => Use(permission: false),
            "positive-before" => Use(before: 1),
            "negative-before" => Use(before: -1),
            "zero-after" => Use(after: 0),
            "above-cap" => Use(after: 61),
            "duration" => Use(duration: 0),
            _ => throw new InvalidOperationException()
        });
    }

    [Fact]
    public void Living_constructor_and_catalog_cannot_be_reinterpreted_as_recovery()
    {
        Assert.Throws<ArgumentException>(() => new RestorativeUseDefinition(Definition().DefinitionId, Remedy().DefinitionId,
            Remedy().Version, RestorativeDeliveryMode.IncapacitatedAssistance, [OrdinaryCareFixture.Form]));
        Assert.Throws<ArgumentException>(() => OrdinaryCareFixture.Use(hpBefore: 0, hpAfter: 40,
            definition: Definition(), permission: Permission()));
        var livingWitness = OrdinaryCareFixture.Use(definition: Definition(), permission: Permission());
        Assert.Throws<ArgumentException>(() => new OrdinaryCareSnapshot([Definition()], [Permission()], [livingWitness]));
        var zeroWitness = Use(definition: OrdinaryCareFixture.Definition(), grant: OrdinaryCareFixture.Permission());
        Assert.Throws<ArgumentException>(() => OrdinaryCareFixture.Ordinary([zeroWitness]));
    }

    [Fact]
    public void Recovery_effect_is_exactly_the_remedy_cap_and_not_an_arbitrary_positive_hp_grant()
    {
        Assert.Equal(25, Assert.Single(RecoveryCareFixture.Create([Use(maximum: 25, after: 25)]).Ordinary!.Uses).HpAfter);
        Assert.Throws<ArgumentException>(() => RecoveryCareFixture.Create([Use(after: 39)]));
    }

    [Fact]
    public void Repeated_real_cuts_preserve_recovery_totals_revocation_and_closed_chronology()
    {
        CareSnapshot first = RecoveryCareFixture.Create([Use()]);
        CareContinuationContext cut = CareContinuationContext.Capture(first);
        CareSnapshot continued = cut.Continue();
        cut.ValidateClosure(first, continued);
        Assert.Equal((1UL, 1UL), (continued.Revision, continued.TotalOrdinaryUses));
        Assert.Empty(continued.Ordinary!.Uses);
        Assert.True(continued.Ordinary.SupportsIncapacitatedAssistance);
        Assert.Throws<ArgumentException>(() => continued.Ordinary.RecordUse(Use(command: 50_010)));
        CareSnapshot twice = continued.WithOrdinary(continued.Ordinary.RecordUse(Use(sequence: 21, command: 50_010)));
        CareSnapshot revoked = twice.WithOrdinary(twice.Ordinary!.Revoke(Revoke(sequence: 21, ordinal: 1)));
        var secondCut = CareContinuationContext.Capture(revoked);
        CareSnapshot final = secondCut.Continue();
        Assert.Equal((3UL, 2UL, 2UL), (final.Revision, final.TotalOrdinaryUses, final.Continuation!.Ordinal));
        Assert.False(Assert.Single(final.Ordinary!.Permissions).IsActive);
        Assert.Throws<ArgumentException>(() => final.Ordinary.RecordUse(Use(sequence: 22, command: 50_020)));
        Assert.Throws<InvalidOperationException>(() => CareContinuationContext.Capture(final));
        byte[] bytes = CareCodec.Encode(final);
        Assert.Equal(bytes, CareCodec.Encode(CareCodec.Decode(bytes, secondCut)));
        Assert.Throws<FormatException>(() => CareCodec.Decode(bytes, cut));
    }

    [Fact]
    public void Recovery_does_not_bypass_an_existing_aftermath_obligation()
    {
        var pending = CareModelFixture.Create([Acknowledgment()]);
        Assert.Throws<ArgumentException>(() => new CareSnapshot(World, Branch, 2, pending.Remedies, pending.Agreements,
            pending.Acknowledgments, [], RecoveryCareFixture.Create([Use()]).Ordinary));
    }
}
