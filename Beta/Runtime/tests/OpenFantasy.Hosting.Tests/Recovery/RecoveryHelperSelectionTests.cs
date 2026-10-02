using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Recovery;

public sealed class RecoveryHelperSelectionTests
{
    [Fact]
    public void Host_helper_selects_and_pays_separately_permitted_recovery_without_consuming_reserved_choices()
    {
        using var files = new StorageFiles();
        var fixture = new RecoveryJournalFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        fixture.Defeat(authority);
        var completed = fixture.DisconnectAndComplete(authority);
        fixture.WalkTo(authority, fixture.Helper, fixture.Beside(completed, fixture.Helper, fixture.Patient));
        var state = authority.ReadStateForHost();
        Assert.Equal(0, state.Progression!.GetActor(fixture.Patient).CurrentHp);
        Assert.True(WorldActorAvailability.CanAct(state, fixture.Helper));
        var controller = new LocalRestorativeController();
        Assert.Empty(controller.SelectActions(state, authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId> { fixture.Patient }));
        Assert.Empty(controller.SelectActions(state, authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId> { fixture.Helper }));
        WorldActionCommand selected = Assert.Single(controller.SelectActions(state, authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId>()));
        var intent = Assert.IsType<UseRestorativeItemIntent>(selected.Intent);
        Assert.Equal(fixture.Helper, selected.ActorId);
        Assert.Equal(fixture.Patient, intent.RecipientActorId);
        Assert.Equal(fixture.Definition.DefinitionId, intent.UseDefinitionId);
        Assert.Equal(fixture.Permission.PermissionId, intent.PermissionId);
        Assert.Empty(controller.SelectActions(state, identity => identity == selected.CommandId,
            new HashSet<EntityId>()));
        var decision = fixture.Commit(authority, WorldBatchKind.AdvancingStep, [selected]);
        var use = Assert.Single(decision.Candidate.Care!.Ordinary!.Uses);
        Assert.Equal(selected.CommandId, use.CommandId);
        Assert.Equal(0, use.HpBefore);
        Assert.True(use.HpAfter > 0);
        Assert.Equal(use.ConsumptionId, Assert.Single(decision.Candidate.Production!.ConsumedItems).ConsumptionId);
        Assert.Equal(state.Calendar.AddMilliseconds(1_800), decision.Candidate.Calendar);
        Assert.DoesNotContain(controller.SelectActions(decision.Candidate, authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId>()), command => command.Intent is UseRestorativeItemIntent next
                && next.UseDefinitionId == fixture.Definition.DefinitionId);
    }
}
