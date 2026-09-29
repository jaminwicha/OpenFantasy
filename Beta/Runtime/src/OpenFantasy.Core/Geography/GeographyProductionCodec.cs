using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Selects an explicit production interpretation without silently upgrading retained geography.</summary>
internal static class GeographyProductionCodec
{
    internal static byte[] Encode(ProductionSnapshot? production, bool consumption, bool continued = false, bool transport = false)
    {
        if (transport)
        {
            if (!continued || production?.Continuation?.SupportsReservedTransport != true || production.SupportsConsumption != consumption)
                throw new ArgumentException("Transport geography requires its explicit V7 material owner.");
            return ProductionCodecV7.Encode(production);
        }
        if (continued)
        {
            if (production?.Continuation is not { } context || production.SupportsConsumption != consumption
                || context.SupportsReservedTransport || context.SupportsTerrainRepresentation || context.SupportsExcavation)
            {
                throw new ArgumentException("Continued geography requires its exact explicit production capability.", nameof(production));
            }
            return context.SupportsConsentedWorld ? ProductionCodecV11.Encode(production)
                : context.SupportsCompleteWorld ? ProductionCodecV9.Encode(production)
                : context.SupportsConstruction ? ProductionCodecV5.Encode(production)
                : context.SupportsMaterialOperations ? ProductionCodecV4.Encode(production)
                : ProductionCodecV3.Encode(production);
        }
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

    internal static ProductionSnapshot? Decode(ReadOnlySpan<byte> bytes, bool consumption,
        GeographyProductionDependencies? dependencies = null, bool transport = false)
    {
        if (dependencies is not null)
        {
            if (dependencies.Context.SupportsConsumption != consumption
                || dependencies.Context.SupportsReservedTransport != transport
                || transport && dependencies.TransportQualification is null)
            {
                throw new FormatException("Continued geography requires the exact production capability.");
            }
            return dependencies.Decode(bytes);
        }
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
