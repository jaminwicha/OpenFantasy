using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Hosting.Tests;

public sealed class SqliteCountedAuthorityTests
{
    [Fact]
    public void TransferRetryAndRestartRetainOneExactReceiptAndConservedInventory()
    {
        using var fixture = new AuthorityWorldFixture();
        CountedAuthorityState genesis = AuthorityWorldFixture.Genesis();
        CommandEnvelope command = AuthorityWorldFixture.Transfer(genesis);
        byte[] receipt;
        using (var host = SqliteCountedAuthority.CreateNew(fixture.DatabasePath, genesis))
        {
            receipt = CommandReceiptCodecV1.Encode(host.Execute(command, AuthorityWorldFixture.Player));
            Assert.Equal(receipt, CommandReceiptCodecV1.Encode(host.Execute(command, AuthorityWorldFixture.Player)));
            Assert.Equal(1UL, host.InspectForHost().CommitSequence);
        }
        using var reopened = SqliteCountedAuthority.OpenExisting(fixture.DatabasePath);
        Assert.Equal(receipt, CommandReceiptCodecV1.Encode(reopened.Execute(command, AuthorityWorldFixture.Player)));
        AuthorityInspection inspection = reopened.InspectForHost();
        Assert.Equal(1UL, inspection.CommitSequence);
        Assert.Equal(6UL, inspection.State.Custody.GetStoredQuantity(AuthorityWorldFixture.Id(3)));
        Assert.Equal(4UL, inspection.State.Custody.GetStoredQuantity(AuthorityWorldFixture.Id(4)));
        Assert.Equal(1UL, inspection.State.Custody.Revision);
    }

    [Fact]
    public void NpcUsesTheSameAuthorityAndCanReturnAReceivedLot()
    {
        using var fixture = new AuthorityWorldFixture();
        using var host = SqliteCountedAuthority.CreateNew(fixture.DatabasePath, AuthorityWorldFixture.Genesis());
        host.Execute(AuthorityWorldFixture.Transfer(host.InspectForHost().State), AuthorityWorldFixture.Player);
        CountedAuthorityState after = host.InspectForHost().State;
        EntityId received = Assert.Single(after.Custody.Lots, lot => lot.CustodianId == AuthorityWorldFixture.Id(4)).LotId;
        CommandReceipt receipt = host.Execute(AuthorityWorldFixture.Transfer(after, 101, received, npc: true), AuthorityWorldFixture.Npc);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(10UL, host.InspectForHost().State.Custody.GetStoredQuantity(AuthorityWorldFixture.Id(3)));
        Assert.Equal(0UL, host.InspectForHost().State.Custody.GetStoredQuantity(AuthorityWorldFixture.Id(4)));
    }

    [Fact]
    public void StaleFreshCommandIsDurablyRejectedWithoutChangingState()
    {
        using var fixture = new AuthorityWorldFixture();
        CountedAuthorityState genesis = AuthorityWorldFixture.Genesis();
        var stale = AuthorityWorldFixture.Transfer(genesis, 101);
        byte[] rejection;
        using (var host = SqliteCountedAuthority.CreateNew(fixture.DatabasePath, genesis))
        {
            host.Execute(AuthorityWorldFixture.Transfer(genesis), AuthorityWorldFixture.Player);
            var before = host.InspectForHost().State.ContentHash;
            CommandReceipt receipt = host.Execute(stale, AuthorityWorldFixture.Player);
            Assert.Equal(CommandReceiptStatus.Rejected, receipt.Status);
            Assert.Equal(before, host.InspectForHost().State.ContentHash);
            Assert.Equal(2UL, host.InspectForHost().CommitSequence);
            Assert.Equal(1UL, host.InspectForHost().State.Custody.Revision);
            rejection = CommandReceiptCodecV1.Encode(receipt);
        }
        using var reopened = SqliteCountedAuthority.OpenExisting(fixture.DatabasePath);
        Assert.Equal(rejection, CommandReceiptCodecV1.Encode(reopened.Execute(stale, AuthorityWorldFixture.Player)));
        Assert.Equal(2UL, reopened.InspectForHost().CommitSequence);
    }

    [Fact]
    public void ConflictingRetryAndUnauthenticatedRetryDoNotReadOrReplaceReceipt()
    {
        using var fixture = new AuthorityWorldFixture();
        var genesis = AuthorityWorldFixture.Genesis();
        using var host = SqliteCountedAuthority.CreateNew(fixture.DatabasePath, genesis);
        CommandEnvelope original = AuthorityWorldFixture.Transfer(genesis);
        host.Execute(original, AuthorityWorldFixture.Player);
        Assert.Throws<AuthorityDispatchException>(() => host.Execute(AuthorityWorldFixture.Transfer(genesis, quantity: 3), AuthorityWorldFixture.Player));
        Assert.Throws<AuthorityDispatchException>(() => host.Execute(original, AuthorityWorldFixture.Npc));
        Assert.Throws<AuthorityDispatchException>(() => host.Execute(original,
            new TrustedCommandDelivery(AuthorityWorldFixture.Player.PrincipalId, AuthorityWorldFixture.Id(99))));
        Assert.Equal(1UL, host.InspectForHost().CommitSequence);
    }

