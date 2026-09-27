using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;

namespace OpenFantasy.Core.Tests.Care;

public sealed class CareCatalogWitnessTests
{
    [Fact]
    public void Catalogs_require_unique_entries_and_exact_remedy_pins()
    {
        var remedy = Remedy();
        var agreement = Agreement();
        Assert.Throws<ArgumentException>(() => Create(remedies: []));
        Assert.Throws<ArgumentException>(() => Create(agreements: []));
        Assert.Throws<ArgumentException>(() => Create(remedies: [remedy, remedy]));
        Assert.Throws<ArgumentException>(() => Create(agreements: [agreement, agreement]));
        Assert.Throws<ArgumentException>(() => Create(agreements: [Agreement(remedy: Remedy(103))]));
        Assert.Throws<ArgumentException>(() => Create(agreements:
            [new(agreement.AgreementId, Patient, Helper, remedy.DefinitionId, ContentHash.Compute("wrong remedy pin"u8))]));
        Assert.Throws<ArgumentException>(() => new CareSnapshot(World, World, 0, [remedy], [agreement], [], []));
        Assert.Throws<ArgumentNullException>(() => Create(remedies: [null!]));
        Assert.Throws<ArgumentNullException>(() => Create(agreements: [null!]));
    }

    [Fact]
    public void Remedy_and_agreement_versions_pin_every_treatment_and_permission_value()
    {
        var remedy = Remedy();
        var agreement = Agreement();
        Assert.NotEqual(remedy.Version, Remedy(hp: 41).Version);
        Assert.NotEqual(remedy.Version, Remedy(range: 255).Version);
        Assert.NotEqual(remedy.Version, new RestorativeRemedyDefinition(remedy.DefinitionId,
            new CountedItemDefinition(remedy.Item.DefinitionId, ContentHash.Compute("other item"u8)), 40, 256).Version);
        Assert.NotEqual(agreement.Version, Agreement(patient: Id(13)).Version);
        Assert.NotEqual(agreement.Version, new CareAgreement(agreement.AgreementId, Patient, Id(14),
            remedy.DefinitionId, remedy.Version).Version);
        var changedRemedy = Remedy(hp: 41);
        Assert.NotEqual(Create().ContentHash, Create(remedies: [changedRemedy],
            agreements: [Agreement(remedy: changedRemedy)]).ContentHash);
    }

    [Theory]
    [InlineData(0U, 256UL)]
    [InlineData(uint.MaxValue, 256UL)]
    [InlineData(40U, 0UL)]
    [InlineData(40U, 4_097UL)]
    public void Remedies_reject_zero_or_out_of_bound_restoration_and_reach(uint hp, ulong range)
    {
        Assert.Throws<ArgumentException>(() => Remedy(hp: hp, range: range));
    }

    [Fact]
    public void Definitions_and_permission_participants_require_distinct_roles()
    {
        var remedy = Remedy();
        Assert.Throws<ArgumentException>(() => new RestorativeRemedyDefinition(remedy.Item.DefinitionId, remedy.Item, 40, 256));
        foreach (EntityId alias in new[] { Patient, Helper, remedy.DefinitionId })
            Assert.Throws<ArgumentException>(() => new CareAgreement(alias, Patient, Helper, remedy.DefinitionId, remedy.Version));
        Assert.Throws<ArgumentException>(() => new CareAgreement(Id(200), Patient, Patient, remedy.DefinitionId, remedy.Version));
    }

    [Fact]
    public void Acknowledgment_requires_derived_identity_and_an_earlier_actual_lethal_result()
    {
        var command = Id(2_000);
        var agreement = Agreement();
        var defeat = Defeat();
        Assert.Throws<ArgumentException>(() => new CareAcknowledgment(Id(99), command, agreement.AgreementId,
            Id(90), 11, ActionHash, TimingHash, defeat));
        foreach (ulong sequence in new[] { 9UL, 10UL })
            Assert.Throws<ArgumentException>(() => Acknowledgment(sequence));
        Assert.Throws<ArgumentException>(() => Acknowledgment(defeat: Defeat(afterHp: 1)));
        var wait = new EncounterActionResolution(10, 7, Id(12), Id(1_000), null, null,
            EncounterActionResolutionStatus.Wait, 0, 0, 0, 0, 100, 0, []);
        Assert.Throws<ArgumentException>(() => Acknowledgment(defeat: wait));
    }

