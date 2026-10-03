using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.PrivateHistoryDirectory;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.SharedHistory;

internal sealed class HistoryModelHarness : IAsyncDisposable
{
    private readonly StorageFiles _files;
    internal SqliteWorldAuthority Authority { get; }
    internal OwnedLocalPlayerSession Application { get; }

    internal HistoryModelHarness(PrivateHistoryDirectoryFixture fixture)
    {
        _files = fixture.Copy();
        Authority = SqliteWorldAuthority.OpenExisting(_files.Path, fixture.Genesis);
        var host = new LocalWorldSession(Authority, fixture.Trade.Owner, fixture.Trade.Control.Delivery(fixture.Trade.Owner),
            new ManualSessionTimeProvider());
        Application = new(host);
    }

    internal static Task Settle(FarmHistoryModel model) => SessionFixture.Until(() =>
    {
        model.Update();
        return !model.IsBusy;
    });

    public async ValueTask DisposeAsync()
    {
        await Application.DisposeAsync();
        _files.Dispose();
    }
}

/// <summary>Delays delivery after the real reader has authenticated its result; it does not create world facts.</summary>
internal sealed class GatedHistoryFacade(IPrivateHistoryPlayerSession source) : IPrivateHistoryPlayerSession
{
    internal TaskCompletionSource? PageGate { get; set; }
    internal TaskCompletionSource? DetailGate { get; set; }
    internal TaskCompletionSource PageCaptured { get; private set; } = Signal();
    internal TaskCompletionSource DetailCaptured { get; private set; } = Signal();
    internal int PageCalls { get; private set; }
    internal int QuoteCalls { get; private set; }
    internal int ProposalCalls { get; private set; }
    internal LocalPrivateHistoryPageRequest? LastPageRequest { get; private set; }
    internal Func<LocalSelectedQueryResult<LocalPrivateHistoryPage>, LocalSelectedQueryResult<LocalPrivateHistoryPage>>? PageTransform { get; set; }
    public bool SupportsPrivateHistoryDirectory => source.SupportsPrivateHistoryDirectory;
    public LocalSelectedPlayerObservation ReadPrivateHistoryObservation() => source.ReadPrivateHistoryObservation();

    public async Task<LocalSelectedQueryResult<LocalPrivateHistoryPage>> ReadSelectedPrivateHistoryAsync(
        LocalActorSelection observed, LocalPrivateHistoryPageRequest request)
    {
        PageCalls++;
        LastPageRequest = request;
        TaskCompletionSource? gate = PageGate;
        var result = await source.ReadSelectedPrivateHistoryAsync(observed, request);
        PageCaptured.TrySetResult();
        if (gate is not null) await gate.Task;
        return PageTransform?.Invoke(result) ?? result;
    }

    public async Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedPaidQuoteHistoryAsync(
        LocalActorSelection observed, EntityId subject)
    {
        QuoteCalls++;
        TaskCompletionSource? gate = DetailGate;
        var result = await source.InspectSelectedPaidQuoteHistoryAsync(observed, subject);
        DetailCaptured.TrySetResult();
        if (gate is not null) await gate.Task;
        return result;
    }

    public async Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedMaterialProposalHistoryAsync(
        LocalActorSelection observed, EntityId subject)
    {
        ProposalCalls++;
        TaskCompletionSource? gate = DetailGate;
        var result = await source.InspectSelectedMaterialProposalHistoryAsync(observed, subject);
        DetailCaptured.TrySetResult();
        if (gate is not null) await gate.Task;
        return result;
    }

    internal static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class PortHistoryFacade(LocalPlayerConnection player) : IPrivateHistoryPlayerSession
{
    public bool SupportsPrivateHistoryDirectory => true;
    public LocalSelectedPlayerObservation ReadPrivateHistoryObservation() => player.ReadSelectedObservation();
    public Task<LocalSelectedQueryResult<LocalPrivateHistoryPage>> ReadSelectedPrivateHistoryAsync(
        LocalActorSelection observed, LocalPrivateHistoryPageRequest request) => player.ReadSelectedPrivateHistoryAsync(observed, request);
    public Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedPaidQuoteHistoryAsync(
        LocalActorSelection observed, EntityId subject) => player.InspectSelectedPaidQuoteHistoryAsync(observed, subject);
    public Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedMaterialProposalHistoryAsync(
        LocalActorSelection observed, EntityId subject) => player.InspectSelectedMaterialProposalHistoryAsync(observed, subject);
}
