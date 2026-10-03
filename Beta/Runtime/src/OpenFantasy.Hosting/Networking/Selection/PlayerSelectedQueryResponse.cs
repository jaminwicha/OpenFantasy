using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Selection;

public sealed record PlayerSelectedQueryResponse(EntityId WorldId, EntityId BranchId, LocalActorSelection Selection,
    LocalSelectedQueryOperation Operation, EntityId? SubjectId, LocalTradeInquiryView? Inquiry = null,
    PaidQuoteView? PaidQuote = null, MaterialProposalView? MaterialProposal = null)
{
    public static PlayerSelectedQueryResponse From<T>(LocalSelectedQueryResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Value is not null and not LocalTradeInquiryView and not PaidQuoteView and not MaterialProposalView)
            throw new FormatException("Unsupported selected query response value.");
        return new(result.WorldId, result.BranchId, result.Selection, result.Operation, result.SubjectId,
            result.Value as LocalTradeInquiryView, result.Value as PaidQuoteView, result.Value as MaterialProposalView);
    }
    public void ValidateFor(PlayerSelectedInput request, EntityId worldId, EntityId branchId, LocalActorSelection current)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate();
        if (request.Kind != PlayerSelectedInputKind.Query || Selection != request.Selection || Selection != current
            || WorldId != worldId || BranchId != branchId || Operation != request.Operation || SubjectId != request.SubjectId
            || Operation == LocalSelectedQueryOperation.SubmitTradeInquiry && Inquiry?.Inquiry != request.Inquiry)
            throw new FormatException("Selected query response does not match its request and current selection.");
    }
    internal void Validate()
    {
        PlayerSelectionCodec.Validate(Selection);
        var request = PlayerSelectedInputCodec.LegacyQuery(Operation,
            Operation == LocalSelectedQueryOperation.SubmitTradeInquiry ? Inquiry?.Inquiry : null, SubjectId);
        _ = new PlayerQueryResult(WorldId, BranchId, Selection.ActorId, request.Kind, SubjectId, Inquiry, PaidQuote, MaterialProposal);
        bool history = Operation is LocalSelectedQueryOperation.InspectPaidQuoteHistory or LocalSelectedQueryOperation.InspectMaterialProposalHistory;
        var required = history ? WorldControlCapabilities.PrivateHistory
            : Operation == LocalSelectedQueryOperation.InspectOpenMaterialProposal ? WorldControlCapabilities.Inventory : WorldControlCapabilities.Trade;
        if (Selection.ControlWitness is { } witness && (witness.Capabilities & required) != required)
            throw new FormatException("Selected query response exceeds the selected capability.");
        if (PaidQuote is { } quote && (history ? quote.ClosedAtSequence is null || quote.Status == PaidQuoteStatus.Open
                : quote.Status != PaidQuoteStatus.Open || quote.ClosedAtSequence is not null)
            || MaterialProposal is { } proposal && (history
                ? proposal.ClosedAtSequence is null || proposal.Status == MaterialProposalStatus.Open || proposal.SenderPresenceRevision is not null
                : proposal.Status != MaterialProposalStatus.Open || proposal.ClosedAtSequence is not null))
            throw new FormatException("Selected inspection mixes open terms and terminal history.");
    }
}

public static class PlayerSelectedQueryResponseCodec
{
    public const int MaximumBodyBytes = PlayerQueryResultCodec.MaximumBodyBytes;
    private static readonly JsonSerializerOptions Options = SelectedObservationOptions.Create();
    public static byte[] Encode(PlayerSelectedQueryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response); response.Validate();
        var buffer = new PlayerQueryJsonBuffer();
        using var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, new Body(1, response), Options); writer.Flush();
        PlayerViewCodec.Preflight(buffer.WrittenSpan); return buffer.WrittenSpan.ToArray();
    }
    public static PlayerSelectedQueryResponse Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.IsEmpty || bytes.Length > MaximumBodyBytes) throw new FormatException("Invalid selected query response size.");
            PlayerViewCodec.Preflight(bytes);
            var body = JsonSerializer.Deserialize<Body>(bytes, Options) ?? throw new FormatException("Missing selected query response.");
            if (body.Version != 1 || body.Result is null || !bytes.SequenceEqual(Encode(body.Result)))
                throw new FormatException("Unsupported or noncanonical selected query response.");
            return body.Result;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException
            or OverflowException or NullReferenceException or NotSupportedException or System.Reflection.TargetInvocationException)
        { throw new FormatException("Invalid selected query response values.", error); }
    }
    private sealed record Body(int Version, PlayerSelectedQueryResponse Result);
}
