using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange;

public sealed partial class PaidWorldExchangeTests
{
    [Fact]
    public void Funded_people_walk_apart_refuse_payment_then_return_to_the_same_quote()
    {
        WorldSnapshot genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        var session = new CareWorldSession(genesis);
        WorldSnapshot state = EarnReward(journey, session);
        var lot = SellableRawLot(state, journey.Helper);
        var reserve = state.Progression!.Currency.Accounts.Single(value => value.Kind == CurrencyAccountKind.Reserve);
        var (issue, quote) = IssueCommand(state, journey.Helper, journey.Player, lot, 1, reserve.AccountId, 1, 1024);
        state = Commit(session, state, issue, WorldActionRejection.None);
        bool Reachable() => state.Space.QueryInteraction(journey.Player, journey.Helper) == SpatialInteraction.Reachable;
        void Walk(long x, long y)
        {
            var control = state.Inventory.Controls.Single(value => value.ActorId == journey.Player);
            state = session.Commit(new WorldStepBatch(NextId(), state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, WorldBatchKind.AdvancingStep,
                [new(new(control.PrincipalId, control.SessionId), journey.Player, x, y)], [], state.ProfileVersion)).Candidate;
        }
        Assert.True(Reachable());
        (long X, long Y) away = default;
        foreach (var direction in new (long X, long Y)[] { (32, 0), (-32, 0), (0, 32), (0, -32) })
        {
            for (int step = 0; step < 24 && Reachable(); step++) Walk(direction.X, direction.Y);
            if (Reachable()) continue;
            away = direction;
            break;
        }
        Assert.False(Reachable(), "Actual movement must establish the distant-body precondition.");
        var before = state;
        state = Commit(session, state, AcceptCommand(state, journey.Player, quote, journey.Helper, 1, 1),
            WorldActionRejection.OutOfReach);
        AssertOwnersUnchanged(before, state);
        for (int step = 0; step < 48 && !Reachable(); step++) Walk(-away.X, -away.Y);
        Assert.True(Reachable());
        var accepted = AcceptCommand(state, journey.Player, quote, journey.Helper, 1, 1);
        state = Commit(session, state, accepted, WorldActionRejection.None);
        Assert.Equal(quote.ContentHash, Assert.Single(state.Coordination!.PaidExchange!.Outcomes).Quote.Quote.ContentHash);
    }

