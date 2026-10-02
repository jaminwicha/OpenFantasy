using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed partial class NativeRosterJourneyTests
{
    private static async Task SendChoice(Dictionary<EntityId, NativeRosterProcess> processes,
        NativeRosterProcess actor, string verb, CancellationToken token)
    {
        await actor.Send(verb);
        var submitted = actor.Submissions[^1];
        var command = WorldActionCodec.Decode(Convert.FromHexString(submitted.GetProperty("Command").GetString()!));
        (EntityId Encounter, ulong Revision) accepted = command.Intent switch
        {
            QueueEncounterWaitIntent wait => (wait.EncounterId, checked(wait.ExpectedTimingRevision + 1)),
            QueueEncounterAbilityIntent ability => (ability.EncounterId, checked(ability.ExpectedTimingRevision + 1)),
            _ => throw new InvalidOperationException("The native queue request did not submit the intended combat choice.")
        };
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            await ObserveAll(processes);
            token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed.TotalSeconds < 5,
                $"Every native stream must observe accepted combat timing {accepted.Revision} before the next deliberate choice.");
            if (processes.Values.All(process => process.View.Encounter is { } encounter
                && encounter.EncounterId == accepted.Encounter && encounter.TimingRevision >= accepted.Revision)) return;
            await Task.Delay(20, token);
        }
    }

    private static async Task FinishEncounter(Dictionary<EntityId, NativeRosterProcess> processes, CancellationToken token)
    {
        int choices = 0;
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            await ObserveAll(processes);
            token.ThrowIfCancellationRequested();
            Assert.True(deadline.Elapsed.TotalSeconds < 90, "The actual native encounter did not complete within its finite combat bound.");
            if (processes.Values.All(process => process.View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome)) break;
            NativeRosterProcess? due = processes.Values.FirstOrDefault(process => process.View.EncounterActions is
                { WaitBlockedReason: LocalEncounterActionBlockedReason.None });
            if (due is null) { await Task.Delay(25, token); continue; }
            Assert.True(++choices <= 80, "Native encounter exceeded its deliberate-choice budget.");
            // Each observed choice is made once through its currently presented wheel. A player
            // without a reachable attack deliberately waits; no actor is moved inside combat.
            await SendChoice(processes, due,
                due.View.EncounterActions!.AbilityOffers.Any(offer => offer.CanQueue) ? "strike" : "wait", token);
        }
        Assert.All(processes.Values, process =>
        {
            Assert.Equal(LocalOutcomeStanding.YourSideStands, process.View.EncounterOutcome!.Standing);
            Assert.Equal(4, process.View.EncounterOutcome.RequiredReaders);
        });
        foreach (NativeRosterProcess process in processes.Values) await process.Send("read-outcome");
        do
        {
            await ObserveAll(processes);
            token.ThrowIfCancellationRequested();
            Assert.True(deadline.Elapsed.TotalSeconds < 90, "Native outcome readings did not return to exploration.");
        } while (processes.Values.Any(process => process.View.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Exploration));
        Assert.All(processes.Values, process => Assert.Equal(LocalOutcomeStage.Completed, process.View.EncounterOutcome!.Stage));
    }

    private static async Task Assemble(WorldSnapshot genesis, EncounterScenarioDefinition scenario,
        Dictionary<EntityId, NativeRosterProcess> processes, CancellationToken token)
    {
        EntityId initiator = scenario.InitiatingActorId;
        LocalSpatialSnapshot Space()
        {
            LocalWorldView view = processes[initiator].View;
            // A detached geometry query composed from admitted terrain/body definitions and public
            // observed positions is planning only. It is never installed into the world authority.
            return genesis.Space.WithActors(genesis.Space.Actors.Select(original =>
            {
                var row = view.Actors.Single(value => value.ActorId == original.ActorId);
                return new ActorPresence(original.Binding, original.Condition, original.Scope, row.Feet,
                    row.PresenceRevision, original.HomePlaceId);
            }));
        }
        bool InReach(LocalSpatialSnapshot space, EntityId actor) => actor == initiator
            || space.QueryInteraction(initiator, actor, scenario.EntryRangeSubunits) == SpatialInteraction.Reachable;
        async Task Walk(EntityId actor, SubunitPosition goal, Func<LocalSpatialSnapshot, bool> arrived)
        {
            for (int step = 0; !arrived(Space()); step++)
            {
                token.ThrowIfCancellationRequested();
                Assert.True(step < 96, "Native assembly exceeded its finite input sample budget.");
                LocalSpatialSnapshot space = Space();
                GroundRouteResult route = space.QueryGroundRoute(actor, goal);
                Assert.True(route.Status == GroundRouteStatus.Reachable && route.NextWaypoint is not null,
                    $"No ordinary native route for {actor} to {goal}: {route.Status}.");
                SubunitPosition feet = space.GetActor(actor).Feet;
                SubunitPosition next = route.NextWaypoint!.Value;
                string direction = next.X != feet.X ? next.X < feet.X ? "left" : "right"
                    : next.Y < feet.Y ? "up" : "down";
                await processes[actor].Send("walk", direction);
                await ObserveAll(processes);
                Assert.NotEqual(feet, Space().GetActor(actor).Feet);
            }
        }
        LocalSpatialSnapshot initial = Space();
        SubunitPosition target = initial.GetActor(scenario.TargetActorId).Feet;
        if (!InReach(initial, scenario.TargetActorId))
        {
            var candidates = new[] { target.Offset(256, 0, 0), target.Offset(-256, 0, 0),
                target.Offset(0, 256, 0), target.Offset(0, -256, 0) }
                .Select(goal => (Goal: goal, Route: initial.QueryGroundRoute(initiator, goal)))
                .Where(value => value.Route.Status == GroundRouteStatus.Reachable)
                .OrderBy(value => value.Route.Waypoints.Count).ToArray();
            Assert.NotEmpty(candidates);
            await Walk(initiator, candidates[0].Goal, space => InReach(space, scenario.TargetActorId));
        }
        while (processes.Keys.Any(actor => !InReach(Space(), actor)))
        {
            LocalSpatialSnapshot space = Space();
            var center = space.GetActor(initiator).Feet;
            var candidates = (from actor in processes.Keys.Where(actor => !InReach(space, actor))
                from x in new[] { -256L, 0L, 256L }
                from y in new[] { -256L, 0L, 256L }
                where x != 0 || y != 0
                let goal = center.Offset(x, y, 0)
                let route = space.QueryGroundRoute(actor, goal)
                where route.Status == GroundRouteStatus.Reachable
                select (Actor: actor, Goal: goal, Route: route))
                .OrderBy(value => value.Route.Waypoints.Count).ThenBy(value => value.Actor)
                .ThenBy(value => value.Goal.X).ThenBy(value => value.Goal.Y).ToArray();
            Assert.NotEmpty(candidates);
            var selected = candidates[0];
            // Stop at observed lawful reach, not an assumed exact number of network movement samples.
            await Walk(selected.Actor, selected.Goal, current => InReach(current, selected.Actor));
        }
        await ObserveAll(processes);
        Assert.True(processes[initiator].View.EncounterLifecycle!.Entries
            .Single(entry => entry.ScenarioId == scenario.DefinitionId).CanEnter);
    }

    private static void VerifyHistory(string database, WorldSnapshot genesis,
        IReadOnlyList<NativeRosterProcess> processes, ulong heldUnits)
    {
        using var authority = SqliteWorldAuthority.OpenExisting(database);
        WorldSnapshot final = authority.ReadStateForHost();
        Assert.Equal(genesis.AdmissionVersion, final.AdmissionVersion);
        Assert.Equal(genesis.Residents.Select(value => (value.ActorId, value.ControllerKind, value.InventoryCustodianId)),
            final.Residents.Select(value => (value.ActorId, value.ControllerKind, value.InventoryCustodianId)));
        Assert.Equal(genesis.Inventory.Controls, final.Inventory.Controls);
        Assert.Equal(genesis.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)),
            final.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)));
        var submitted = processes.SelectMany(process => process.Submissions).ToArray();
        Assert.True(submitted.Length >= 10);
        var commands = submitted.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!))).ToArray();
        Assert.Equal(commands.Length, commands.Select(command => command.CommandId).Distinct().Count());
        Assert.Single(commands, command => command.Intent is EnterEncounterIntent);
        Assert.True(commands.Count(command => command.Intent is QueueEncounterWaitIntent) >= 4);
        Assert.Contains(commands, command => command.Intent is QueueEncounterAbilityIntent);
        Assert.Equal(4, commands.Count(command => command.Intent is AcknowledgeEncounterOutcomeIntent));
        foreach (var value in submitted)
        {
            Assert.False(value.GetProperty("IsRetry").GetBoolean());
            var command = WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
            var owner = genesis.Inventory.Controls.Single(control => control.ActorId == command.ActorId);
            var receipt = authority.FindReceipt(command, new TrustedCommandDelivery(owner.PrincipalId, owner.SessionId));
            Assert.NotNull(receipt);
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(Convert.FromHexString(value.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
        }
        var waits = commands.Where(command => command.Intent is QueueEncounterWaitIntent).ToArray();
        Assert.Equal(genesis.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player).Select(actor => actor.ActorId).Order(),
            waits.Select(command => command.ActorId).Distinct().Order());
        var firstChoices = waits.Select(command => (Command: command, Receipt: authority.FindReceipt(command,
                new(command.PrincipalId, command.OriginSessionId))!))
            .GroupBy(value => value.Command.ActorId).Select(group => group.OrderBy(value => value.Receipt.CommitSequence).First())
            .OrderBy(value => value.Receipt.CommitSequence).ToArray();
        ulong heldStart = firstChoices[2].Receipt.CommitSequence;
        ulong resumedAt = firstChoices[3].Receipt.CommitSequence;
        WorldSnapshot held = authority.ReadRecordedStateForHost(heldStart).State;
        for (ulong revision = heldStart; revision < resumedAt; revision++)
        {
            WorldSnapshot recorded = authority.ReadRecordedStateForHost(revision).State;
            Assert.Equal(held.Calendar, recorded.Calendar);
            Assert.Equal(heldUnits, recorded.Pacing!.Encounter!.ElapsedTacticalUnits);
            Assert.DoesNotContain(recorded.Pacing.Encounter.Decisions, choice => choice.ActorId == firstChoices[3].Command.ActorId);
            // Automatic NPC queueing may lawfully change the decision/timing revision; it cannot
            // resolve or earn elapsed time while the fourth human has supplied no choice.
        }
        ulong first = commands.Select(command => authority.FindReceipt(command,
            new(command.PrincipalId, command.OriginSessionId))!.CommitSequence).Min();
        var waitResolutions = new Dictionary<EntityId, EncounterActionResolution>();
        bool npcResolution = false;
        for (ulong revision = first; revision <= final.Revision; revision++)
        {
            WorldSnapshot recorded = authority.ReadRecordedStateForHost(revision).State;
            if (recorded.EncounterActions?.LastResolution is not { } resolution || resolution.WorldSequence != revision) continue;
            if (waits.Any(command => command.CommandId == resolution.DecisionCommandId))
            {
                Assert.Equal(EncounterActionResolutionStatus.Wait, resolution.Status);
                Assert.True(resolution.Delay > 0);
                Assert.True(recorded.Pacing!.Encounter!.Actors.Single(actor => actor.ActorId == resolution.ActorId).RemainingDelay > 0);
                waitResolutions[resolution.ActorId] = resolution;
            }
            else if (genesis.GetResident(resolution.ActorId).ControllerKind == ResidentControllerKind.Host)
                npcResolution = true;
        }
        Assert.Equal(4, waitResolutions.Count);
        Assert.True(npcResolution, "The actual NPC combat decision must resolve; automatic actors were not suppressed.");
        Assert.Equal(EncounterLifecyclePhase.Exploration, final.EncounterLifecycle!.Phase);
        Assert.NotNull(final.EncounterOutcome!.Last);
        Assert.Equal(firstChoices.Select(value => value.Command.ActorId).Order(), final.EncounterOutcome.Last.AcknowledgedActorIds);
        Assert.True(final.EncounterLifecycle.TacticalTicks > 0);
        Assert.Equal(final.ContentHash, authority.ReadRecordedStateForHost(final.Revision).State.ContentHash);
    }
}
