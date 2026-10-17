using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalRecoveryTests
{
    [Fact]
    public void Paid_samples_partial_arrival_and_collective_departure_reopen_with_exact_history_and_receipts()
    {
        using var journal = new WithdrawalJournal();
        var run = journal.Run;
        run.Choose(run.Humans.Select(run.Retreat).ToArray());
        journal.Flush();
        journal.Reopen();
        bool moved = false, arrived = false, attacked = false;
        for (int step = 0; run.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active; step++)
        {
            Assert.True(step < 1200);
            run.ChooseDue();
            run.ResolveOrTick();
            journal.Flush();
            attacked |= run.State.EncounterActions?.LastResolution?.Status == EncounterActionResolutionStatus.Damaged;
            var group = run.State.Retreat!.Withdrawal!.Parties.SingleOrDefault();
            if (!moved && group?.Members.Any(member => member.SamplesWalked > 0 && !member.HasArrived) == true)
            {
                moved = true;
                journal.Reopen();
            }
            if (!arrived && group?.Members.Any(member => member.HasArrived) == true && !group.AllArrived)
            {
                arrived = true;
                journal.Reopen();
                Assert.Equal(EncounterLifecyclePhase.Active, journal.Authority.ReadStateForHost().EncounterLifecycle!.Phase);
            }
        }
        Assert.True(moved && arrived && attacked, "The journal must contain paid movement, partial arrival and actual opposing damage.");
        Assert.Null(run.State.EncounterOutcome!.Last);
        Assert.All(run.State.Retreat!.Withdrawal!.Last!.Ended,
            end => Assert.Equal(EncounterWithdrawalEndReason.Disengaged, end.Reason));
        journal.Reopen();
    }

    [Theory]
    [InlineData("sample", false)]
    [InlineData("sample", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    [InlineData("departure", false)]
    [InlineData("departure", true)]
    public void Interrupted_paid_boundary_recovers_one_complete_transaction_and_can_continue(string target, bool committed) =>
        ExerciseInterruptedBoundary(target, committed, processKill: false);

    internal static void ExerciseInterruptedBoundary(string target, bool committed, bool processKill)
    {
        using var journal = new WithdrawalJournal();
        var run = journal.Run;
        run.Choose(run.Humans.Select(run.Retreat).ToArray());
        journal.Flush();
        bool selected = false;
        for (int step = 0; !selected; step++)
        {
            Assert.True(step < 1200);
            run.ChooseDue(attacks: false);
            journal.Flush();
            var before = run.State;
            var walking = before.Retreat!.Withdrawal!.Parties.SelectMany(party => party.Members)
                .FirstOrDefault(member => member.SamplesWalked > 0 && !member.HasArrived);
            if (target == "cancel" && walking is not null)
            {
                run.Choose(run.Command(walking.ActorId, new CancelEncounterWithdrawalIntent(
                    before.Pacing!.Encounter!.EncounterId, before.Pacing.Encounter.Revision, walking.Command.CommandId)));
                Assert.Equal(before.Calendar, run.State.Calendar);
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Space), System.Text.Json.JsonSerializer.Serialize(run.State.Space));
                Assert.Equal(before.Pacing.Encounter.Actors, run.State.Pacing!.Encounter!.Actors);
                selected = true;
            }
            else
            {
                run.ResolveOrTick();
                selected = target == "departure"
                    ? run.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration
                    : target == "sample" && before.Space.Actors.Any(actor => actor.Feet != run.State.Space.GetActor(actor.ActorId).Feet);
            }
            if (!selected)
            {
                journal.Flush();
                continue;
            }
            var batch = run.Batches[^1];
            var expected = journal.Authority.Preview(batch);
            if (processKill) journal.KillHostDuringCommit(batch, target, committed, before, run.State);
            else
            {
                Assert.Throws<IOException>(() => journal.Authority.Commit(batch, boundary =>
                {
                    if (boundary == (committed ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
                        throw new IOException("Injected withdrawal publication interruption.");
                }));
                Assert.Throws<InvalidOperationException>(() => journal.Authority.ReadStateForHost());
            }
            journal.Reopen(committed ? run.State : before, verifyReceipts: false);
            Assert.Equal(committed, journal.Authority.IsIdentityOccupiedForHost(batch.CauseId));
            foreach (var command in batch.Actions)
            {
                Assert.Equal(committed, journal.Authority.IsIdentityOccupiedForHost(command.CommandId));
                Assert.Equal(committed, journal.Authority.FindReceipt(command, run.Delivery(command.ActorId)) is not null);
            }
            if (!committed) journal.Authority.Commit(batch);
            Assert.Throws<AuthorityDispatchException>(() => journal.Authority.Commit(batch));
            journal.AcceptRecoveredBatch(expected);
            journal.Reopen();
        }
        // Continue the recovered authority with the very next lawful batch, preserving all existing debts.
        if (run.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active)
        {
            run.ChooseDue(attacks: false);
            run.ResolveOrTick();
        }
        else run.Apply(WorldBatchKind.AdvancingStep);
        journal.Flush();
        journal.Reopen();
    }

    [Fact]
    public void Saved_transition_preserves_a_live_delegation_and_later_revocation_through_original_genesis_replay()
    {
        var fixture = new EffectiveControlFixture();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot before;
        using (var authority = SqliteWorldAuthority.CreateNew(source.Path, fixture.Genesis))
        {
            fixture.Grant(authority);
            fixture.LoseAndExpire(authority);
            before = fixture.Acquire(authority);
        }
        var witness = before.EffectiveControl!.Find(fixture.Owner)!.Witness!;
        Assert.NotNull(witness);
        WorldSnapshot revoked;
        using (var authority = SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, destination.Path,
            WorldPartyWithdrawalTransition.ForHead(before, fixture.Commands.NextId())))
        {
            var after = authority.ReadStateForHost();
            Assert.Equal(before.EffectiveControl.ContentHash, after.EffectiveControl!.ContentHash);
            Assert.Equal(before.ControlPermissions!.ContentHash, after.ControlPermissions!.ContentHash);
            Assert.Equal(before.Controllers!.ContentHash, after.Controllers!.ContentHash);
            Assert.Equal(witness, after.EffectiveControl.Find(fixture.Owner)!.Witness);
            revoked = fixture.Revoke(authority);
            Assert.Null(revoked.EffectiveControl!.Find(fixture.Owner)!.Witness);
            Assert.Equal(before.Calendar, revoked.Calendar);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(destination.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(revoked), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(before.ContentHash, reopened.ReadRecordedStateForHost(before.Revision).State.ContentHash);
        Assert.Equal(witness, reopened.ReadRecordedStateForHost(before.Revision + 1).State.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.True(reopened.IsIdentityOccupiedForHost(witness.AssignmentId));
    }

    /// <summary>Publishes the ordinary Core journey through SQLite; no prepared head or synthetic receipts are installed.</summary>
    private sealed class WithdrawalJournal : IDisposable
    {
        private readonly StorageFiles _files = new();
        private int _published;
        private readonly Dictionary<EntityId, byte[]> _receipts = [];
        internal WithdrawalWorldFixture Run { get; } = WithdrawalWorldFixture.Active();
        internal SqliteWorldAuthority Authority { get; private set; }

        internal WithdrawalJournal()
        {
            Authority = SqliteWorldAuthority.CreateNew(_files.Path, Run.Genesis);
            Flush();
        }

        internal void Flush()
        {
            while (_published < Run.Batches.Count)
            {
                var result = Authority.Commit(Run.Batches[_published++]);
                RetainReceipts(result);
                Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            }
            Assert.Equal(Run.State.ContentHash, Authority.ReadStateForHost().ContentHash);
        }

        private void RetainReceipts(WorldStepDecision result)
        {
            foreach (var receipt in result.Receipts)
                _receipts.Add(receipt.CommandId, WorldReceiptCodecV1.Encode(receipt));
        }

        internal void AcceptRecoveredBatch(WorldStepDecision result)
        {
            Assert.Equal(_published + 1, Run.Batches.Count);
            RetainReceipts(result);
            _published++;
        }

        internal void KillHostDuringCommit(WorldStepBatch batch, string target, bool committed,
            WorldSnapshot before, WorldSnapshot after)
        {
            Authority.Dispose();
            PartyWithdrawalProcessTests.KillAtCommit(_files.Path, batch, target, committed, before, after);
        }

        internal void Reopen(WorldSnapshot? expected = null, bool verifyReceipts = true)
        {
            expected ??= Run.State;
            Authority.Dispose();
            Authority = SqliteWorldAuthority.OpenExisting(_files.Path, WorldSnapshotCodec.Encode(Run.Genesis));
            Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(Authority.ReadStateForHost()));
            Assert.Equal(expected.ContentHash, Authority.ReadRecordedStateForHost(expected.Revision).State.ContentHash);
            Assert.Equal(Run.Genesis.ContentHash, Authority.ReadRecordedStateForHost(0).State.ContentHash);
            if (!verifyReceipts) return;
            foreach (var batch in Run.Batches.Take(_published))
            {
                Assert.True(Authority.IsIdentityOccupiedForHost(batch.CauseId));
                foreach (var command in batch.Actions)
                {
                    Assert.True(Authority.IsIdentityOccupiedForHost(command.CommandId));
                    var receipt = Authority.FindReceipt(command, Run.Delivery(command.ActorId));
                    Assert.NotNull(receipt);
                    Assert.Equal(_receipts[command.CommandId], WorldReceiptCodecV1.Encode(receipt));
                }
            }
        }

        public void Dispose()
        {
            Authority.Dispose();
            _files.Dispose();
        }
    }
}
