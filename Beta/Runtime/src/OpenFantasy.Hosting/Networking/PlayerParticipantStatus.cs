using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>A coherent participant status read, not the shared world's global run state.</summary>
public sealed record PlayerParticipantStatus
{
    public PlayerParticipantStatus(LocalWorldRunState runState, LocalTradeInquiryView? inquiry = null)
    {
        if (!Enum.IsDefined(runState)) throw new ArgumentOutOfRangeException(nameof(runState));
        PlayerQueryResultValidation.ValidateInquiry(inquiry);
        RunState = runState;
        Inquiry = inquiry;
    }

    public LocalWorldRunState RunState { get; }
    public LocalTradeInquiryView? Inquiry { get; }
}
