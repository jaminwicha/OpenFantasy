using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Hosting.Tests.Review;

public sealed class DurableAuthorityAdversarialTests
{
    [Theory]
    [InlineData("head")]
    [InlineData("journal")]
    [InlineData("identity")]
    public void SuppressedRequiredWriteMustNotProduceAnAcknowledgedCommit(string target)
    {
        using var fixture = new ReviewWorld();
        CountedAuthorityState genesis = ReviewWorld.Genesis();
        using (SqliteCountedAuthority.CreateNew(fixture.Path, genesis)) { }
        string mutation = target switch
        {
            "head" => "CREATE TRIGGER review_suppress BEFORE UPDATE ON world_head BEGIN SELECT RAISE(IGNORE); END;",
            "journal" => "CREATE TRIGGER review_suppress BEFORE INSERT ON command_journal BEGIN SELECT RAISE(IGNORE); END;",
            "identity" => "CREATE TRIGGER review_suppress BEFORE INSERT ON occupied_identity BEGIN SELECT RAISE(IGNORE); END;",
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
        fixture.MutateDatabase(mutation);

        // Refusing the altered schema at open, or refusing and rolling back its failed required
        // write, are both safe. Returning a receipt while a required row is missing is not.
        Assert.ThrowsAny<Exception>(() =>
        {
            using SqliteCountedAuthority authority = SqliteCountedAuthority.OpenExisting(fixture.Path);
            authority.Execute(ReviewWorld.Transfer(genesis, ReviewWorld.Id(100), 3), ReviewWorld.Delivery);
        });
        Assert.Equal(0L, fixture.ScalarLong("SELECT count(*) FROM command_journal"));
        Assert.Equal(0L, fixture.ScalarLong("SELECT count(*) FROM occupied_identity"));
    }

    [Fact]
    public void RecoveryRejectsChangedCommandBytesEvenWhenStoredSnapshotChecksumsStillMatch()
    {
        using var fixture = new ReviewWorld();
        CountedAuthorityState genesis = ReviewWorld.Genesis();
        EntityId commandId = ReviewWorld.Id(110);
        using (SqliteCountedAuthority authority = SqliteCountedAuthority.CreateNew(fixture.Path, genesis))
        {
            Assert.Equal(CommandReceiptStatus.Applied,
                authority.Execute(ReviewWorld.Transfer(genesis, commandId, 3), ReviewWorld.Delivery).Status);
        }

        byte[] changedCommand = CommandCodecV1.Encode(ReviewWorld.Transfer(genesis, commandId, 4));
        fixture.MutateDatabase("UPDATE command_journal SET command=$bytes", changedCommand);
        Assert.Throws<InvalidDataException>(() => SqliteCountedAuthority.OpenExisting(fixture.Path));
    }

    [Fact]
    public void RecoveryRejectsAnIncompleteIdentityRegistryWithoutChangingTheStoredWorld()
    {
        using var fixture = new ReviewWorld();
        CountedAuthorityState genesis = ReviewWorld.Genesis();
        using (SqliteCountedAuthority authority = SqliteCountedAuthority.CreateNew(fixture.Path, genesis))
        {
            authority.Execute(ReviewWorld.Transfer(genesis, ReviewWorld.Id(120), 3), ReviewWorld.Delivery);
        }

        fixture.MutateDatabase("DELETE FROM occupied_identity WHERE kind=3");
        Assert.Throws<InvalidDataException>(() => SqliteCountedAuthority.OpenExisting(fixture.Path));
        Assert.Equal(1L, fixture.ScalarLong("SELECT count(*) FROM command_journal"));
        Assert.Equal(2L, fixture.ScalarLong("SELECT count(*) FROM occupied_identity"));
    }

    [Fact]
    public void UnsignedHighBitsInStateAndSourceIntervalsSurviveCommitAndRecovery()
    {
        using var fixture = new ReviewWorld();
        ulong revision = (ulong)long.MaxValue + 101;
        CountedAuthorityState genesis = ReviewWorld.Genesis(revision, ulong.MaxValue - 8, ulong.MaxValue);
        CommandEnvelope command = ReviewWorld.Transfer(genesis, ReviewWorld.Id(130), 3);
        byte[] committedState;
        byte[] receiptBytes;
        using (SqliteCountedAuthority authority = SqliteCountedAuthority.CreateNew(fixture.Path, genesis))
        {
            CommandReceipt receipt = authority.Execute(command, ReviewWorld.Delivery);
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            AuthorityInspection inspection = authority.InspectForHost();
            Assert.Equal(revision + 1, inspection.State.Custody.Revision);
            Assert.Equal(ulong.MaxValue - 5, inspection.State.Custody.GetLot(ReviewWorld.Lot).Source.Start);
            Assert.Equal(ulong.MaxValue, inspection.State.Custody.GetLot(ReviewWorld.Lot).Source.End);
            Assert.Equal(3UL, inspection.State.Custody.GetStoredQuantity(ReviewWorld.SecondStorage));
            committedState = AuthorityEncodingV1.Encode(inspection.State);
            receiptBytes = CommandReceiptCodecV1.Encode(receipt);
        }

        using SqliteCountedAuthority restored = SqliteCountedAuthority.OpenExisting(fixture.Path);
        Assert.Equal(committedState, AuthorityEncodingV1.Encode(restored.InspectForHost().State));
        Assert.Equal(receiptBytes, CommandReceiptCodecV1.Encode(restored.Execute(command, ReviewWorld.Delivery)));
        Assert.Equal(1UL, restored.InspectForHost().CommitSequence);
    }

    [Fact]
    public void APreviousCommandIdentityCannotBecomeALaterSplitChild()
    {
        using var fixture = new ReviewWorld();
        CountedAuthorityState genesis = ReviewWorld.Genesis();
        EntityId laterCommandId = ReviewWorld.Id(140);
        EntityId historicalCommandId = EntityId.DeriveSpawned(laterCommandId, 0);
        CommandEnvelope? conflictingSplit = null;
        byte[] rejectionBytes;
        byte[] unchanged;
        using (SqliteCountedAuthority authority = SqliteCountedAuthority.CreateNew(fixture.Path, genesis))
        {
            Assert.Equal(CommandReceiptStatus.Applied,
                authority.Execute(ReviewWorld.Transfer(genesis, historicalCommandId, 8), ReviewWorld.Delivery).Status);
            CountedAuthorityState current = authority.InspectForHost().State;
            unchanged = AuthorityEncodingV1.Encode(current);
            conflictingSplit = ReviewWorld.Transfer(current, laterCommandId, 3,
                ReviewWorld.SecondStorage, ReviewWorld.FirstStorage);
            CommandReceipt result = authority.Execute(conflictingSplit, ReviewWorld.Delivery);
            Assert.Equal(CommandReceiptStatus.Rejected, result.Status);
            Assert.Equal(CommandRejectionReason.ResourceUnavailable, result.RejectionReason);
            Assert.Equal(unchanged, AuthorityEncodingV1.Encode(authority.InspectForHost().State));
            Assert.Equal(2UL, authority.InspectForHost().CommitSequence);
            rejectionBytes = CommandReceiptCodecV1.Encode(result);
        }

        using SqliteCountedAuthority restored = SqliteCountedAuthority.OpenExisting(fixture.Path);
        Assert.Equal(rejectionBytes, CommandReceiptCodecV1.Encode(restored.Execute(conflictingSplit, ReviewWorld.Delivery)));
        Assert.Equal(unchanged, AuthorityEncodingV1.Encode(restored.InspectForHost().State));
        Assert.Equal(2UL, restored.InspectForHost().CommitSequence);
    }

    [Fact]
    public void ExistingReceiptDoesNotBypassCurrentDeliveryPrincipalOrSessionChecks()
    {
        using var fixture = new ReviewWorld();
        CountedAuthorityState genesis = ReviewWorld.Genesis();
        CommandEnvelope command = ReviewWorld.Transfer(genesis, ReviewWorld.Id(150), 3);
        using SqliteCountedAuthority authority = SqliteCountedAuthority.CreateNew(fixture.Path, genesis);
        byte[] expected = CommandReceiptCodecV1.Encode(authority.Execute(command, ReviewWorld.Delivery));
        string? message = null;
        foreach (TrustedCommandDelivery denied in new[]
        {
            new TrustedCommandDelivery(ReviewWorld.Id(200), ReviewWorld.Delivery.SessionId),
            new TrustedCommandDelivery(ReviewWorld.Delivery.PrincipalId, ReviewWorld.Id(201)),
            new TrustedCommandDelivery(ReviewWorld.Id(200), ReviewWorld.Id(201))
        })
        {
            AuthorityDispatchException error = Assert.Throws<AuthorityDispatchException>(() => authority.Execute(command, denied));
            message ??= error.Message;
            Assert.Equal(message, error.Message);
            Assert.DoesNotContain(command.CommandId.ToString(), error.Message, StringComparison.Ordinal);
        }

        Assert.Equal(expected, CommandReceiptCodecV1.Encode(authority.Execute(command, ReviewWorld.Delivery)));
        Assert.Equal(1UL, authority.InspectForHost().CommitSequence);
    }

    [Fact]
    public async Task CompetingCallersCannotBothSpendTheSameSourceUnits()
    {
        using var fixture = new ReviewWorld();
        CountedAuthorityState genesis = ReviewWorld.Genesis();
        CommandEnvelope first = ReviewWorld.Transfer(genesis, ReviewWorld.Id(160), 6);
        CommandEnvelope second = ReviewWorld.Transfer(genesis, ReviewWorld.Id(161), 6);
        byte[] finalState;
        using (SqliteCountedAuthority authority = SqliteCountedAuthority.CreateNew(fixture.Path, genesis))
        {
            CommandReceipt[] results = await Task.WhenAll(
                Task.Run(() => authority.Execute(first, ReviewWorld.Delivery)),
                Task.Run(() => authority.Execute(second, ReviewWorld.Delivery)));
            Assert.Single(results, result => result.Status == CommandReceiptStatus.Applied);
            Assert.Single(results, result => result.Status == CommandReceiptStatus.Rejected);
            Assert.Equal(new ulong[] { 1, 2 }, results.Select(result => result.Commit.Sequence).Order());
            AuthorityInspection inspection = authority.InspectForHost();
            Assert.Equal(2UL, inspection.State.Custody.GetStoredQuantity(ReviewWorld.FirstStorage));
            Assert.Equal(6UL, inspection.State.Custody.GetStoredQuantity(ReviewWorld.SecondStorage));
            Assert.Equal(2UL, inspection.CommitSequence);
            finalState = AuthorityEncodingV1.Encode(inspection.State);
        }

        using SqliteCountedAuthority restored = SqliteCountedAuthority.OpenExisting(fixture.Path);
        Assert.Equal(finalState, AuthorityEncodingV1.Encode(restored.InspectForHost().State));
    }
}

internal sealed class ReviewWorld : IDisposable
{
    internal static EntityId Id(ulong value) => new(0, value);
    internal static readonly EntityId FirstStorage = Id(3);
    internal static readonly EntityId SecondStorage = Id(4);
    internal static readonly EntityId Lot = Id(5);
    internal static readonly TrustedCommandDelivery Delivery = new(Id(8), Id(9));
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openfantasy-review-" + Guid.NewGuid().ToString("N"));

