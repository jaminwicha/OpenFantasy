using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.OrdinaryCareFixture;

namespace OpenFantasy.Core.Tests.Care;

public sealed class OrdinaryCareModelTests
{
    [Fact]
    public void Forms_require_exact_definition_and_version_without_inventing_anatomy()
    {
        var definition = Definition();
        Assert.True(definition.SupportsForm(Form.DefinitionId, Form.Version));
        Assert.False(definition.SupportsForm(Id(901), Form.Version));
        Assert.False(definition.SupportsForm(Form.DefinitionId, ContentHash.Compute("different form"u8)));
        Assert.Throws<ArgumentException>(() => Definition(forms: []));
        Assert.Throws<ArgumentException>(() => Definition(forms: [Form, Form]));
        Assert.Throws<ArgumentException>(() => Definition(milliseconds: 0));
        Assert.Throws<ArgumentException>(() => new RestorativeUseDefinition(Id(500), Id(100), Remedy().Version,
            (RestorativeDeliveryMode)3, [Form]));
        Assert.Equal(1_800UL, definition.ExplorationMilliseconds);
        Assert.NotEqual(definition.Version, Definition(true).Version);
    }

    [Fact]
    public void Historical_assistance_remains_valid_after_later_revocation_in_the_same_step()
    {
        var initial = Ordinary();
        var use = Use();
        var used = initial.RecordUse(use);
        var revoked = used.Revoke(Revocation());
        Assert.Equal(0UL, initial.Revision);
        Assert.Equal(1UL, used.Revision);
        Assert.Equal(2UL, revoked.Revision);
        Assert.True(initial.GetPermission(Id(700)).IsActive);
        Assert.False(revoked.GetPermission(Id(700)).IsActive);
        Assert.Equal(initial.GetPermission(Id(700)).GrantVersion, revoked.GetPermission(Id(700)).GrantVersion);
        Assert.NotEqual(initial.GetPermission(Id(700)).Version, revoked.GetPermission(Id(700)).Version);
        Assert.Same(use, Assert.Single(revoked.Uses));
        var complete = CreateOrdinary(revoked);
        Assert.Equal(2UL, complete.Revision);
        Assert.Equal(5UL, use.PatientRevisionAfter);
        Assert.Null(complete.Pending);
    }

    [Fact]
    public void Revoked_permission_cannot_authorize_later_or_same_position_assistance()
    {
        var revoked = Revocation(20, 0);
        Assert.Throws<ArgumentException>(() => Ordinary([Use(ordinal: 1)], revoked));
        Assert.Throws<ArgumentException>(() => Ordinary([Use()], revoked));
        var state = Ordinary().Revoke(revoked);
        Assert.Throws<ArgumentException>(() => state.RecordUse(Use(sequence: 21)));
        Assert.Throws<InvalidOperationException>(() => state.Revoke(Revocation(21)));
    }

    [Fact]
    public void Self_use_has_no_external_permission_and_can_have_no_aftermath_agreement()
    {
        var definition = Definition(true, 501);
        var use = Use(self: true, hpBefore: 55, hpAfter: 60);
        var ordinary = new OrdinaryCareSnapshot([definition], [], [use]);
        var care = new CareSnapshot(World, Branch, 1, [Remedy()], [], [], [], ordinary);
        Assert.Null(use.PermissionId);
        Assert.Null(use.PermissionGrantVersion);
        Assert.Null(use.PermissionRevision);
        Assert.Empty(care.Agreements);
        Assert.Empty(care.Acknowledgments);
        Assert.Empty(care.Treatments);
        Assert.Equal(60, Assert.Single(care.Ordinary!.Uses).HpAfter);
        Assert.Throws<ArgumentException>(() => new CareSnapshot(World, Branch, 0, [Remedy()], [], [], []));
    }

    [Theory]
    [InlineData(0, 40)]
    [InlineData(10, 10)]
    [InlineData(10, 61)]
    [InlineData(60, 60)]
    public void Ordinary_use_refuses_incapacitated_no_effect_or_uncapped_resource_witness(int before, int after)
    {
        Assert.Throws<ArgumentException>(() => Use(hpBefore: before, hpAfter: after));
    }

