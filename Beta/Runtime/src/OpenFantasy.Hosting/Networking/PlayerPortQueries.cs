using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Core.Authority;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Queries only through the authenticated port; result values confer no new permission or commitment.</summary>
internal static class PlayerPortQueries
{
    internal static async Task<PlayerQueryResult> ExecuteAsync(LocalPlayerConnection player, PlayerQuery query)
        => await ExecuteAsync(player, query, null).ConfigureAwait(false);

    internal static Task<PlayerQueryResult> ExecuteAsync(AuthenticatedPlayerChannel channel, PlayerQuery query) =>
        ExecuteAsync(channel.Player, query, channel.Presentation);

    private static async Task<PlayerQueryResult> ExecuteAsync(LocalPlayerConnection player, PlayerQuery query,
        PlayerPresentationBootstrap? presentation)
    {
        LocalWorldView scope = player.ReadView();
        LocalTradeInquiryView? inquiry = null;
        PaidQuoteView? paidQuote = null;
        MaterialProposalView? materialProposal = null;
        PlayerParticipantStatus? participantStatus = null;
        PlayerPresentationBootstrap? bootstrap = null;
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
            case PlayerQueryKind.ReadPresentationBootstrap:
                if (player.ReadRunState() == LocalWorldRunState.Unavailable || presentation is null)
                    throw new AuthorityDispatchException("The admitted player has no presentation bootstrap.");
                bootstrap = presentation;
                break;
            case PlayerQueryKind.ReadParticipantStatus:
                participantStatus = await player.ReadParticipantStatusAsync().ConfigureAwait(false);
                break;
            default:
                throw new FormatException("Unsupported player query.");
        }
        return new PlayerQueryResult(scope.WorldId, scope.BranchId, player.ActorId, query.Kind, query.SubjectId,
            inquiry, paidQuote, materialProposal, bootstrap, participantStatus);
    }
}
