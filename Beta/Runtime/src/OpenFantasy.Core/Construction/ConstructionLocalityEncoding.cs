using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Worlds.WorldSnapshotCodecV1;
using static OpenFantasy.Core.Worlds.WorldSpatialCodecV1;

namespace OpenFantasy.Core.Construction;

/// <summary>
/// Canonical nonterrain image of the complete legacy construction interpretation. This encoding is
/// verified against ordinary replay; it is not an independent decoder or authority admission path.
/// </summary>
public static class ConstructionLocalityEncoding
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 5) throw new ArgumentException("Locality storage requires profile five.", nameof(world));
        return EncodeOwners(world.Inventory, EncodeReferencedComponents(world.Space.Scope, world.Space.Actors,
                world.Space.Regions.Select(TerrainRegionReference.Capture)), world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering,
            world.Production!, world.Progression!, world.Quests!, world.Construction!);
    }

    public static byte[] Encode(ConstructionLocalityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return EncodeOwners(state.Inventory, EncodeReferencedComponents(state.Occupancy.Scope, state.Actors, state.Terrain),
            state.Residents, state.AdmissionVersion, state.Calendar, state.Revision, state.AdvancingSteps,
            state.Dialogues, state.Gathering, state.Production, state.Progression, state.Quests, state.Construction);
    }

    private static byte[] EncodeOwners(CountedAuthorityState inventory, byte[] space,
        IEnumerable<WorldResident> residents, ContentHash admission, WorldInstant calendar, ulong revision, ulong steps,
        IEnumerable<DialogueSession> dialogues, GatheringSnapshot? gathering, ProductionSnapshot production,
        ProgressionSnapshot progression, QuestSnapshot quests, ConstructionSnapshot construction)
    {
        byte[] bytes = CanonicalEncodingV1.Frame("world.construction-locality.v1", Number(5),
            AuthorityEncodingV1.Encode(inventory), space, EncodeResidents(residents), admission.ToByteArray(),
            Signed(calendar.Days), Number((ulong)calendar.MillisecondsWithinDay), Number(revision), Number(steps),
            EncodeDialogues(dialogues), gathering is null ? [] : GatheringCodecV1.Encode(gathering),
            ProductionCodecV1.Encode(production), ProgressionCodecV1.Encode(progression), QuestCodecV1.Encode(quests),
            ConstructionCodecV1.Encode(construction));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
            throw new ArgumentException("The locality image exceeds its retained storage bound.");
        return bytes;
    }
}
