using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Requires the retained production admission and exact source giver before adding any new candidate state.</summary>
internal static class QuestFarmWorldValidation
{
    internal static void Validate(FarmContentFiles files, WorldSnapshot world, QuestFarmSources source)
    {
        if (world.ProfileVersion != 3 || world.Production is null || world.Gathering is null
            || world.Residents.Count != 2 || world.Residents.Count(value => value.ControllerKind == ResidentControllerKind.Player) != 1
            || world.Residents.Count(value => value.ControllerKind == ResidentControllerKind.Host) != 1)
        {
            throw new ArgumentException("Quest admission requires the existing two-actor production farm and an explicit migration.", nameof(world));
        }

        // This temporary view validates the original input pins without the already-admitted production
        // identities colliding with their own expected definitions. It is never returned, saved or published.
        var gatheredView = new WorldSnapshot(world.Inventory, world.Space, world.Residents,
            new ContentHash(GatheringFarmDefinition.AcceptedHash), world.Calendar, world.Revision,
            world.AdvancingSteps, world.Dialogues, world.Gathering);
        ProductionSnapshot expected = new ProductionFarmBuilder(gatheredView, ProductionFarmDefinition.Load(files)).Build();
        var actualDefinitions = new ProductionSnapshot(world.WorldId, world.BranchId, 0, world.Production.Recipes, [], []);
        if (actualDefinitions.ContentHash != expected.ContentHash
            || world.AdmissionVersion != WorldProductionMigration.ComputeTargetAdmissionVersion(gatheredView.AdmissionVersion, expected))
        {
            throw new ArgumentException("The existing production recipes or admission pin differ from the retained farm profile.", nameof(world));
        }

        WorldResident giver = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host);
        var home = new SubunitPosition(checked(source.Wren.GetProperty("tileX").GetInt64() * 256 + 128),
            checked(source.Wren.GetProperty("tileY").GetInt64() * 256 + 128), 256);
        if (giver.DisplayName != source.Wren.GetProperty("name").GetString() || giver.HomeFeet != home
            || giver.WanderRadiusCells != source.Wren.GetProperty("wander").GetInt32()
            || giver.OpeningUtterance?.Text != source.Wren.GetProperty("dialog").GetString())
        {
            throw new ArgumentException("The existing host resident does not retain Wren's admitted source persona.", nameof(world));
        }
    }
}
