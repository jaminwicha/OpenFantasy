using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// Canonical UTF-8 JSON containing Version and Result. Version 1 retains the five original query shapes;
/// version 2 contains only presentation bootstrap and participant status. Complete explicit fields, including
/// nulls, use the observation converters; compression, world roots and alternate JSON spellings are refused.
/// </summary>
public static class PlayerQueryResultCodec
{
    public const int Version = 1;
    public const int PresentationVersion = 2;
    public const int MaximumBodyBytes = 16 * 1024;
    private static readonly JsonSerializerOptions Options = PlayerViewCodec.CreateOptions();

    public static byte[] Encode(PlayerQueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        PlayerQueryResultValidation.Validate(result);
        var buffer = new PlayerQueryJsonBuffer();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            if (PlayerQueryCodec.IsPresentation(result.Kind))
                JsonSerializer.Serialize(writer, new PresentationBody(PresentationVersion,
                    new PresentationResult(result.WorldId, result.BranchId, result.ActorId, result.Kind,
                        result.Presentation, result.ParticipantStatus)), Options);
            else
                JsonSerializer.Serialize(writer, new ResultBody(Version, LegacyResult.From(result)), Options);
            writer.Flush();
        }
        PlayerViewCodec.Preflight(buffer.WrittenSpan);
        return buffer.WrittenSpan.ToArray();
    }

    public static PlayerQueryResult Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.IsEmpty || bytes.Length > MaximumBodyBytes)
                throw new FormatException("Query result exceeds its nonempty byte bound.");
            PlayerViewCodec.Preflight(bytes);
            using JsonDocument document = JsonDocument.Parse(bytes.ToArray());
            if (!document.RootElement.TryGetProperty("Version", out JsonElement versionValue)
                || !versionValue.TryGetInt32(out int version)) throw new FormatException("Missing query result version.");
            PlayerQueryResult result;
            if (version == Version)
            {
                ResultBody body = JsonSerializer.Deserialize<ResultBody>(bytes, Options)
                    ?? throw new FormatException("Missing query result.");
                if (body.Result is null || PlayerQueryCodec.IsPresentation(body.Result.Kind))
                    throw new FormatException("Version 1 cannot contain a presentation query.");
                result = body.Result.ToResult();
            }
            else if (version == PresentationVersion)
            {
                PresentationBody body = JsonSerializer.Deserialize<PresentationBody>(bytes, Options)
                    ?? throw new FormatException("Missing presentation result.");
                if (body.Result is null || !PlayerQueryCodec.IsPresentation(body.Result.Kind))
                    throw new FormatException("Version 2 requires a presentation query.");
                result = body.Result.ToResult();
            }
            else throw new FormatException("Unsupported query result version.");
            if (!bytes.SequenceEqual(Encode(result)))
                throw new FormatException("Query results require their complete canonical value encoding.");
            return result;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException
            or OverflowException or NullReferenceException or NotSupportedException or System.Reflection.TargetInvocationException)
        {
            throw new FormatException("Invalid query result values.", exception);
        }
    }

    // This explicit DTO preserves every version 1 field and its original property order.
    private sealed record LegacyResult(EntityId WorldId, EntityId BranchId, EntityId ActorId, PlayerQueryKind Kind,
        EntityId? SubjectId, LocalTradeInquiryView? Inquiry, PaidQuoteView? PaidQuote, MaterialProposalView? MaterialProposal)
    {
        internal static LegacyResult From(PlayerQueryResult result) => new(result.WorldId, result.BranchId, result.ActorId,
            result.Kind, result.SubjectId, result.Inquiry, result.PaidQuote, result.MaterialProposal);
        internal PlayerQueryResult ToResult() => new(WorldId, BranchId, ActorId, Kind, SubjectId, Inquiry, PaidQuote, MaterialProposal);
    }

    private sealed record PresentationResult(EntityId WorldId, EntityId BranchId, EntityId ActorId, PlayerQueryKind Kind,
        PlayerPresentationBootstrap? Presentation, PlayerParticipantStatus? ParticipantStatus)
    {
        internal PlayerQueryResult ToResult() => new(WorldId, BranchId, ActorId, Kind,
            presentation: Presentation, participantStatus: ParticipantStatus);
    }

    private sealed record ResultBody(int Version, LegacyResult Result);
    private sealed record PresentationBody(int Version, PresentationResult Result);
}
