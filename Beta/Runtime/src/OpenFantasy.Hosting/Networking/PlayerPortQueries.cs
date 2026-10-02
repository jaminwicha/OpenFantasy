using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Queries only through the authenticated port; result values confer no new permission or commitment.</summary>
internal static class PlayerPortQueries
{
    internal static async Task<PlayerQueryResult> ExecuteAsync(LocalPlayerConnection player, PlayerQuery query)
    {
        LocalWorldView scope = player.ReadView();
        LocalTradeInquiryView? inquiry = null;
        PaidQuoteView? paidQuote = null;
        MaterialProposalView? materialProposal = null;
        switch (query.Kind)
        {
            case PlayerQueryKind.SubmitTradeInquiry:
                inquiry = await player.SubmitTradeInquiryAsync(query.Inquiry!).ConfigureAwait(false);
                break;
            case PlayerQueryKind.CancelTradeInquiry:
                inquiry = await player.CancelTradeInquiryAsync().ConfigureAwait(false);
                break;
            case PlayerQueryKind.ReadTradeInquiry:
                inquiry = player.ReadTradeInquiry();
                break;
            case PlayerQueryKind.InspectPaidQuote:
                paidQuote = await player.InspectPaidQuoteAsync(query.SubjectId!.Value).ConfigureAwait(false);
                break;
            case PlayerQueryKind.InspectMaterialProposal:
                materialProposal = await player.InspectMaterialProposalAsync(query.SubjectId!.Value).ConfigureAwait(false);
                break;
            default:
                throw new FormatException("Unsupported player query.");
        }
        return new PlayerQueryResult(scope.WorldId, scope.BranchId, player.ActorId, query.Kind, query.SubjectId,
            inquiry, paidQuote, materialProposal);
    }
}
