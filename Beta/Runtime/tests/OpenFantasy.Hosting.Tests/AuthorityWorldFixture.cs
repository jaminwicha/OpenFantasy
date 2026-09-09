using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Hosting.Tests;

internal sealed class AuthorityWorldFixture : IDisposable
{
    internal AuthorityWorldFixture()
    {
        Directory.CreateDirectory(DirectoryPath);
    }

    internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "openfantasy-authority-" + Guid.NewGuid().ToString("N"));
    internal string DatabasePath => Path.Combine(DirectoryPath, "world.sqlite");
    internal static EntityId Id(ulong value) => new(0, value);
    internal static TrustedCommandDelivery Player => new(Id(20), Id(21));
    internal static TrustedCommandDelivery Npc => new(Id(30), Id(31));

    internal static CountedAuthorityState Genesis(ulong revision = 0)
    {
        var definition = new CountedItemDefinition(Id(10), ContentHash.Compute("wood-v1"u8));
        var custody = new CustodySnapshot(Id(1), Id(2), revision,
            [new(Id(3), 100, 0), new(Id(4), 100, 0)],
            [new(Id(5), Id(3), definition, new SourceInterval(Id(6), 0, 10), 0)]);
        return new CountedAuthorityState(custody, 10, [Id(22), Id(32)],
            [new(Player.PrincipalId, Player.SessionId, Id(22)), new(Npc.PrincipalId, Npc.SessionId, Id(32))],
            [new(Id(3), Id(22), CustodyDepositRule.AnyControlledActor), new(Id(4), Id(32), CustodyDepositRule.AnyControlledActor)],
            [new(Id(6), definition, 10)]);
    }

    internal static CommandEnvelope Transfer(CountedAuthorityState state, ulong commandId = 100,
        EntityId? lotId = null, ulong quantity = 4, bool npc = false)
    {
        CountedSourceLot lot = state.Custody.GetLot(lotId ?? Id(5));
        EntityId destination = lot.CustodianId == Id(3) ? Id(4) : Id(3);
        TrustedCommandDelivery delivery = npc ? Npc : Player;
        return new CommandEnvelope(Id(commandId), delivery.PrincipalId, delivery.SessionId, npc ? Id(32) : Id(22),
            state.WorldId, state.BranchId,
            [new(RevisionTargetKind.Branch, state.BranchId, state.Custody.Revision),
                new(RevisionTargetKind.Entity, lot.LotId, lot.Revision),
                new(RevisionTargetKind.Entity, lot.CustodianId, state.Custody.GetStorage(lot.CustodianId).Revision),
                new(RevisionTargetKind.Entity, destination, state.Custody.GetStorage(destination).Revision)],
            new TransferLotIntent(lot.LotId, lot.CustodianId, destination, quantity));
    }

    public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
}
