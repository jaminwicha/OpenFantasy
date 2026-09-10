using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class AdmissionTests
{
    [Fact]
    public void ValidCompositionSharesExactDefinitionsAndKeepsDistinctClockCounters()
    {
        WorldSnapshot world = Create(dialogues: [Dialogue()]);
        WorldIdentityIndex index = WorldAdmissionValidator.Validate(world);
        foreach (ulong number in new ulong[] { 1, 2, 3, 10, 11, 20, 21, 30, 31, 40, 50, 51,
                     60, 61, 70, 71, 80, 90, 91, 92, 100, 101, 110, 111, 140, 141 })
        {
            Assert.True(index.Contains(Id(number)));
        }
        TerrainCell ground = world.Space.Regions[0].Cells.First(cell => cell.SourceId.HasValue);
        Assert.True(index.Contains(ground.SourceId!.Value));
        Assert.True(index.Contains(ground.FormationId!.Value));
        Assert.False(index.Contains(Id(999)));
        Assert.True(world.Inventory.Custody.Revision > world.Revision);
    }

    [Fact]
    public void ACountedLotCannotAlsoBeASpatialForm()
    {
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(space: existing =>
            new LocalSpatialSnapshot(Scope, existing.Regions, [Presence(0, Id(70)), Presence(1)]))));
    }

    [Fact]
    public void ACountedSourceCannotClaimUncoupledTerrainRightsEvenAfterExcavation()
    {
        TerrainRegionSnapshot terrain = AdmissionFixture.Terrain();
        TerrainCell cell = terrain.Cells.First(value => value.SourceId.HasValue);
        terrain = terrain.Excavate(terrain.Revision, [cell.Coordinate]);
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(
            inventory: _ => Inventory(sourceId: cell.SourceId),
            space: existing => new LocalSpatialSnapshot(Scope, [terrain], existing.Actors))));
    }

    [Fact]
    public void SharedCountedMaterialIdentityRequiresTheSameVersionAcrossDomains()
    {
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(inventory: _ =>
            Inventory(material: new CountedItemDefinition(Material.DefinitionId, ContentHash.Compute("other"u8))))));
    }

    [Fact]
    public void TerrainMaterialCannotAliasAPrincipalOrBodyDefinition()
    {
        foreach (ulong alias in new ulong[] { 100, 40 })
        {
            Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(space: existing =>
                new LocalSpatialSnapshot(Scope, [AdmissionFixture.Terrain(Id(alias))], existing.Actors))));
        }
    }

    [Theory]
    [InlineData(10UL)]
    [InlineData(11UL)]
    public void EveryPlayerAndHostResidentRequiresAnExplicitControl(ulong omittedActor)
    {
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(inventory: existing =>
            Inventory(controls: existing.Controls.Where(value => value.ActorId != Id(omittedActor))))));
    }

    [Fact]
    public void LanguageAndRegisterReferencesCannotAliasOtherEntityRoles()
    {
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(residents: people =>
            [Resident(0, channel: new LanguageChannelKey(Id(10), Id(91), CommunicationMode.Spoken)), people[1]])));
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(residents: people =>
            [Resident(0, channel: new LanguageChannelKey(Id(90), Id(90), CommunicationMode.Spoken)), people[1]])));
    }

    [Fact]
    public void SharedChannelsAndRulesRequireExactPins()
    {
        ContentHash changed = ContentHash.Compute("changed-pin"u8);
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(residents: people =>
            [people[0], Resident(1, channelVersion: changed)])));
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(residents: people =>
            [people[0], Resident(1, rulesVersion: changed)])));
    }

    [Fact]
    public void SharedUtteranceDefinitionCannotSilentlyChangeItsTextOrChannel()
    {
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(residents: people =>
            [people[0], Resident(1, utterance: new ResidentUtterance(Id(92), Channel, AdmissionFixture.Version, "A different claim."))])));
    }

    [Fact]
    public void DialogueAndInjuryCausesAreReferencesWithCompatibleRepeatedUse()
    {
        var injury = new BodyInjury(Id(120), Id(141), new BodySlotKey("core"), BodyInjuryKind.Scar);
        WorldSnapshot world = Create(space: existing => new LocalSpatialSnapshot(Scope, existing.Regions,
            [Presence(0, injuries: [injury]), Presence(1)]), dialogues: [Dialogue()]);
        WorldIdentityIndex index = WorldAdmissionValidator.Validate(world);
        Assert.True(index.Contains(Id(120)));
        Assert.True(index.Contains(Id(141)));
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(dialogues: [Dialogue(Id(70))])));
    }

    [Fact]
    public void InjuryIdentitiesCannotDuplicateAcrossActorsOrAliasTheirCauses()
    {
        var injury = new BodyInjury(Id(120), Id(130), new BodySlotKey("core"), BodyInjuryKind.Scar);
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(space: existing =>
            new LocalSpatialSnapshot(Scope, existing.Regions,
                [Presence(0, injuries: [injury]), Presence(1, injuries: [injury])]))));
        var selfCaused = new BodyInjury(Id(120), Id(120), new BodySlotKey("core"), BodyInjuryKind.Scar);
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(space: existing =>
            new LocalSpatialSnapshot(Scope, existing.Regions,
                [Presence(0, injuries: [selfCaused]), Presence(1)]))));
    }

    [Fact]
    public void AuthoredDialogueCanRemainOnAnImpairedSpeakerButCannotBeActive()
    {
        WorldSnapshot retained = Create(space: existing => new LocalSpatialSnapshot(Scope, existing.Regions,
            [Presence(0), Presence(1, vocal: false)]));
        Assert.NotNull(WorldAdmissionValidator.Validate(retained));
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(
            space: existing => new LocalSpatialSnapshot(Scope, existing.Regions,
                [Presence(0), Presence(1, vocal: false)]), dialogues: [Dialogue()])));
    }

    [Fact]
    public void ActiveDialogueRequiresExplicitListenerComprehensionAndHearing()
    {
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(
            residents: people => [Resident(0, band: ComprehensionBand.Practical), people[1]], dialogues: [Dialogue()])));
        var injury = new BodyInjury(Id(120), Id(130), new BodySlotKey("core"),
            BodyInjuryKind.FunctionLoss, BodyFunction.Hearing);
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(
            space: existing => new LocalSpatialSnapshot(Scope, existing.Regions,
                [Presence(0, injuries: [injury]), Presence(1)]), dialogues: [Dialogue()])));
    }

    [Fact]
    public void AnAuthoredUtteranceDoesNotSupplyTheSpeakersAdmittedChannel()
    {
        WorldSnapshot authored = Create(residents: people => [people[0], Resident(1, band: ComprehensionBand.Practical)]);
        Assert.NotNull(WorldAdmissionValidator.Validate(authored));
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(
            residents: people => [people[0], Resident(1, band: ComprehensionBand.Practical)], dialogues: [Dialogue()])));
    }

    [Fact]
    public void ActiveSpokenDialogueCannotBeRestoredOutOfRangeOrThroughAnotherMode()
    {
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(space: existing =>
        {
            ActorPresence speaker = existing.GetActor(Id(11));
            var distant = new ActorPresence(speaker.Binding, speaker.Condition, Scope,
                new SubunitPosition(1024, 0, 256), speaker.Revision, speaker.HomePlaceId);
            return new LocalSpatialSnapshot(Scope, existing.Regions, [existing.GetActor(Id(10)), distant]);
        }, dialogues: [Dialogue()])));
        var signed = new LanguageChannelKey(Id(90), Id(91), CommunicationMode.Signed);
        var utterance = new ResidentUtterance(Id(92), signed, AdmissionFixture.Version, "A signed message.");
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(residents: _ =>
            [Resident(0, channel: signed, utterance: utterance), Resident(1, channel: signed, utterance: utterance)],
            dialogues: [Dialogue()])));
    }

    [Fact]
    public void AdvancingStepsCannotExceedCommittedWorldRevisions()
    {
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(revision: 1, steps: 2)));
        Assert.NotNull(WorldAdmissionValidator.Validate(Create(revision: ulong.MaxValue, steps: ulong.MaxValue)));
    }

    [Fact]
    public void ReusingTerrainIndexStillValidatesNewDynamicIdentitiesAndChangedTerrain()
    {
        WorldSnapshot original = Create();
        WorldIdentityIndex previous = WorldAdmissionValidator.Validate(original);
        var unchanged = new WorldSnapshot(original.Inventory, original.Space, original.Residents,
            AdmissionFixture.Version, original.Calendar, 3, 2);
        WorldIdentityIndex reused = WorldAdmissionValidator.Validate(unchanged, previous);
        Assert.True(reused.Contains(Id(70)));
        WorldIdentityIndex decodedEquivalent = WorldAdmissionValidator.Validate(Create(), previous);
        Assert.True(decodedEquivalent.Contains(Id(70)));
        TerrainCell source = original.Space.Regions[0].Cells.First(value => value.SourceId.HasValue);
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(
            inventory: _ => Inventory(sourceId: source.SourceId), space: _ => original.Space), previous));
        Assert.Throws<ArgumentException>(() => WorldAdmissionValidator.Validate(Create(space: existing =>
            new LocalSpatialSnapshot(Scope, [AdmissionFixture.Terrain(Id(100))], existing.Actors)), previous));
        Assert.True(previous.Contains(Id(100)));
        Assert.False(previous.Contains(Id(999)));
    }

    [Fact]
    public void AReusedTerrainLayerDoesNotMutateAPreviousDynamicIndex()
    {
        WorldSnapshot original = Create();
        WorldIdentityIndex previous = WorldAdmissionValidator.Validate(original);
        var injury = new BodyInjury(Id(998), Id(999), new BodySlotKey("core"), BodyInjuryKind.Scar);
        WorldSnapshot changed = Create(space: _ => new LocalSpatialSnapshot(Scope, original.Space.Regions,
            [Presence(0, injuries: [injury]), Presence(1)]));
        WorldIdentityIndex next = WorldAdmissionValidator.Validate(changed, previous);
        Assert.True(next.Contains(Id(998)));
        Assert.True(next.Contains(Id(999)));
        Assert.False(previous.Contains(Id(998)));
        Assert.False(previous.Contains(Id(999)));
    }
}
