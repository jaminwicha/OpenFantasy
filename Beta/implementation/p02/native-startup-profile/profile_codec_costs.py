"""Refine the measured encoding hotspot on the same failed save; diagnostic instrumentation only."""
import profile_native_startup as profile

for relative, signature, stage in (
    ('Worlds/WorldResourceHistoryCodec.cs', 'public static byte[] Encode(WorldResourceHistory value', 'codec-world-resource'),
    ('Worlds/WorldResourceHistoryCodecV1.cs', 'public static byte[] Encode(WorldResourceHistory value', 'codec-world-resource-body'),
    ('Progression/ResourceHistory/ActorResourceHistoryCodecV1.cs', 'public static byte[] Encode(ActorResourceHistory history', 'codec-actor-resource-history'),
    ('Production/ProductionCodecV2.cs', 'public static byte[] Encode(ProductionSnapshot state', 'codec-production-v2'),
    ('Production/ProductionCodecV9.cs', 'public static byte[] Encode(ProductionSnapshot state', 'codec-production-v9'),
    ('Authority/AuthorityEncodingV1.cs', 'public static byte[] Encode(CountedAuthorityState state', 'codec-inventory'),
    ('Care/CareCodecV2.cs', 'public static byte[] Encode(CareSnapshot state', 'codec-care-v2'),
    ('Care/CareCodecV3.cs', 'public static byte[] Encode(CareSnapshot state', 'codec-care-v3'),
    ('Geography/LiquidReservoirCodecV1.cs', 'internal static byte[] EncodeReservoirs(', 'codec-reservoir-body'),
    ('Geography/LiquidReservoirCodecV3.cs', 'public static byte[] Encode(LiquidReservoirState state', 'codec-reservoir-v3'),
):
    profile.ANCHORS.append((profile.C + relative, profile.sig(signature), profile.scope(stage), True))

if __name__ == '__main__':
    raise SystemExit(profile.main())
