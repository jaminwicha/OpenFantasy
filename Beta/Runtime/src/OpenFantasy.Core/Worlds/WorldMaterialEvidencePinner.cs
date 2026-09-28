using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Digests owner candidates with the component encoders of the complete profile 26 root. Owners are
/// immutable, so one digest per owner instance is reused while a single decision is prepared.
/// </summary>
internal sealed class WorldMaterialEvidencePinner
{
    private readonly Dictionary<object, ContentHash> _digests = new(ReferenceEqualityComparer.Instance);

    internal WorldMaterialOwnerPins Pin(in WorldMaterialEvidenceOwners owners) => new(
        Digest(owners.Inventory, static value => AuthorityEncodingV1.Encode(value)),
        Digest(owners.Production, static value => ProductionCodecV2.Encode(value)),
        Digest(owners.Gathering, static value => GatheringCodecV1.Encode(value)),
        Digest(owners.Excavation, static value => ExcavationCodecV2.Encode(value)),
        Digest(owners.Construction, static value => ConstructionCodecV1.Encode(value)),
        // Spatial components follow the current support mode. The Construction pin covers derived installed structures.
        Digest(owners.Space, static value => ExcavationMaterialSource.EncodeSpace(value)),
        Digest(owners.Quests, static value => QuestCodecV1.Encode(value)),
        Digest(owners.Progression, static value => ProgressionCodecV1.Encode(value)),
        Digest(owners.Care, static value => CareCodecV2.Encode(value)),
        Digest(owners.Reservoirs, static value => LiquidReservoirCodecV2.Encode(value)),
        Digest(owners.LiquidCells, static value => LiquidCellStateCodecV2.Encode(value)));

    private ContentHash? Digest<TOwner>(TOwner? owner, Func<TOwner, byte[]> encode) where TOwner : class
    {
        if (owner is null)
        {
            return null;
        }
        if (!_digests.TryGetValue(owner, out ContentHash? digest))
        {
            digest = ContentHash.Compute(encode(owner));
            _digests.Add(owner, digest);
        }
        return digest;
    }
}
