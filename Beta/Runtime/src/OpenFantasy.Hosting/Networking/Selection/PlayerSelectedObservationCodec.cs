using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Selection;

/// <summary>A full selected observation on one authenticated connection's ordered presentation stream.</summary>
public sealed record PlayerSelectedObservation(EntityId StreamId, ulong Revision, LocalSelectedPlayerObservation Observation)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int PresentationVersion { get; init; } = PlayerSelectedObservationCodec.Version;

    public PlayerSelectedObservation WithConstruction() => this with
    {
        PresentationVersion = PlayerSelectedObservationCodec.ConstructionVersion
    };

    public PlayerSelectedObservation WithMaterialDescriptions() => this with
    {
        PresentationVersion = PlayerSelectedObservationCodec.MaterialDescriptionsVersion
    };

    public PlayerSelectedObservation WithCollectiveWithdrawal() => Observation.HomeView.Retreat?.Withdrawal is not null
        ? this with { PresentationVersion = PlayerSelectedObservationCodec.CollectiveWithdrawalVersion }
        : this;

    public PlayerSelectedObservation WithCarePermissionDescriptions() =>
        CarePermissionDescriptionValidation.HasDescriptions(Observation.HomeView)
            || CarePermissionDescriptionValidation.HasDescriptions(Observation.ControlledView)
        ? this with { PresentationVersion = PlayerSelectedObservationCodec.CarePermissionDescriptionsVersion }
        : this;
}

/// <summary>The full home and scoped actor share one aggregate byte/expansion budget and one capture identity.</summary>
public static class PlayerSelectedObservationCodec
{
    public const int Version = 1;
    public const int MaterialDescriptionsVersion = 2;
    public const int ConstructionVersion = 3;
    public const int CarePermissionDescriptionsVersion = 4;
    public const int CollectiveWithdrawalVersion = 5;
    public const int MaximumBodyBytes = PlayerViewCodec.MaximumBodyBytes;
    public const int MaximumExpandedJsonBytes = PlayerViewCodec.MaximumExpandedJsonBytes;
    private static readonly JsonSerializerOptions Options = SelectedObservationOptions.Create();
    private static readonly JsonSerializerOptions DescribedOptions = SelectedObservationOptions.Create(includeMaterialDescriptions: true);
    private static readonly JsonSerializerOptions ConstructionOptions = SelectedObservationOptions.Create(includeMaterialDescriptions: true, includeConstruction: true);
    private static readonly JsonSerializerOptions CarePermissionOptions = SelectedObservationOptions.Create(
        includeMaterialDescriptions: true, includeConstruction: true, includeCarePermissionDescriptions: true);
    public static byte[] Encode(PlayerSelectedObservation observation)
    {
        using var json = EncodeJson(observation);
        return json.WrittenSpan.Length <= MaximumBodyBytes ? json.WrittenSpan.ToArray() : ObservationCompression.Encode(json.WrittenSpan);
    }
    public static byte[] EncodeWithConstruction(PlayerSelectedObservation observation) => Encode(observation.WithConstruction());

    public static byte[] EncodeWithMaterialDescriptions(PlayerSelectedObservation observation) =>
        Encode(observation.WithMaterialDescriptions());

    public static PlayerSelectedObservation Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.IsEmpty || bytes.Length > MaximumBodyBytes) throw new FormatException("Invalid selected observation size.");
            byte[]? expanded = ObservationCompression.IsCompressed(bytes) ? ObservationCompression.Decode(bytes) : null;
            ReadOnlySpan<byte> json = expanded is null ? bytes : expanded;
            PlayerViewCodec.Preflight(json);
            var body = JsonSerializer.Deserialize<Body>(json, Options) ?? throw new FormatException("Missing selected observation.");
            if (body.Version is not Version and not MaterialDescriptionsVersion and not ConstructionVersion
                and not CarePermissionDescriptionsVersion and not CollectiveWithdrawalVersion || body.Value is null) throw new FormatException("Unsupported selected observation version.");
            var value = body.Value with { PresentationVersion = body.Version };
            using var canonical = EncodeJson(value);
            if (!json.SequenceEqual(canonical.WrittenSpan)) throw new FormatException("Noncanonical selected observation.");
            return value;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException
            or OverflowException or NullReferenceException or NotSupportedException or System.Reflection.TargetInvocationException)
        { throw new FormatException("Invalid selected observation values.", error); }
    }
    private static BoundedObservationJsonBuffer EncodeJson(PlayerSelectedObservation value)
    {
        SelectedObservationValidation.Validate(value);
        if (value.PresentationVersion is not Version and not MaterialDescriptionsVersion and not ConstructionVersion
            and not CarePermissionDescriptionsVersion and not CollectiveWithdrawalVersion)
            throw new FormatException("Unsupported selected presentation version.");
        bool careDescriptions = value.PresentationVersion >= CarePermissionDescriptionsVersion;
        bool construction = value.PresentationVersion >= ConstructionVersion;
        bool collective = value.PresentationVersion == CollectiveWithdrawalVersion;
        WithdrawalObservationValidation.Validate(value.Observation.HomeView, collective);
        if (value.Observation.ControlledView is { } controlledWithdrawal)
            WithdrawalObservationValidation.Validate(controlledWithdrawal, collective);
        bool described = value.PresentationVersion >= MaterialDescriptionsVersion;
        if (construction)
        {
            ConstructionObservationValidation.Validate(value.Observation.HomeView);
            if (value.Observation.ControlledView is { } operated) ConstructionObservationValidation.Validate(operated);
        }
        if (described)
        {
            MaterialDescriptionValidation.Validate(value.Observation.HomeView);
            if (value.Observation.ControlledView is { } controlled) MaterialDescriptionValidation.Validate(controlled);
        }
        CarePermissionDescriptionValidation.Validate(value.Observation.HomeView, required: careDescriptions);
        if (value.Observation.ControlledView is { } controlledCare)
            CarePermissionDescriptionValidation.Validate(controlledCare, required: careDescriptions);
        var buffer = new BoundedObservationJsonBuffer();
        try
        {
            using var writer = new Utf8JsonWriter(buffer);
            JsonSerializer.Serialize(writer, new Body(value.PresentationVersion, value), careDescriptions ? CarePermissionOptions
                : construction ? ConstructionOptions : described ? DescribedOptions : Options); writer.Flush();
            PlayerViewCodec.Preflight(buffer.WrittenSpan);
            return buffer;
        }
        catch { buffer.Dispose(); throw; }
    }
    private sealed record Body(int Version, PlayerSelectedObservation Value);
}
