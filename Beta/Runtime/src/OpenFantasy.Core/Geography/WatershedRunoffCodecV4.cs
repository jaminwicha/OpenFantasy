namespace OpenFantasy.Core.Geography;

/// <summary>Explicit V7 forcing and routing bytes requiring actual transport qualification and opaque material evidence.</summary>
public static class WatershedRunoffCodecV4
{
    public const int MaximumEncodedByteLength = WatershedRunoffCodecV1.MaximumEncodedByteLength;
    public const int MaximumRequestByteLength = WatershedRunoffCodecV2.MaximumRequestByteLength;

    public static byte[] Encode(PreparedWatershedRunoff runoff)
    {
        ArgumentNullException.ThrowIfNull(runoff);
        return WatershedRunoffCodecV1.EncodeRunoff(runoff, runoff.Request.Production?.SupportsConsumption == true, continued: true, transport: true);
    }

    public static PreparedWatershedRunoff Decode(ReadOnlySpan<byte> bytes, GeographyProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return WatershedRunoffCodecV1.DecodeRunoff(bytes, cancellationToken, dependencies.Context.SupportsConsumption, dependencies, transport: true);
    }

    public static byte[] EncodeRequest(WatershedRunoffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WatershedRunoffCodecV1.EncodeRequest(request, request.Production?.SupportsConsumption == true, continued: true, transport: true);
    }

    public static WatershedRunoffRequest DecodeRequest(ReadOnlySpan<byte> bytes, GeographyProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var request = WatershedRunoffCodecV1.DecodeRequest(bytes, cancellationToken, dependencies.Context.SupportsConsumption, dependencies, transport: true);
            // The external context must also bind the decoded live inventory, not merely the production bytes.
            _ = new WatershedRunoffPlanner().Prepare(request, cancellationToken);
            if (!EncodeRequest(request).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The continued runoff request is noncanonical.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return request;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The continued runoff request violates its exact material or funding contract.", error);
        }
    }
}
