using System.Text.Json;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.HumanConstruction;

public sealed class HumanConstructionAdmissionTests
{
    private static readonly string[] Names = ["Amber", "Birch", "Cedar"];

    [Fact]
    public void Explicit_fresh_builder_selection_preserves_the_original_kit_contract_and_materials()
    {
        var original = RoadThreatWorldAdmission.CreateEmbedded(HumanConstructionWorldAdmission.SeedText, Names);
        var actual = HumanConstructionWorldAdmission.CreateEmbedded(HumanConstructionWorldAdmission.SeedText, Names);
        var builder = original.Construction!.Projects.Single().BuilderActorId;
        Assert.Equal(ResidentControllerKind.Host, original.GetResident(builder).ControllerKind);
        Assert.Equal(ResidentControllerKind.Player, actual.GetResident(builder).ControllerKind);
        Assert.Equal((ushort)40, actual.ProfileVersion);
        Assert.Equal(original.Inventory.ContentHash, actual.Inventory.ContentHash);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(original.Space), JsonSerializer.SerializeToUtf8Bytes(actual.Space));
        Assert.Equal(original.Construction.ContentHash, actual.Construction!.ContentHash);
        Assert.Equal(original.Production!.ContentHash, actual.Production!.ContentHash);
        Assert.Equal(original.Progression!.ContentHash, actual.Progression!.ContentHash);
        Assert.Equal(original.Quests!.ContentHash, actual.Quests!.ContentHash);
        Assert.Equal(original.Calendar, actual.Calendar);
        Assert.Equal(0UL, actual.Revision);
        Assert.Equal(0UL, actual.AdvancingSteps);
        Assert.Equal(0UL, actual.Construction.Projects.Single().Progress.CompletedUnits);
        Assert.False(actual.Construction.Projects.Single().IsCompleted);
        Assert.Equal(original.Residents.Select(actor => actor.ActorId), actual.Residents.Select(actor => actor.ActorId));
        foreach (var resident in original.Residents)
        {
            var current = actual.GetResident(resident.ActorId);
            Assert.Equal(resident.ActorId == builder ? ResidentControllerKind.Player : resident.ControllerKind,
                current.ControllerKind);
            Assert.Equal(resident.InventoryCustodianId, current.InventoryCustodianId);
            Assert.Equal(resident.HomeFeet, current.HomeFeet);
            Assert.Equal(resident.ExperiencedTime, current.ExperiencedTime);
            Assert.Equal(resident.DisplayName, current.DisplayName);
        }
        Assert.Equal(original.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
                .Select(actor => actor.ActorId).Append(builder).Order(),
            actual.Controllers!.Assignments.Select(row => row.ActorId).Order());
        Assert.Equal(4, actual.Controllers.Assignments.Count);
        Assert.Equal(WorldSnapshotCodec.Encode(actual), WorldSnapshotCodec.Encode(
            HumanConstructionWorldAdmission.CreateEmbedded(HumanConstructionWorldAdmission.SeedText, Names)));
        var ordinary = CargoOutcomeWorldAdmission.CreateEmbedded(HumanConstructionWorldAdmission.SeedText, Names);
        Assert.Equal(ResidentControllerKind.Host, ordinary.GetResident(builder).ControllerKind);
        Assert.NotEqual(ordinary.AdmissionVersion, actual.AdmissionVersion);
    }

    [Fact]
    public void Operator_persists_the_named_scenario_and_refuses_to_overwrite_it()
    {
        using var files = new ServerFiles();
        string database = Path.Combine(files.DirectoryPath, "human-construction.sqlite3");
        using var output = new StringWriter();
        var application = new HostApplication(output);
        string[] arguments = ["--create-human-construction", database, HumanConstructionWorldAdmission.SeedText, .. Names];
        Assert.Equal(0, application.Run(arguments));
        byte[] snapshot;
        using (var authority = SqliteWorldAuthority.OpenExisting(database))
        {
            snapshot = WorldSnapshotCodec.Encode(authority.ReadStateForHost());
            Assert.Equal(WorldSnapshotCodec.Encode(HumanConstructionWorldAdmission.CreateEmbedded(
                HumanConstructionWorldAdmission.SeedText, Names)), snapshot);
        }
        Assert.Equal(1, application.Run(arguments));
        using var reopened = SqliteWorldAuthority.OpenExisting(database);
        Assert.Equal(snapshot, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Operator_refuses_an_ambiguous_roster_before_creating_a_database(int count)
    {
        using var files = new ServerFiles();
        string database = Path.Combine(files.DirectoryPath, "invalid.sqlite3");
        using var output = new StringWriter();
        Assert.Equal(1, new HostApplication(output).Run(["--create-human-construction", database,
            HumanConstructionWorldAdmission.SeedText, .. Enumerable.Range(0, count).Select(index => $"Player {index}")]));
        Assert.False(File.Exists(database));
    }
}
