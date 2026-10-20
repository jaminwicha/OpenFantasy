using OpenFantasy.Client;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Client;

/// <summary>The journal against real quest worlds: it shows what the permitted view shows and nothing more.</summary>
public sealed class FarmJournalModelTests
{
    [Fact]
    public void An_offer_appears_only_while_addressed_and_commitments_persist_with_real_progression()
    {
        WorldSnapshot world = Create();
        JournalPage before = JournalPage.From(new LocalWorldView(world, Id(10)));
        Assert.Empty(before.Quests);
        JournalCharacter character = Assert.IsType<JournalCharacter>(before.Character);
        Assert.Equal((1, 60, 10), (character.Level, character.Hp, character.Mp));

        world = Apply(world, Action(world, 96_001, new SpeakToActorIntent(Id(11), 0)));
        JournalQuest offer = Assert.Single(JournalPage.From(new LocalWorldView(world, Id(10))).Quests);
        Assert.Equal(JournalQuestState.Offered, offer.State);
        Assert.Equal(("6 / 6 ready", "420 XP · 200 gil"), (offer.ProgressText, offer.RewardText));
        // Another resident's view exposes no offer addressed to the player.
        Assert.Empty(JournalPage.From(new LocalWorldView(world, Id(11))).Quests);

        world = Apply(world, QuestUpgradeFixture.Accept(world));
        JournalQuest accepted = Assert.Single(JournalPage.From(new LocalWorldView(world, Id(10))).Quests);
        Assert.Equal(("Accepted · ready to deliver", "6 / 6 ready"), (accepted.StateText, accepted.ProgressText));

        world = Apply(world, QuestUpgradeFixture.Complete(world));
        world = Apply(world, Action(world, 96_002, new CloseDialogueIntent(world.Dialogues.Single().DialogueId)));
        JournalPage after = JournalPage.From(new LocalWorldView(world, Id(10)));
        JournalQuest completed = Assert.Single(after.Quests);
        Assert.Equal(("Completed", "Delivered"), (completed.StateText, completed.ProgressText));
        Assert.Equal((3, 38UL, 78, 14, 200UL), (after.Character!.Level, after.Character.Experience, after.Character.Hp,
            after.Character.Mp, after.Character.Currency));
    }

    [Fact]
    public void A_world_without_progression_reports_no_character_and_states_missing_projections()
    {
        JournalPage page = JournalPage.From(new LocalWorldView(Genesis(), Id(10)));
        Assert.Null(page.Character);
        Assert.Empty(page.Quests);
        Assert.Contains("not shown", JournalPage.EquipmentAndSkills, StringComparison.Ordinal);
    }

    private static WorldSnapshot Create()
    {
        WorldSnapshot gathered = GatheringWorldFixture.Create();
        WorldSnapshot production = new WorldProductionMigrationProcessor().Decide(gathered,
            ProductionUpgradeFixture.Request(gathered)).Candidate;
        return new WorldQuestMigrationProcessor().Decide(production, QuestUpgradeFixture.Request(production)).Candidate;
    }

    private static WorldSnapshot Apply(WorldSnapshot world, WorldActionCommand action) =>
        new WorldStepProcessor().Decide(world, GatheringWorldFixture.Batch(world, action)).Candidate;

    private static WorldActionCommand Action(WorldSnapshot world, ulong commandId, WorldActionIntent intent) =>
        new(Id(commandId), Delivery.PrincipalId, Delivery.SessionId, Id(10), world.WorldId, world.BranchId,
            world.Space.GetActor(Id(10)).Revision, intent);
}
