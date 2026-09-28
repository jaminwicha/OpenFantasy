using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Selects an explicit production interpretation without silently upgrading retained geography.</summary>
internal static class GeographyProductionCodec
{
    internal static byte[] Encode(ProductionSnapshot? production, bool consumption)
    {
        if (production?.IsContinued == true)
        {
            throw new ArgumentException("Geography archives require complete inline production history.", nameof(production));
        }
        if ((production?.SupportsConsumption == true) != consumption)
        {
            throw new ArgumentException("The geography archive must retain its exact consumption ownership profile.");
        }
        return production is null ? [] : consumption ? ProductionCodecV2.Encode(production) : ProductionCodecV1.Encode(production);
    }

    internal static ProductionSnapshot? Decode(ReadOnlySpan<byte> bytes, bool consumption)
    {
        if (bytes.IsEmpty)
        {
            if (consumption)
            {
                throw new FormatException("Consumption-capable geography requires its complete production owner.");
            }
            return null;
        }
        return consumption ? ProductionCodecV2.Decode(bytes) : ProductionCodecV1.Decode(bytes);
    }
}