    internal ReviewWorld()
    {
        Directory.CreateDirectory(_directory);
        Path = System.IO.Path.Combine(_directory, "world.sqlite3");
    }

    internal string Path { get; }

    internal static CountedAuthorityState Genesis(ulong revision = 7, ulong intervalStart = 0, ulong intervalEnd = 8)
    {
        var definition = new CountedItemDefinition(Id(7), ContentHash.Compute("review-counted-wood-v1"u8));
        var custody = new CustodySnapshot(Id(1), Id(2), revision,
            [new(FirstStorage, ulong.MaxValue, revision + 1), new(SecondStorage, ulong.MaxValue, revision + 2)],
            [new(Lot, FirstStorage, definition, new SourceInterval(Id(6), intervalStart, intervalEnd), revision + 3)]);
        return new CountedAuthorityState(custody, ulong.MaxValue, [Id(10)],
            [new(Delivery.PrincipalId, Delivery.SessionId, Id(10))],
            [new(FirstStorage, Id(10), CustodyDepositRule.OwnerOnly), new(SecondStorage, Id(10), CustodyDepositRule.OwnerOnly)],
            [new(Id(6), definition, intervalEnd)]);
    }

    internal static CommandEnvelope Transfer(CountedAuthorityState state, EntityId id, ulong quantity,
        EntityId? source = null, EntityId? destination = null)
    {
        EntityId from = source ?? FirstStorage;
        EntityId to = destination ?? SecondStorage;
        return new CommandEnvelope(id, Delivery.PrincipalId, Delivery.SessionId, Id(10), state.WorldId, state.BranchId,
            [new(RevisionTargetKind.Branch, state.BranchId, state.Custody.Revision),
                new(RevisionTargetKind.Entity, Lot, state.Custody.GetLot(Lot).Revision),
                new(RevisionTargetKind.Entity, from, state.Custody.GetStorage(from).Revision),
                new(RevisionTargetKind.Entity, to, state.Custody.GetStorage(to).Revision)],
            new TransferLotIntent(Lot, from, to, quantity));
    }

    internal void MutateDatabase(string sql, byte[]? bytes = null)
    {
        using SqliteConnection connection = OpenRaw();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (bytes is not null) command.Parameters.AddWithValue("$bytes", bytes);
        command.ExecuteNonQuery();
    }

    internal long ScalarLong(string sql)
    {
        using SqliteConnection connection = OpenRaw();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    private SqliteConnection OpenRaw()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
