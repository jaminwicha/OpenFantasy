using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

public enum PlayerQueryKind : byte
{
    SubmitTradeInquiry = 1,
    CancelTradeInquiry = 2,
    ReadTradeInquiry = 3,
    InspectPaidQuote = 4,
    InspectMaterialProposal = 5,
    ReadPresentationBootstrap = 16,
    ReadParticipantStatus = 17
}

/// <summary>One non-authoritative request. Controller identity and delivery come only from its authenticated port.</summary>
public sealed record PlayerQuery
{
    public PlayerQuery(PlayerQueryKind kind, LocalTradeInquiry? inquiry = null, EntityId? subjectId = null)
    {
        if (!Enum.IsDefined(kind)
            || (kind == PlayerQueryKind.SubmitTradeInquiry) != (inquiry is not null)
            || (kind is PlayerQueryKind.InspectPaidQuote or PlayerQueryKind.InspectMaterialProposal) != subjectId.HasValue)
        {
            throw new ArgumentException("A query carries exactly the fields required by its kind.");
        }
        Kind = kind;
        Inquiry = inquiry;
        SubjectId = subjectId;
    }

    public PlayerQueryKind Kind { get; }
    public LocalTradeInquiry? Inquiry { get; }
    public EntityId? SubjectId { get; }
}