    [Theory]
    [InlineData(1U, 10_000UL)]
    [InlineData(3U, 3_334UL)]
    [InlineData(80U, 125UL)]
    [InlineData(uint.MaxValue, 1UL)]
    public void Treatment_charges_exact_positive_speed_normalized_standard_debt(uint speed, ulong expectedDelay)
    {
        var acknowledgment = Acknowledgment();
        var treatment = Treatment(acknowledgment, speed: speed, delay: expectedDelay);
        Assert.Equal(expectedDelay, treatment.HelperDelay);
        Assert.Throws<ArgumentException>(() => Treatment(acknowledgment, speed: speed, delay: expectedDelay - 1));
        Assert.Throws<ArgumentException>(() => Treatment(acknowledgment, speed: speed, delay: expectedDelay + 1));
    }

    [Theory]
    [InlineData("treatment-id")]
    [InlineData("consumption-id")]
    [InlineData("resource-overflow")]
    [InlineData("zero-hp")]
    [InlineData("over-max-hp")]
    [InlineData("zero-speed")]
    public void Treatment_witness_refuses_invalid_identity_resource_and_debt_fields(string invalid)
    {
        var acknowledgment = Acknowledgment();
        var command = Id(3_000);
        Assert.Throws<ArgumentException>(() => new CareTreatment(
            invalid == "treatment-id" ? Id(99) : EntityId.DeriveSpawned(command, 1), command, acknowledgment.AcknowledgmentId,
            invalid == "consumption-id" ? Id(98) : EntityId.DeriveSpawned(command, 0), 12, 8,
            invalid == "resource-overflow" ? ulong.MaxValue : 3, 100,
            invalid == "zero-hp" ? 0 : invalid == "over-max-hp" ? 101 : 40,
            invalid == "zero-speed" ? 0U : 80U, 125));
    }

    [Fact]
    public void Catalog_and_history_enumeration_stops_at_each_declared_capacity()
    {
        int seen = 0;
        IEnumerable<RestorativeRemedyDefinition> Remedies()
        {
            for (ulong index = 1_000; ; index++) { seen++; yield return Remedy(index); }
        }
        Assert.Throws<ArgumentException>(() => new CareSnapshot(World, Branch, 0, Remedies(), [Agreement()], [], []));
        Assert.Equal(CareSnapshot.MaximumRemedies + 1, seen);
        seen = 0;
        IEnumerable<CareAgreement> Agreements()
        {
            for (ulong index = 2_000; ; index++) { seen++; yield return Agreement(index); }
        }
        Assert.Throws<ArgumentException>(() => new CareSnapshot(World, Branch, 0, [Remedy()], Agreements(), [], []));
        Assert.Equal(CareSnapshot.MaximumAgreements + 1, seen);
        seen = 0;
        IEnumerable<CareAcknowledgment> Acknowledgments()
        {
            for (ulong index = 0; ; index++)
            {
                seen++;
                yield return Acknowledgment(11 + index, 2_000 + index, Defeat(10 + index, 7 + index, 1_000 + index));
            }
        }
        Assert.Throws<ArgumentException>(() => new CareSnapshot(World, Branch, 0, [Remedy()], [Agreement()], Acknowledgments(), []));
        Assert.Equal(CareSnapshot.MaximumAcknowledgments + 1, seen);
        seen = 0;
        IEnumerable<CareTreatment> Treatments()
        {
            for (ulong index = 0; ; index++)
            {
                seen++;
                yield return Treatment(Acknowledgment(), 12 + index, 3_000 + index);
            }
        }
        Assert.Throws<ArgumentException>(() => new CareSnapshot(World, Branch, 0, [Remedy()], [Agreement()], [], Treatments()));
        Assert.Equal(CareSnapshot.MaximumTreatments + 1, seen);
    }
}
