using System.Text.Json;
using System.Text.Json.Serialization;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

/// <summary>
/// A versioned, bounded presentation-value encoding. It is separate from diagnostic JSON, canonical world state,
/// receipts and authority admission. Decoding creates an immutable view and never creates a world root.
/// </summary>
public static class PlayerViewCodec
{
    public const int Version = 1;
    public const int ControllerAcceptanceVersion = 2;
    public const int AssistedRecoveryVersion = 3;
    public const int CareDispositionVersion = 4;
    public const int MaterialDescriptionsVersion = 5;
    public const int ConstructionVersion = 6;
    public const int MaximumBodyBytes = 900 * 1024;
    public const int MaximumExpandedJsonBytes = 16 * 1024 * 1024;
    public const int MaximumDepth = 48;
    public const int MaximumCollectionItems = 16_384;
    public const int MaximumStringBytes = 16_384;
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static readonly JsonSerializerOptions DescribedOptions = CreateOptions(includeMaterialDescriptions: true);
    private static readonly JsonSerializerOptions ConstructionOptions = CreateOptions(includeMaterialDescriptions: true, includeConstruction: true);

    public static byte[] Encode(LocalWorldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        using BoundedObservationJsonBuffer json = EncodeJson(view);
        return json.WrittenSpan.Length <= MaximumBodyBytes ? json.WrittenSpan.ToArray()
            : ObservationCompression.Encode(json.WrittenSpan);
    }

    /// <summary>Explicitly includes public descriptors; Encode remains the exact legacy representation.</summary>
    public static byte[] EncodeWithMaterialDescriptions(LocalWorldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        using BoundedObservationJsonBuffer json = EncodeJson(view, includeMaterialDescriptions: true);
        return json.WrittenSpan.Length <= MaximumBodyBytes ? json.WrittenSpan.ToArray()
            : ObservationCompression.Encode(json.WrittenSpan);
    }

    /// <summary>Includes current builder preparation and independent public installed geometry.</summary>
    public static byte[] EncodeWithConstruction(LocalWorldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        using BoundedObservationJsonBuffer json = EncodeJson(view, includeMaterialDescriptions: true, includeConstruction: true);
        return json.WrittenSpan.Length <= MaximumBodyBytes ? json.WrittenSpan.ToArray()
            : ObservationCompression.Encode(json.WrittenSpan);
    }

    public static LocalWorldView Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.IsEmpty || bytes.Length > MaximumBodyBytes)
            {
                throw new FormatException("Observation body exceeds its nonempty wire byte bound.");
            }
            byte[]? expanded = ObservationCompression.IsCompressed(bytes) ? ObservationCompression.Decode(bytes) : null;
            ReadOnlySpan<byte> json = expanded is null ? bytes : expanded;
            Preflight(json);
            ObservationBody body = JsonSerializer.Deserialize<ObservationBody>(json, Options)
                ?? throw new FormatException("Missing observation body.");
            if (body.View is null || body.Version != ConstructionVersion && body.Version != MaterialDescriptionsVersion && body.Version != VersionFor(body.View))
            {
                throw new FormatException("Unsupported or incomplete observation body.");
            }
            // Canonical JSON rejects duplicate, missing, unknown and contradictory derived fields. Brotli's
            // compressed spelling may vary across implementations; only the expanded typed bytes must agree.
            using BoundedObservationJsonBuffer canonical = EncodeJson(body.View, body.Version >= MaterialDescriptionsVersion, body.Version == ConstructionVersion);
            if (!json.SequenceEqual(canonical.WrittenSpan))
            {
                throw new FormatException("An observation must use its complete canonical value encoding.");
            }
            return body.View;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException
            or OverflowException or NullReferenceException or NotSupportedException or System.Reflection.TargetInvocationException)
        {
            throw new FormatException("Invalid observation values.", exception);
        }
    }

    private static BoundedObservationJsonBuffer EncodeJson(LocalWorldView view, bool includeMaterialDescriptions = false, bool includeConstruction = false)
    {
        ObservationValidation.Validate(view);
        if (includeConstruction) ConstructionObservationValidation.Validate(view);
        if (includeMaterialDescriptions) MaterialDescriptionValidation.Validate(view);
        var buffer = new BoundedObservationJsonBuffer();
        try
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                JsonSerializer.Serialize(writer, new ObservationBody(includeConstruction ? ConstructionVersion : includeMaterialDescriptions ? MaterialDescriptionsVersion : VersionFor(view), view),
                    includeConstruction ? ConstructionOptions : includeMaterialDescriptions ? DescribedOptions : Options);
                writer.Flush();
            }
            Preflight(buffer.WrittenSpan);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static int VersionFor(LocalWorldView view) =>
        view.SupportsCareDispositions ? CareDispositionVersion
            : view.EncounterOutcome?.AllowsAssistedRecovery == true ? AssistedRecoveryVersion
            : view.EncounterOutcome?.AcknowledgmentMode == LocalOutcomeAcknowledgmentMode.ControllerAcceptance
            ? ControllerAcceptanceVersion : Version;

    internal static JsonSerializerOptions CreateOptions(bool includeMaterialDescriptions = false, bool includeConstruction = false)
    {
        var options = new JsonSerializerOptions
        {
            MaxDepth = MaximumDepth,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            NumberHandling = JsonNumberHandling.Strict
        };
        ObservationViewConverters.AddTo(options, includeMaterialDescriptions, includeConstruction);
        options.Converters.Add(new ObservationValueConverter<LocalMaterialProposalPreparation, MaterialProposalPreparationData>(
            MaterialProposalPreparationData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<RestorativeAvailability, RestorativeAvailabilityData>(
            RestorativeAvailabilityData.From, value => value.ToView()));
        options.Converters.Add(new ObservationCommandHashConverter());
        options.Converters.Add(new ObservationOccupancyConverter());
        options.Converters.Add(new ObservationCollectionConverter());
        return options;
    }

    internal static void Preflight(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > MaximumExpandedJsonBytes)
        {
            throw new FormatException("Observation JSON exceeds its nonempty expanded byte bound.");
        }
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = MaximumDepth });
        Span<int> counts = stackalloc int[MaximumDepth + 1];
        Span<bool> arrays = stackalloc bool[MaximumDepth + 1];
        counts.Clear();
        arrays.Clear();
        var names = new HashSet<string>?[MaximumDepth + 1];
        int depth = 0;
        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName
                && reader.ValueSpan.Length > MaximumStringBytes)
            {
                throw new FormatException("Observation text exceeds its bound.");
            }
            if (reader.TokenType is JsonTokenType.EndArray or JsonTokenType.EndObject)
            {
                names[depth--] = null;
                continue;
            }
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (names[depth] is not { } properties || !properties.Add(reader.GetString()!))
                {
                    throw new FormatException("Duplicate observation property.");
                }
            }
            if ((arrays[depth] || reader.TokenType == JsonTokenType.PropertyName)
                && ++counts[depth] > MaximumCollectionItems)
            {
                throw new FormatException("Observation container exceeds its bound.");
            }
            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
            {
                counts[++depth] = 0;
                arrays[depth] = reader.TokenType == JsonTokenType.StartArray;
                names[depth] = arrays[depth] ? null : new HashSet<string>(StringComparer.Ordinal);
            }
        }
    }

    private sealed record ObservationBody(int Version, LocalWorldView View);
}
