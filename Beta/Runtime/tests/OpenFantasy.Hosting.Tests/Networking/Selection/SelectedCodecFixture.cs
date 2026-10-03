using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Networking.Selection;

internal sealed class SelectedCodecFixture : IDisposable
{
    private readonly StorageFiles _files = new();
    internal EffectiveControlFixture Source { get; } = new();
    internal SqliteWorldAuthority Authority { get; }
    internal WorldSnapshot State { get; }
    internal PlayerSelectedObservation Frame { get; }
    internal LocalActorSelection Selection => Frame.Observation.Selection;
    internal SelectedCodecFixture(WorldControlCapabilities capabilities = WorldControlCapabilities.All)
    {
        Authority = SqliteWorldAuthority.CreateNew(_files.Path, Source.Genesis);
        Source.Grant(Authority, capabilities);
        Source.LoseAndExpire(Authority);
        State = Source.Acquire(Authority);
        var witness = State.EffectiveControl!.Find(Source.Owner)!.Witness!;
        var selection = new LocalActorSelection(new(0x5e1ec7c0dec, 1), Source.Grantee, Source.Owner, 3, witness);
        var home = new LocalWorldView(State, Source.Grantee, regions: new LocalRegionObserver(Source.Grantee));
        var controlled = LocalControlledActorView.Project(State, Source.Grantee, Source.Delivery(Source.Grantee), witness,
            regions: new LocalRegionObserver(Source.Owner));
        Frame = new(new(0x5e1ec7c0dec, 2), 9, new(selection, home, controlled,
            new(PlayerControlState.Available, new(0x5e1ec7c0dec, 3), Source.Grantee, 0), false));
    }
    public void Dispose() { Authority.Dispose(); _files.Dispose(); }
}
