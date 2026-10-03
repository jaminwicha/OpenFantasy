using System.Text.Json;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;

namespace OpenFantasy.Hosting.Tests.Content;

public sealed class AdventureWorldAdmissionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Fresh_adventure_preserves_people_resources_and_work_while_adding_cooperation_policies(int count)
    {
        string[] names = Enumerable.Range(0, count).Select(index => $"Traveler {index}").ToArray();
        var original = CargoOutcomeWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, names);
        var actual = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, names);
        Assert.Equal((ushort)40, actual.ProfileVersion);
        Assert.Equal(0UL, actual.Revision);
        Assert.Equal(0UL, actual.AdvancingSteps);
        Assert.Equal(original.Inventory.ContentHash, actual.Inventory.ContentHash);
        Assert.Equal(original.Production!.ContentHash, actual.Production!.ContentHash);
        Assert.Equal(original.Progression!.ContentHash, actual.Progression!.ContentHash);
        Assert.Equal(original.Quests!.ContentHash, actual.Quests!.ContentHash);
        Assert.Equal(original.Construction!.ContentHash, actual.Construction!.ContentHash);
        Assert.Equal(original.Calendar, actual.Calendar);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(original.Space), JsonSerializer.SerializeToUtf8Bytes(actual.Space));
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(original.Residents), JsonSerializer.SerializeToUtf8Bytes(actual.Residents));
        var builder = actual.Construction.Projects.Single().BuilderActorId;
        Assert.Equal(ResidentControllerKind.Host, actual.GetResident(builder).ControllerKind);
        Assert.Equal(0UL, actual.Construction.Projects.Single().Progress.CompletedUnits);
        Assert.Equal(count, actual.Controllers!.Assignments.Count);
        Assert.DoesNotContain(actual.Controllers.Assignments, row => row.ActorId == builder);
        Assert.NotNull(actual.ControlPermissions);
        Assert.NotNull(actual.EffectiveControl);
        Assert.NotNull(actual.Recovery);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(original.Care!.Ordinary!.Permissions),
            JsonSerializer.SerializeToUtf8Bytes(actual.Care!.Ordinary!.Permissions));
        Assert.Single(actual.Care.Ordinary.Definitions, definition =>
            definition.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance);
        Assert.NotEqual(original.AdmissionVersion, actual.AdmissionVersion);
        byte[] bytes = WorldSnapshotCodec.Encode(actual);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, names)));
    }

    [Fact]
    public void Admission_binds_seed_order_name_and_roster_size()
    {
        var original = AdventureWorldAdmission.CreateEmbedded("one", ["Rowan", "Mira"]);
        foreach (var changed in new[]
        {
            AdventureWorldAdmission.CreateEmbedded("two", ["Rowan", "Mira"]),
            AdventureWorldAdmission.CreateEmbedded("one", ["Mira", "Rowan"]),
            AdventureWorldAdmission.CreateEmbedded("one", ["Rowan", "Ash"]),
            AdventureWorldAdmission.CreateEmbedded("one", ["Rowan"])
        })
        {
            Assert.NotEqual(original.ContentHash, changed.ContentHash);
        }
    }
}
