using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

internal static class PlayerQueryResultValidation
{
    internal static void Validate(PlayerQueryResult result)
    {
        Require(Enum.IsDefined(result.Kind), "Unknown query result kind.");
        bool inspection = result.Kind is PlayerQueryKind.InspectPaidQuote or PlayerQueryKind.InspectMaterialProposal;
        Require(inspection == result.SubjectId.HasValue, "A query result must name only an inspection subject.");
        Require(result.Inquiry is null || !inspection, "An inspection cannot contain an inquiry.");
        Require(result.Kind != PlayerQueryKind.SubmitTradeInquiry || result.Inquiry is not null,
            "A submitted inquiry requires its current result.");
        Require(result.PaidQuote is null || result.Kind == PlayerQueryKind.InspectPaidQuote,
            "A quote is present only in its inspection result.");
        Require(result.MaterialProposal is null || result.Kind == PlayerQueryKind.InspectMaterialProposal,
            "A proposal is present only in its inspection result.");
        if (result.Inquiry is { } inquiry)
        {
            Require(inquiry.Inquiry is not null && Enum.IsDefined(inquiry.State) && inquiry.ExpiresAtStep > 0
                && (inquiry.State == LocalTradeInquiryState.Offered) == inquiry.QuoteId.HasValue,
                "An inquiry result must contain consistent state, expiry and issued quote identity.");
        }
        if (result.PaidQuote is { } quote)
        {
            var fees = quote.Fees ?? throw new FormatException("A quote requires its fee collection.");
            Require(fees.Count <= SpotQuote.MaximumFees, "A quote has too many fees.");
            ObservationValidation.ValidateValues(quote);
            Require(quote.QuoteId == result.SubjectId && quote.IssuerActorId != quote.CounterpartyActorId
                && (quote.ViewerIsIssuer ? quote.IssuerActorId : quote.CounterpartyActorId) == result.ActorId,
                "A quote must match its subject and participant.");
            Require(quote.SellerCustodianId != quote.BuyerCustodianId && quote.LotId != quote.SellerCustodianId
                && quote.LotId != quote.BuyerCustodianId && quote.SellerAccountId != quote.BuyerAccountId,
                "A quote's distinct stock and account owners disagree.");
            Require(quote.UnitPrice > 0 && quote.QuantityAllowance is > 0 and <= SpotQuote.MaximumQuantity
                && quote.ExpiresAtStep >= quote.IssuedAtStep, "Invalid quote price, allowance or window.");
            Require(fees.Select(fee => fee.PayeeAccountId).Distinct().Count() == fees.Count
                && fees.All(fee => fee.PayeeAccountId != quote.BuyerAccountId), "Invalid quote fee payees.");
            _ = quote.TotalFor(quote.QuantityAllowance);
            Require(quote.ObservedIssuerRevision is null || !quote.ViewerIsIssuer && quote.Status == PaidQuoteStatus.Open,
                "Only an open quote's counterparty can observe its issuer for acceptance.");
            bool filled = quote.Status == PaidQuoteStatus.Filled;
            Require(filled == quote.FilledQuantity.HasValue && filled == quote.FilledPayment.HasValue
                && filled == quote.FilledFees.HasValue, "A filled quote requires exactly its agreed totals.");
            if (filled)
            {
                ulong quantity = quote.FilledQuantity.GetValueOrDefault();
                Require(quantity > 0 && quantity <= quote.QuantityAllowance
                    && quote.FilledPayment == checked(quote.UnitPrice * quantity)
                    && quote.FilledFees == checked(quote.UnitFees * quantity),
                    "Quote settlement totals contradict its fixed terms.");
            }
            ValidateClosure(quote.Status == PaidQuoteStatus.Open, quote.Status == PaidQuoteStatus.Expired,
                quote.ClosedAtSequence);
        }
        if (result.MaterialProposal is { } proposal)
        {
            ObservationValidation.ValidateValues(proposal);
            Require(proposal.ProposalId == result.SubjectId && proposal.Terms.WorldId == result.WorldId
                && proposal.Terms.BranchId == result.BranchId
                && (proposal.ViewerIsSender ? proposal.Terms.SenderActorId : proposal.Terms.RecipientActorId) == result.ActorId,
                "A material proposal must match its subject, world and participant.");
            Require(proposal.ExpiresAtAdvancingStep > 0, "A proposal requires a fixed expiry.");
            ValidateClosure(proposal.Status == MaterialProposalStatus.Open, proposal.Status == MaterialProposalStatus.Expired,
                proposal.ClosedAtSequence);
        }
        ObservationValidation.ValidateValues(result);
    }

    private static void ValidateClosure(bool open, bool expired, ulong? sequence)
    {
        // A due open book entry already projects Expired before its durable outcome exists.
        Require(sequence is null || sequence > 0, "A closed outcome requires a published sequence.");
        Require(open ? sequence is null : expired || sequence.HasValue, "The result status and closure disagree.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new FormatException(message);
    }
}
