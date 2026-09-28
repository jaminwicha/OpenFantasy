namespace OpenFantasy.Core.Geography;

/// <summary>Explicit continued forcing and routing bytes; nested production requires its exact external context and evidence.</summary>
public static class WatershedRunoffCodecV3
{
    public const int MaximumEncodedByteLength = WatershedRunoffCodecV1.MaximumEncodedByteLength;
    public const int MaximumRequestByteLength = WatershedRunoffCodecV2.MaximumRequestByteLength;

    public static byte[] Encode(PreparedWatershedRunoff runoff)
    {
        ArgumentNullException.ThrowIfNull(runoff);
        return WatershedRunoffCodecV1.EncodeRunoff(runoff, runoff.Request.Production?.SupportsConsumption == true, continued: true);
    }

    public static PreparedWatershedRunoff Decode(ReadOnlySpan<byte> bytes, GeographyProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return WatershedRunoffCodecV1.DecodeRunoff(bytes, cancellationToken, dependencies.Context.SupportsConsumption, dependencies);
    }

    public static byte[] EncodeRequest(WatershedRunoffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WatershedRunoffCodecV1.EncodeRequest(request, request.Production?.SupportsConsumption == true, continued: true);
    }

    public static WatershedRunoffRequest DecodeRequest(ReadOnlySpan<byte> bytes, GeographyProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var request = WatershedRunoffCodecV1.DecodeRequest(bytes, cancellationToken, dependencies.Context.SupportsConsumption, dependencies);
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
