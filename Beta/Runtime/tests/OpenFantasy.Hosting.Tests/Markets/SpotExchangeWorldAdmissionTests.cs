using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;

namespace OpenFantasy.Hosting.Tests.Markets;

public sealed class SpotExchangeWorldAdmissionTests
{
    [Fact]
    public void A_real_funded_component_sale_cannot_enter_current_world_without_its_paid_cause()
    {
        var fixture = new CareWorldFixture();
        var world = fixture.CompleteQuest(new CareWorldSession(fixture.Genesis));
        var beforeHash = world.ContentHash;
        var progression = world.Progression!;
        var buyer = progression.Currency.GetActorAccount(fixture.Player);
        var seller = progression.Currency.GetActorAccount(fixture.Helper);
        Assert.True(buyer.Balance > 0); // Actually earned by the source-backed quest, not test-minted.
        var lot = world.Inventory.Custody.Lots.First(value => value.CustodianId == fixture.HelperStorage
            && value.Quantity > 1 && value.Protection == LotProtection.None && value.ReservedBy is null);
        var issuer = world.Inventory.Controls.Single(value => value.ActorId == fixture.Helper);
        var recipient = world.Inventory.Controls.Single(value => value.ActorId == fixture.Player);
        var issuerDelivery = new TrustedCommandDelivery(issuer.PrincipalId, issuer.SessionId);
        var recipientDelivery = new TrustedCommandDelivery(recipient.PrincipalId, recipient.SessionId);
        var currency = progression.Currency.Currency;
        var quote = new SpotQuote(new(0x5e771e, 1), world.WorldId, world.BranchId, fixture.Helper,
            issuer.PrincipalId, issuer.SessionId, fixture.Player, SpotQuoteSide.IssuerSells, SpotLotKind.CountedSource,
            lot.LotId, lot.Definition, lot.Revision, lot.CustodianId, world.GetResident(fixture.Player).InventoryCustodianId,
            currency.DefinitionId, currency.Version, seller.AccountId, buyer.AccountId, 1, [], 1, 0,
            world.AdvancingSteps, world.AdvancingSteps + 10);
        var book = SpotQuoteBook.Empty(world.WorldId, world.BranchId);
        var authority = new SpotExchangeAuthority();
        var issue = authority.Issue(world.Inventory, world.Production!, progression, book, quote,
            issuerDelivery, world.AdvancingSteps, _ => false);
        book = issue.ApplyTo(world.Inventory, world.Production!, progression, book);
        var acceptance = new SpotAcceptance(new(0x5e771e, 2), recipient.PrincipalId, recipient.SessionId,
            quote.QuoteId, quote.ContentHash, 1, 1, 0);
        var plan = authority.Accept(world.Inventory, world.Production!, progression, book, acceptance,
            recipientDelivery, world.AdvancingSteps, _ => false);
        Assert.Equal(SpotExchangeOutcome.Applied, plan.Outcome);
        Assert.Equal(buyer.Balance - 1, plan.CandidateProgression.GetAccount(buyer.AccountId).Balance);
        Assert.Equal(seller.Balance + 1, plan.CandidateProgression.GetAccount(seller.AccountId).Balance);
        Assert.Equal(progression.Currency.AdmittedSupply, plan.CandidateProgression.Currency.AdmittedSupply);

        Assert.Contains("Resource history", Assert.Throws<ArgumentException>(() =>
            WorldResourceJourneyTests.Copy(world, world.ResourceHistory!, plan.CandidateProgression)).Message);
        // Even disguising the outer revision cannot pass the actual world's exact funded currency replay.
        var disguised = new ProgressionSnapshot(progression.WorldId, progression.BranchId, progression.Revision,
            progression.Profiles, progression.Curves, progression.Actors, plan.CandidateProgression.Currency);
        Assert.Contains("funded currency payments", Assert.Throws<ArgumentException>(() =>
            WorldResourceJourneyTests.Copy(world, world.ResourceHistory!, disguised)).Message);

        var continued = world.Production!.ContinueSpotExchange(world.Inventory);
        var refusal = Assert.Throws<ArgumentException>(() => new WorldSnapshot(world.Inventory, world.Space,
            world.Residents, world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps,
            production: continued));
        Assert.Contains("coordinated material", refusal.Message);
        Assert.Equal(beforeHash, world.ContentHash);
        Assert.Equal(beforeHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world)).ContentHash);
    }
}
