using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalArchiveTests
{
    [Fact]
    public void Inherited_and_next_ordinary_cut_preserve_open_proposal_delegation_receipts_and_original_history()
    {
        var fixture = new EffectiveControlFixture();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        List<(WorldActionCommand Command, byte[] Receipt)> receipts = [];
        WorldSnapshot before;
        WorldMaterialProposal pending;
        using (var authority = SqliteWorldAuthority.CreateNew(source.Path, fixture.Genesis))
        {
            fixture.Grant(authority, WorldControlCapabilities.All);
            fixture.LoseAndExpire(authority);
            fixture.Acquire(authority);
            var command = Propose(authority);
            pending = authority.ReadStateForHost().Coordination!.Proposals!.FindOpen(
                WorldMaterialProposal.DeriveIdentity(command.CommandId))!;
            Assert.NotNull(pending);
            ReachNextCut(authority, 0);
            before = authority.ReadStateForHost();
            Assert.Equal(pending.Encode(), before.Coordination!.Proposals!.FindOpen(pending.ProposalId)!.Encode());
        }
        string[] inherited;
        using (var database = WorldDatabase.Open(source.Path, false))
            inherited = database.ReadMaterialCareArchives().Select(row => JsonSerializer.Serialize(row)).ToArray();
        Assert.Single(inherited);
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, destination.Path,
            WorldPartyWithdrawalTransition.ForHead(before, fixture.Commands.NextId())))
        {
            var migrated = authority.ReadStateForHost();
            Assert.Equal(JsonSerializer.Serialize(before.Coordination), JsonSerializer.Serialize(migrated.Coordination));
            Assert.Equal(before.EffectiveControl!.ContentHash, migrated.EffectiveControl!.ContentHash);
            Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            ReachNextCut(authority, 1);
            final = authority.ReadStateForHost();
            Assert.Equal(pending.Encode(), final.Coordination!.Proposals!.FindOpen(pending.ProposalId)!.Encode());
            Assert.Equal(before.EffectiveControl.ContentHash, final.EffectiveControl!.ContentHash);
            Assert.Equal(before.Inventory.ContentHash, final.Inventory.ContentHash);
            Assert.Equal(before.ContentHash, authority.ReadRecordedStateForHost(before.Revision).State.ContentHash);
        }
        using (var database = WorldDatabase.Open(destination.Path, false))
        {
            Assert.Equal(inherited, database.ReadMaterialCareArchives().Take(1).Select(row => JsonSerializer.Serialize(row)));
            Assert.Equal(2, database.ReadMaterialCareArchives().Count());
            Assert.Equal(WorldSnapshotCodec.Encode(fixture.Genesis), database.ReadGenesis());
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(destination.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(pending.Encode(), reopened.ReadStateForHost().Coordination!.Proposals!.FindOpen(pending.ProposalId)!.Encode());
        foreach (var (command, receipt) in receipts)
        {
            Assert.True(reopened.IsIdentityOccupiedForHost(command.CommandId));
            Assert.Equal(receipt, WorldReceiptCodecV1.Encode(reopened.SubmitMaterialProposalCommand(command,
                fixture.Delivery(fixture.Grantee), fixture.Commands.NextId())));
        }
        Assert.Equal(final.ContentHash, reopened.ReadStateForHost().ContentHash);
        // Both generations remain inspectable by their actual closing state and original journal.
        for (ulong ordinal = 0; ordinal < 2; ordinal++)
        {
            var archived = reopened.ReadMaterialCareArchiveForHost(ordinal);
            Assert.Equal(ordinal == 0 ? (ushort)40 : (ushort)41, archived.Segment.Continued.ProfileVersion);
            Assert.Equal(archived.Segment.Encode(), WorldSnapshotCodec.Encode(
                reopened.ReadRecordedStateForHost(archived.Segment.Reference.BoundarySequence).State));
        }

        WorldActionCommand Propose(SqliteWorldAuthority authority)
        {
            var state = authority.ReadStateForHost();
            var lot = state.Inventory.Custody.Lots.First(row =>
                row.CustodianId == state.GetResident(fixture.Owner).InventoryCustodianId && row.Quantity > 0 && row.ReservedBy is null);
            var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, fixture.Owner, fixture.Grantee,
                ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
                state.GetResident(fixture.Grantee).InventoryCustodianId, 1, lot.Revision);
            var command = fixture.Delegated(state, new ProposeMaterialIntent(terms, state.AdvancingSteps + 10_000));
            Submit(authority, command);
            return command;
        }

        void Submit(SqliteWorldAuthority authority, WorldActionCommand command)
        {
            var receipt = authority.SubmitMaterialProposalCommand(command, fixture.Delivery(fixture.Grantee), fixture.Commands.NextId());
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            receipts.Add((command, WorldReceiptCodecV1.Encode(receipt)));
        }

        void ReachNextCut(SqliteWorldAuthority authority, ulong count)
        {
            for (int attempt = 0; authority.InspectForHost().Journal.MaterialCareArchiveCount == count; attempt++)
            {
                Assert.True(attempt < WorldMaterialProposalBook.MaximumOutcomes,
                    "Real proposal closures must reach the unchanged ordinary continuation policy.");
                var command = Propose(authority);
                Submit(authority, fixture.Delegated(authority.ReadStateForHost(),
                    new WithdrawMaterialProposalIntent(WorldMaterialProposal.DeriveIdentity(command.CommandId))));
            }
            Assert.Equal(count + 1, authority.InspectForHost().Journal.MaterialCareArchiveCount);
        }
    }
}
