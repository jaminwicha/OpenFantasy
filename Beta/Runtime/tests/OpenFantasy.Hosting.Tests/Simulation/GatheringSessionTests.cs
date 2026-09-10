using System.Text.Json;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.GatheringWorldFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class GatheringSessionTests
{
    [Fact]
    public async Task NewProfileUsesDurableCutoffAndOwnToolProjectionWithoutPublishingPendingYield()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Create();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        LocalWorldView original = session.ReadView();
        Assert.Equal(1000U, original.PlayerGatherEffort);
        LocalGatherNodeView visible = Assert.Single(original.GatheringNodes);
        Assert.Equal(NodeId, visible.NodeId);
        Assert.Equal(GatherNodeKind.Oak, visible.Kind);
        Assert.Equal(GatherToolKind.Axe, visible.RequiredTool);
        Assert.Equal(GatherNodeVisibility.Ready, visible.Visibility);
        Assert.Equal(5U, visible.RemainingSwings);
        LocalGatherToolLotView tool = Assert.Single(original.GatheringTools);
        Assert.Equal(ToolLotId, tool.LotId);
        Assert.Equal(GatherToolKind.Axe, tool.Kind);
        session.SetReady(true);
        WorldActionCommand action = Swing(genesis);
        Task<WorldActionReceipt> pending = session.SubmitAction(action);
        Task<WorldActionReceipt> duplicate = session.SubmitAction(action);
        Assert.False(pending.IsCompleted);
        Assert.False(duplicate.IsCompleted);
        Assert.Equal(5U, session.ReadView().GatheringNodes.Single().RemainingSwings);
        session.SetReady(false);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.False(pending.IsCompleted);
        Assert.Equal(0UL, authority.ReadStateForHost().AdvancingSteps);
        session.SetReady(true);
        await Tick(time);
        WorldActionReceipt receipt = await pending;
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(receipt.ResultId, (await duplicate).ResultId);
        Assert.Equal(1UL, authority.ReadStateForHost().AdvancingSteps);
        Assert.Equal((ushort)2, authority.ReadStateForHost().ProfileVersion);
        Assert.Equal(610U, session.ReadView().PlayerGatherEffort);
        Assert.Equal(4U, session.ReadView().GatheringNodes.Single().RemainingSwings);
        Assert.Equal(5U, original.GatheringNodes.Single().RemainingSwings);
        Assert.Equal(receipt.ResultId, (await session.SubmitAction(action)).ResultId);
        Assert.Equal(1UL, authority.ReadStateForHost().AdvancingSteps);
        string publicJson = JsonSerializer.Serialize(session.ReadView());
        Assert.DoesNotContain("NextUnissuedUnit", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("UnitCapacity", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceId", publicJson, StringComparison.Ordinal);
        await session.StopAsync();
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(receipt.ResultId, reopened.FindReceipt(action, Delivery)!.ResultId);
        Assert.Equal(1UL, Node(reopened.InspectForHost().State).NextUnissuedUnit);
    }

    [Fact]
    public async Task ReadingHoldRejectsGatheringWithoutWorkOrCalendarProgress()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Create(effort: 400);
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        session.SetReady(true);
        Task<WorldActionReceipt> speaking = session.SubmitAction(Speak(genesis));
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await speaking).Status);
        WorldSnapshot held = authority.ReadStateForHost();
        Assert.True(session.ReadView().HasReadingHold);
        Assert.Equal(410U, session.ReadView().PlayerGatherEffort);
        Task<WorldActionReceipt> gathering = session.SubmitAction(Swing(held));
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Rejected, (await gathering).Status);
        WorldSnapshot after = authority.ReadStateForHost();
        Assert.Equal(held.AdvancingSteps, after.AdvancingSteps);
        Assert.Equal(held.Calendar, after.Calendar);
        Assert.Equal(410U, session.ReadView().PlayerGatherEffort);
        Assert.Equal(0UL, Node(after).NextUnissuedUnit);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(after.Revision, authority.ReadStateForHost().Revision);
        Assert.Equal(5U, session.ReadView().GatheringNodes.Single().RemainingSwings);
    }

    [Fact]
    public async Task LegacyProjectionHasNoInventedNodesToolsOrGatheringEffort()
    {
        using var files = new SessionFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, Genesis());
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        Assert.Empty(session.ReadView().GatheringNodes);
        Assert.Empty(session.ReadView().GatheringTools);
        Assert.Null(session.ReadView().PlayerGatherEffort);
        session.SetReady(true);
        await Tick(time);
        Assert.Equal((ushort)1, authority.ReadStateForHost().ProfileVersion);
        Assert.Empty(session.ReadView().GatheringNodes);
    }
}
