using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Tests.Quests;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionFixture;

namespace OpenFantasy.Core.Tests.Construction;

internal static class ConstructionWorldFixture
{
    internal static readonly EntityId Builder = AdmissionFixture.Id(11);
    internal static readonly EntityId KitLot = Id(80);

    internal static WorldSnapshot Basis(bool completed = true, bool held = false)
    {
        QuestFixture quest = QuestFixture.Create();
        if (completed)
        {
            quest = quest.Apply(quest.Plan(quest.Accept()));
            quest = quest.Apply(quest.Plan(quest.Complete()));
        }
        WorldSnapshot value = quest.AsWorld();
        // This source-free fixture deliberately admits no previous transform definitions.
        // The native/content suite separately retains the real ration calibration and batches.
        var production = new ProductionSnapshot(value.WorldId, value.BranchId, 0, [], [], []);
        return new WorldSnapshot(value.Inventory, value.Space, value.Residents, value.AdmissionVersion, value.Calendar,
            value.Revision, value.AdvancingSteps, held ? value.Dialogues : [], value.Gathering, production, value.Progression, value.Quests);
    }

    internal static WorldConstructionAdmission Admission(WorldSnapshot basis)
    {
        ConstructionDefinition definition = Definition(sourceItem: AdmissionFixture.Material);
        var project = new ConstructionProject(Id(70), definition, Builder, Builder,
            basis.Quests!.Definitions.Single().DefinitionId, QuestFixture.Project, QuestFixture.Reservation,
            new(660, -256, 256), new(512, 0, 256),
            [new(Builder, new(512, 0, 256), new(768, 256, 256)),
             new(AdmissionFixture.Id(10), new(0, 256, 256), new(0, 512, 256))]);
        var state = new ConstructionSnapshot(basis.WorldId, basis.BranchId, 0, [definition], [project]);
        var supply = new ConstructionToolAdmission(Builder, AdmissionFixture.Id(61),
            new AdmittedCountedSource(Id(81), definition.RequiredTool.Item, 1), KitLot);
        return new WorldConstructionAdmission(Pin, state, [supply]);
    }

    internal static WorldConstructionMigration Request(WorldSnapshot basis, WorldConstructionAdmission? admission = null)
    {
        WorldConstructionAdmission selected = admission ?? Admission(basis);
        return new WorldConstructionMigration(Id(90), basis.WorldId, basis.BranchId, basis.Revision, basis.AdvancingSteps,
            basis.ContentHash, basis.AdmissionVersion, WorldConstructionMigration.ComputeTargetAdmissionVersion(basis.AdmissionVersion, selected), selected);
    }

    internal static WorldSnapshot Create(bool completed = true, bool held = false)
    {
        WorldSnapshot basis = Basis(completed, held);
        return new WorldConstructionMigrationProcessor().Decide(basis, Request(basis)).Candidate;
    }

    internal static WorldActionCommand Command(WorldSnapshot world, ulong sequence = 100, WorkConstructionIntent? intent = null)
    {
        var project = world.Construction!.Projects.Single();
        return StepFixture.Action(world, 10_000 + sequence, intent ?? new WorkConstructionIntent(project.ProjectId,
            project.Revision, world.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, KitLot,
            world.Inventory.Custody.GetLot(KitLot).Revision), 11);
    }

    internal static WorldStepDecision Step(WorldSnapshot world, ulong sequence, WorldActionCommand? command = null,
        IEnumerable<WorldMovementInput>? movement = null, Func<EntityId, bool>? occupied = null) =>
        new WorldStepProcessor().Decide(world, new WorldStepBatch(Id(20_000 + sequence), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            movement ?? [], [command ?? Command(world, sequence)], 5), occupied);

    internal static WorldSnapshot Copy(WorldSnapshot world, LocalSpatialSnapshot? space = null,
        CountedAuthorityState? inventory = null, IEnumerable<DialogueSession>? dialogues = null) => new(inventory ?? world.Inventory,
        space ?? world.Space, world.Residents, world.AdmissionVersion, world.Calendar, world.Revision,
        world.AdvancingSteps, dialogues ?? world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests, world.Construction);
}
