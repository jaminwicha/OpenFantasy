using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Prepares a source-pinned quest admission for an existing production farm; publishes no world or save.</summary>
public static class QuestFarmAdmission
{
    public static ContentHash AdmissionVersion => new(QuestFarmDefinition.AcceptedHash);

    public static QuestFarmComponents CreateQuests(WorldSnapshot world) => Create(FarmContentFiles.Embedded(), world);

    internal static QuestFarmComponents CreateQuests(string contentRoot, WorldSnapshot world) =>
        Create(new FarmContentFiles(contentRoot), world);

    private static QuestFarmComponents Create(FarmContentFiles files, WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        QuestFarmDefinition definition = QuestFarmDefinition.Load(files);
        QuestFarmSources sources = QuestFarmSources.Load(files, definition);
        QuestFarmWorldValidation.Validate(files, world, sources);
        return new QuestFarmBuilder(world, definition, sources).Build();
    }
}

/// <summary>
/// Candidate admission only: retained custody plus empty project storage, fresh explicit progression,
/// and an unaccepted quest. The migration owner must atomically preserve and publish all other world state.
/// </summary>
public sealed class QuestFarmComponents
{
    internal QuestFarmComponents(CountedAuthorityState inventory, WorldQuestAdmission admission)
    {
        admission.Quests.ValidateForAuthority(inventory, admission.Progression);
        Inventory = inventory;
        Admission = admission;
    }

    public CountedAuthorityState Inventory { get; }
    public WorldQuestAdmission Admission { get; }
    public ProgressionSnapshot Progression => Admission.Progression;
    public QuestSnapshot Quests => Admission.Quests;
    public ContentHash AdmissionVersion => Admission.ContentVersion;
}