    [Theory]
    [InlineData(false, 0UL)]
    [InlineData(true, 1UL)]
    public void LostOutcomeRequiresReopenAndExactRetry(bool afterCommit, ulong committed)
    {
        using var fixture = new AuthorityWorldFixture();
        var genesis = AuthorityWorldFixture.Genesis();
        CommandEnvelope command = AuthorityWorldFixture.Transfer(genesis);
        using (var host = SqliteCountedAuthority.CreateNew(fixture.DatabasePath, genesis))
        {
            Assert.Throws<IOException>(() => host.Execute(command, AuthorityWorldFixture.Player, reached =>
            {
                if (reached == (afterCommit ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
                {
                    throw new IOException("Injected lost outcome");
                }
            }));
            Assert.Throws<InvalidOperationException>(() => host.InspectForHost());
        }
        using var reopened = SqliteCountedAuthority.OpenExisting(fixture.DatabasePath);
        Assert.Equal(committed, reopened.InspectForHost().CommitSequence);
        Assert.Equal(CommandReceiptStatus.Applied, reopened.Execute(command, AuthorityWorldFixture.Player).Status);
        Assert.Equal(1UL, reopened.InspectForHost().CommitSequence);
        Assert.Equal(4UL, reopened.InspectForHost().State.Custody.GetStoredQuantity(AuthorityWorldFixture.Id(4)));
    }

    [Fact]
    public void OnlyOneHostCanOwnAWorldAndCreatingNeverOverwrites()
    {
        using var fixture = new AuthorityWorldFixture();
        var genesis = AuthorityWorldFixture.Genesis();
        using (var host = SqliteCountedAuthority.CreateNew(fixture.DatabasePath, genesis))
        {
            Assert.Throws<IOException>(() => SqliteCountedAuthority.CreateNew(fixture.DatabasePath, genesis));
            Assert.ThrowsAny<Exception>(() => SqliteCountedAuthority.OpenExisting(fixture.DatabasePath));
            SqliteAuthorityDiagnostics database = host.InspectForHost().Database;
            Assert.Equal("3.53.4", database.Version);
            Assert.Equal("wal", database.JournalMode);
            Assert.Equal(2, database.Synchronous);
            Assert.Equal(1, database.ForeignKeys);
            Assert.Equal("exclusive", database.LockingMode);
            Assert.NotEmpty(database.SourceId);
        }
        using var reopened = SqliteCountedAuthority.OpenExisting(fixture.DatabasePath);
        Assert.Equal(0UL, reopened.InspectForHost().CommitSequence);
    }

    [Fact]
    public void MissingAndCorruptFilesNeverBecomeNewWorlds()
    {
        using var fixture = new AuthorityWorldFixture();
        Assert.Throws<FileNotFoundException>(() => SqliteCountedAuthority.OpenExisting(fixture.DatabasePath));
        Assert.False(File.Exists(fixture.DatabasePath));
        byte[] invalid = "not a world database"u8.ToArray();
        File.WriteAllBytes(fixture.DatabasePath, invalid);
        Assert.ThrowsAny<Exception>(() => SqliteCountedAuthority.OpenExisting(fixture.DatabasePath));
        Assert.Equal(invalid, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public void ConcurrentDeliveryOfOneCommandCommitsExactlyOnce()
    {
        using var fixture = new AuthorityWorldFixture();
        var genesis = AuthorityWorldFixture.Genesis();
        using var host = SqliteCountedAuthority.CreateNew(fixture.DatabasePath, genesis);
        CommandEnvelope command = AuthorityWorldFixture.Transfer(genesis);
        Parallel.For(0, 32, _ => Assert.Equal(1UL, host.Execute(command, AuthorityWorldFixture.Player).Commit.Sequence));
        Assert.Equal(1UL, host.InspectForHost().CommitSequence);
        Assert.Equal(4UL, host.InspectForHost().State.Custody.GetStoredQuantity(AuthorityWorldFixture.Id(4)));
    }

    [Fact]
    public void UnsignedRevisionSurvivesSqliteAboveSignedIntegerRange()
    {
        using var fixture = new AuthorityWorldFixture();
        using (var host = SqliteCountedAuthority.CreateNew(fixture.DatabasePath, AuthorityWorldFixture.Genesis(ulong.MaxValue - 1)))
        {
            host.Execute(AuthorityWorldFixture.Transfer(host.InspectForHost().State), AuthorityWorldFixture.Player);
        }
        using var reopened = SqliteCountedAuthority.OpenExisting(fixture.DatabasePath);
        Assert.Equal(ulong.MaxValue, reopened.InspectForHost().State.Custody.Revision);
    }
}
