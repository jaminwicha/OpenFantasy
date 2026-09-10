using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Source-pinned construction admission. Existing worlds require explicit atomic migration publication.</summary>
public static class ConstructionFarmAdmission
{
    public static ContentHash AdmissionVersion => new(ConstructionFarmDefinition.AcceptedHash);

    public static ConstructionFarmComponents CreateConstruction(WorldSnapshot world) =>
        Create(FarmContentFiles.Embedded(), world, fresh: false);

    internal static ConstructionFarmComponents CreateConstruction(string contentRoot, WorldSnapshot world) =>
        Create(new FarmContentFiles(contentRoot), world, fresh: false);

    /// <summary>Creates a new unplayed world root from a qualified fresh quest genesis, never resetting a progressed save.</summary>
    public static WorldSnapshot CreateGenesis(WorldSnapshot freshQuestGenesis)
    {
        ArgumentNullException.ThrowIfNull(freshQuestGenesis);
        RequireFresh(freshQuestGenesis);
        ConstructionFarmComponents components = Create(FarmContentFiles.Embedded(), freshQuestGenesis, fresh: true);
        return new WorldSnapshot(components.Inventory, freshQuestGenesis.Space, freshQuestGenesis.Residents,
            WorldConstructionMigration.ComputeTargetAdmissionVersion(freshQuestGenesis.AdmissionVersion, components.Admission),
            freshQuestGenesis.Calendar, 0, 0, freshQuestGenesis.Dialogues, freshQuestGenesis.Gathering,
            components.Production, freshQuestGenesis.Progression, freshQuestGenesis.Quests, components.Construction);
    }

    private static ConstructionFarmComponents Create(FarmContentFiles files, WorldSnapshot world, bool fresh)
    {
        ArgumentNullException.ThrowIfNull(world);
        ConstructionFarmDefinition metadata = ConstructionFarmDefinition.Load(files);
        QuestFarmSources sources = metadata.ValidateSources(files);
        ConstructionFarmProfileValidation.Validate(world, sources);
        return new ConstructionFarmBuilder(world, metadata).Build(fresh);
    }

    private static void RequireFresh(WorldSnapshot world)
    {
        if (world.ProfileVersion != 4 || world.Revision != 0 || world.AdvancingSteps != 0
            || world.Calendar != new WorldInstant(0, 32_400_000) || world.Dialogues.Count != 0
            || world.Inventory.Custody.Revision != 0 || world.Inventory.Custody.Storages.Any(value => value.Revision != 0)
            || world.Inventory.Custody.Lots.Any(value => value.Revision != 0 || value.ReservedBy is not null)
            || world.Space.Regions.Any(value => value.Revision != 0)
            || world.Space.Actors.Any(value => value.Revision != 0 || value.Condition.Injuries.Count != 0
                || value.Feet != world.GetResident(value.ActorId).HomeFeet)
            || world.Residents.Any(value => value.ExperiencedTime != LocalExposureDuration.Zero)
            || world.Gathering is null || world.Gathering.AdvancingStep != 0
            || world.Gathering.Nodes.Any(value => value.Revision != 0 || value.NextUnissuedUnit != 0 || value.DepletedAtStep is not null)
            || world.Gathering.Actors.Any(value => value.Effort != 0)
            || world.Production is null || world.Production.Revision != 0 || world.Production.Batches.Count != 0 || world.Production.Lots.Count != 0
            || world.Quests is null || world.Quests.Revision != 0 || world.Quests.Participations.Count != 0
            || world.Progression is null || world.Progression.Revision != 0 || world.Progression.Currency.Revision != 0
            || world.Progression.Currency.Accounts.Any(value => value.Revision != 0
                || value.Balance != (value.Kind == CurrencyAccountKind.Reserve ? 200UL : 0UL))
            || world.Progression.Actors.Any(value => value.Revision != 0 || value.Level != 1 || value.Experience != 0
                || value.CurrentHp != value.Stats.MaxHp || value.CurrentMp != value.Stats.MaxMp))
        {
            throw new ArgumentException("Fresh construction genesis requires an unplayed initial quest state; progressed worlds need explicit migration.", nameof(world));
        }
    }
}

/// <summary>Pure proposed inventory/production changes and new construction metadata; no work or persistence is applied.</summary>
public sealed class ConstructionFarmComponents
{
    internal ConstructionFarmComponents(CountedAuthorityState inventory, ProductionSnapshot production, WorldConstructionAdmission admission)
    {
        Inventory = inventory;
        Production = production;
        Admission = admission;
    }

    public CountedAuthorityState Inventory { get; }
    public ProductionSnapshot Production { get; }
    public ConstructionSnapshot Construction => Admission.Construction;
    public WorldConstructionAdmission Admission { get; }
    public ContentHash AdmissionVersion => Admission.ContentVersion;
}