    [Fact]
    public void Positive_arbitrary_restoration_is_not_the_pinned_remedy_effect()
    {
        var structurallyPositive = Use(hpBefore: 10, hpAfter: 49);
        var ordinary = Ordinary([structurallyPositive]);
        Assert.Throws<ArgumentException>(() => CreateOrdinary(ordinary));
        Assert.Throws<ArgumentException>(() => Use(patientRevision: ulong.MaxValue));
        Assert.Throws<ArgumentException>(() => Use(ordinal: 64));
    }

    [Fact]
    public void Ordinary_history_cannot_reuse_commands_or_insert_a_prior_position()
    {
        var first = Use();
        Assert.Throws<ArgumentException>(() => Ordinary([first, Use(command: 10_002)]));
        Assert.Throws<ArgumentException>(() => Ordinary([first], Revocation(sequence: 21, command: 10_000)));
        Assert.Throws<ArgumentException>(() => Ordinary([first]).RecordUse(Use(sequence: 19, command: 10_002)));
        Assert.Throws<ArgumentException>(() => Ordinary([first]).Revoke(Revocation(sequence: 19)));
    }

    [Fact]
    public void Aftermath_pending_interval_excludes_ordinary_work_and_all_commands_are_distinct()
    {
        var acknowledgment = Acknowledgment();
        var pending = CareModelFixture.Create([acknowledgment]);
        Assert.Throws<ArgumentException>(() => CreateOrdinary(Ordinary([Use()]), pending));
        Assert.Throws<ArgumentException>(() => CreateOrdinary(Ordinary(revocation: Revocation()), pending));
        var completed = CareModelFixture.Create([acknowledgment], [Treatment(acknowledgment)]);
        Assert.NotNull(CreateOrdinary(Ordinary([Use()]), completed));
        Assert.Throws<ArgumentException>(() => CreateOrdinary(Ordinary([Use(sequence: 11)]), completed));
        Assert.Throws<ArgumentException>(() => CreateOrdinary(Ordinary([Use(command: 2_000)]), completed));
        var distantTreatment = CareModelFixture.Create([acknowledgment], [Treatment(acknowledgment, sequence: 21)]);
        Assert.Throws<ArgumentException>(() => CreateOrdinary(Ordinary([Use()]), distantTreatment));
    }

    [Fact]
    public void Permission_requires_actual_distinct_participants_exact_definition_and_patient_revocation()
    {
        var definition = Definition();
        Assert.Throws<ArgumentException>(() => new CarePermission(Id(700), Patient, Patient,
            definition.DefinitionId, definition.Version));
        var wrong = new CarePermission(Id(700), Patient, Helper, definition.DefinitionId, ContentHash.Compute("changed use"u8));
        Assert.Throws<ArgumentException>(() => new OrdinaryCareSnapshot([definition], [wrong], []));
        var wrongPatient = new CarePermissionRevocation(EntityId.DeriveSpawned(Id(10_001), 0), Id(10_001), Id(700), Id(99), 20, 0);
        Assert.Throws<ArgumentException>(() => Permission(wrongPatient));
        var self = Definition(true, 501);
        Assert.Throws<ArgumentException>(() => new OrdinaryCareSnapshot([self],
            [new CarePermission(Id(700), Patient, Helper, self.DefinitionId, self.Version)], []));
    }

    [Fact]
    public void Selected_permission_tuple_is_complete_and_retains_stale_values_for_authority_rejection()
    {
        var definition = Definition();
        var permission = Permission();
        var ingredient = new IngredientRequest(IngredientLotKind.Source, Id(800), 2, 1);
        Assert.Throws<ArgumentException>(() => new UseRestorativeItemIntent(Patient, definition.DefinitionId,
            definition.Version, permission.PermissionId, null, 0, 1, 2, 3, 4, ingredient));
        var selected = new UseRestorativeItemIntent(Patient, definition.DefinitionId, definition.Version,
            permission.PermissionId, permission.GrantVersion, 77, 1, 2, 3, 4, ingredient);
        Assert.Equal(77UL, selected.ExpectedPermissionRevision);
        Assert.Same(ingredient, selected.Ingredient);
        var revoke = new RevokeCarePermissionIntent(permission.PermissionId, 77, permission.GrantVersion);
        Assert.Equal(77UL, revoke.ExpectedPermissionRevision);
    }
}
