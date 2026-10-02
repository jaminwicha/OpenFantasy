using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// One scoped participant result. A missing inspection subject and a foreign subject have the same null payload;
/// this value contains no authority object or transport delivery. The authenticated host supplies its scope.
/// </summary>
public sealed record PlayerQueryResult
{
    public PlayerQueryResult(EntityId worldId, EntityId branchId, EntityId actorId, PlayerQueryKind kind,
        EntityId? subjectId = null, LocalTradeInquiryView? inquiry = null, PaidQuoteView? paidQuote = null,
        MaterialProposalView? materialProposal = null, PlayerPresentationBootstrap? presentation = null,
        PlayerParticipantStatus? participantStatus = null)
    {
        WorldId = worldId;
        BranchId = branchId;
        ActorId = actorId;
        Kind = kind;
        SubjectId = subjectId;
        Inquiry = inquiry;
        PaidQuote = paidQuote;
        MaterialProposal = materialProposal;
        Presentation = presentation;
        ParticipantStatus = participantStatus;
        PlayerQueryResultValidation.Validate(this);
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId ActorId { get; }
    public PlayerQueryKind Kind { get; }
    public EntityId? SubjectId { get; }
    public LocalTradeInquiryView? Inquiry { get; }
    public PaidQuoteView? PaidQuote { get; }
    public MaterialProposalView? MaterialProposal { get; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PlayerPresentationBootstrap? Presentation { get; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PlayerParticipantStatus? ParticipantStatus { get; }

    /// <summary>Checks response correlation and connection scope before a caller publishes a result.</summary>
    public void ValidateFor(PlayerQuery query, EntityId worldId, EntityId branchId, EntityId actorId)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (WorldId != worldId || BranchId != branchId || ActorId != actorId || Kind != query.Kind
            || SubjectId != query.SubjectId
            || (Kind == PlayerQueryKind.SubmitTradeInquiry && Inquiry?.Inquiry != query.Inquiry))
        {
            throw new FormatException("A query result does not match its request and authenticated scope.");
        }
    }
}
