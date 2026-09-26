namespace OpenFantasy.Core.Geography;

/// <summary>Consumption-capable forcing inputs and rederived routing witnesses.</summary>
public static class WatershedRunoffCodecV2
{
    public const int MaximumEncodedByteLength = WatershedRunoffCodecV1.MaximumEncodedByteLength;
    public const int MaximumRequestByteLength = 22_900_000;

    public static byte[] Encode(PreparedWatershedRunoff runoff) =>
        WatershedRunoffCodecV1.EncodeRunoff(runoff, consumption: true);

    public static PreparedWatershedRunoff Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        WatershedRunoffCodecV1.DecodeRunoff(bytes, cancellationToken, consumption: true);

    public static byte[] EncodeRequest(WatershedRunoffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WatershedRunoffCodecV1.EncodeRequest(request, consumption: true);
    }

    public static WatershedRunoffRequest DecodeRequest(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var request = WatershedRunoffCodecV1.DecodeRequest(bytes, cancellationToken, consumption: true);
            if (!EncodeRequest(request).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The runoff request is noncanonical.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return request;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The runoff request violates its finite funding or receiver contracts.", error);
        }
    }
}