    [Fact]
    public void Quote_and_payment_identities_are_known_to_the_root_and_cannot_alias_other_roles()
    {
        WorldSnapshot genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        var session = new CareWorldSession(genesis);
        var state = EarnReward(journey, session);
        var lot = SellableRawLot(state, journey.Helper);
        var reserve = state.Progression!.Currency.Accounts.Single(value => value.Kind == CurrencyAccountKind.Reserve);
        var (issue, quote) = IssueCommand(state, journey.Helper, journey.Player, lot, 1, reserve.AccountId, 1);
        state = Commit(session, state, issue, WorldActionRejection.None);
        Assert.True(state.IdentityIndex.Contains(quote.QuoteId));
        Assert.True(state.IdentityIndex.Contains(issue.CommandId));
        var aliased = CloneQuote(quote, WorldPaidQuote.DeriveIdentity(journey.Player));
        var book = new WorldPaidExchangeBook([new WorldPaidQuote(journey.Player, aliased, state.Revision)], [], []);
        byte[] forged = ReplaceBook(state, book);
        var error = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(forged));
        Assert.Contains("aliases incompatible roles", error.ToString());
        var beforeActors = state.Progression!.Actors.Select(value => (value.ActorId, value.Revision, value.Level,
            value.Experience, value.CurrentHp, value.CurrentMp, value.Stats)).ToArray();
        var accept = AcceptCommand(state, journey.Player, quote, journey.Helper, 1, 1);
        state = Commit(session, state, accept, WorldActionRejection.None);
        Assert.True(state.IdentityIndex.Contains(issue.CommandId));
        Assert.True(state.IdentityIndex.Contains(quote.QuoteId));
        Assert.True(state.IdentityIndex.Contains(accept.CommandId));
        Assert.True(state.IdentityIndex.Contains(WorldPaidQuote.SplitChildOf(accept.CommandId)));
        Assert.Equal(beforeActors, state.Progression!.Actors.Select(value => (value.ActorId, value.Revision, value.Level,
            value.Experience, value.CurrentHp, value.CurrentMp, value.Stats)).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Paid_world_keeps_real_saved_gift_ingress_and_refuses_issuer_work_conflicts(bool stalePayment)
    {
        WorldSnapshot genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var session = new CareWorldSession(authority);
        var state = EarnReward(journey, session);
        var lot = SellableRawLot(state, journey.Helper);
        var reserve = state.Progression!.Currency.Accounts.Single(value => value.Kind == CurrencyAccountKind.Reserve);
        var (issue, quote) = IssueCommand(state, journey.Helper, journey.Player, lot, 1, reserve.AccountId, 1);
        state = Commit(session, state, issue, WorldActionRejection.None);
        // Gift consent remains its own actor submission, even in a world that also interprets prices.
        var giftTerms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, journey.Helper, journey.Player,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(journey.Player).InventoryCustodianId, 1, lot.Revision);
        var gift = journey.Command(state, journey.Helper, new ProposeMaterialIntent(giftTerms, state.AdvancingSteps + 100));
        Assert.Equal(WorldActionRejection.None, authority.SubmitMaterialProposalCommand(gift, Delivery(gift), NextId()).Rejection);
        state = authority.ReadStateForHost();
        Assert.Single(state.Coordination!.Proposals!.Open);
        var before = state;
        var acceptance = AcceptCommand(state, journey.Player, quote, journey.Helper, 1, stalePayment ? 2UL : 1UL);
        var ownWork = journey.Command(state, journey.Helper, new ProposeMaterialIntent(giftTerms, state.AdvancingSteps + 100));
        var decision = authority.Commit(journey.Batch(state, WorldBatchKind.AdvancingStep, [acceptance, ownWork]));
        Assert.Equal(stalePayment ? WorldActionRejection.RevisionConflict : WorldActionRejection.Unavailable,
            decision.Receipts.Single(value => value.CommandId == acceptance.CommandId).Rejection);
        Assert.Equal(WorldActionRejection.None, decision.Receipts.Single(value => value.CommandId == ownWork.CommandId).Rejection);
        state = decision.Candidate;
        Assert.Equal(before.Inventory.ContentHash, state.Inventory.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, state.Progression!.ContentHash);
        Assert.Equal(before.Coordination!.PaidExchange!.ContentHash, state.Coordination!.PaidExchange!.ContentHash);
        Assert.Equal(2, state.Coordination.Proposals!.Open.Count);
    }

    private static SpotQuote CloneQuote(SpotQuote quote, EntityId identity, ulong? marketRevision = null) =>
        new(identity, quote.WorldId, quote.BranchId, quote.IssuerActorId, quote.IssuerPrincipalId, quote.IssuerSessionId,
            quote.CounterpartyActorId, quote.Side, quote.LotKind, quote.LotId, quote.Definition, quote.ExpectedLotRevision,
            quote.SellerCustodianId, quote.BuyerCustodianId, quote.CurrencyDefinitionId, quote.CurrencyVersion,
            quote.SellerAccountId, quote.BuyerAccountId, quote.UnitPrice, quote.Fees, quote.QuantityAllowance,
            marketRevision ?? quote.MarketRevision, quote.IssuedAtStep, quote.ExpiresAtStep);

    private static byte[] ReplaceBook(WorldSnapshot world, WorldPaidExchangeBook book)
    {
        byte[][] root = ReadFields(WorldSnapshotCodec.Encode(world));
        byte[][] coordination = ReadFields(root[22]);
        coordination[6] = WorldPaidExchangeCodec.Encode(book);
        root[22] = WriteFields(coordination);
        return WriteFields(root);
    }

    // An independent frame reader/writer deliberately bypasses the world's decoder while constructing hostile bytes.
    private static byte[][] ReadFields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)));
        var result = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            offset += 4;
            result[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return result;
    }

    private static byte[] WriteFields(params byte[][] fields)
    {
        var bytes = new byte[12 + fields.Sum(value => 4 + value.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), checked((uint)fields.Length));
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), checked((uint)field.Length));
            offset += 4;
            field.CopyTo(bytes, offset);
            offset += field.Length;
        }
        return bytes;
    }
}
