using System.Buffers.Binary;
using System.Collections.ObjectModel;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Complete read-only contact proposal retaining a world's material, bodies and terrain lineage.
/// Clearance does not establish footing, strength, a trajectory or permission to publish the proposal.
/// </summary>
public sealed class ConstructionContactScene
{
    private readonly Dictionary<EntityId, SubunitPosition> _origins = [];

    public ConstructionContactScene(WorldSnapshot basis, IEnumerable<TerrainRegionSnapshot> regions,
        ConstructionPhysicalSnapshot physical, IEnumerable<ActorPresence> actors)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(physical);
        ArgumentNullException.ThrowIfNull(actors);
        if (basis.Construction is not { } construction || basis.ConstructionPhysical is not { } retained
            || physical.ConstructionHash != construction.ContentHash
            || physical.WorldId != basis.WorldId || physical.BranchId != basis.BranchId
            || physical.Revision < retained.Revision)
        {
            throw new ArgumentException("A contact scene requires the retained fabrication and physical owner without revision rewind.", nameof(physical));
        }

        Basis = basis;
        Physical = physical;
        Scope = basis.Space.Scope;
        // The descent catalog admits unsupported geometry while preserving the existing finite scene bounds.
        var candidate = new VerticalDescentQuery(Scope, regions, physical.GetStructures(), actors);
        Regions = candidate.Regions;
        Actors = candidate.Actors;
        ValidateRegions();
        ValidateActors();
        ValidateIntactPlacement(retained);
        ValidateClearance(candidate.Structures);
        InputPin = ComputeInputPin();
    }

    public WorldSnapshot Basis { get; }
    public ConstructionPhysicalSnapshot Physical { get; }
    public ReadOnlyCollection<TerrainRegionSnapshot> Regions { get; }
    public ReadOnlyCollection<ActorPresence> Actors { get; }
    public TerrainScope Scope { get; }
    public ContentHash InputPin { get; }

    /// <summary>Returns the candidate origin of the retained installed project selected by its identity.</summary>
    public SubunitPosition OriginFor(ConstructionProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return _origins.TryGetValue(project.ProjectId, out SubunitPosition origin) ? origin
            : throw new ArgumentException("The project is not an installed assembly in this contact scene.", nameof(project));
    }

    private void ValidateRegions()
    {
        if (Regions.Count != Basis.Space.Regions.Count)
        {
            throw new ArgumentException("Contact terrain must retain the complete original region partition.");
        }
        for (int index = 0; index < Regions.Count; index++)
        {
            TerrainRegionSnapshot previous = Basis.Space.Regions[index];
            TerrainRegionSnapshot candidate = Regions[index];
            if (candidate.Bounds != previous.Bounds || candidate.Scope != previous.Scope
                || candidate.Seed != previous.Seed || candidate.Rules.Version != previous.Rules.Version
                || candidate.Revision < previous.Revision || candidate.Cells.Count != previous.Cells.Count)
            {
                throw new ArgumentException("Contact terrain must retain its exact partition, scope, generator and nondecreasing revisions.");
            }
            for (int cellIndex = 0; cellIndex < previous.Cells.Count; cellIndex++)
            {
                TerrainCell before = previous.Cells[cellIndex];
                TerrainCell after = candidate.Cells[cellIndex];
                if (before.Coordinate != after.Coordinate || before.Stratum != after.Stratum
                    || before.Material != after.Material || before.FormationId != after.FormationId
                    || before.SourceId != after.SourceId || before.IsExcavated && !after.IsExcavated
                    || !before.IsExcavated && after.IsExcavated && !before.IsSolid)
                {
                    throw new ArgumentException("Contact terrain can add excavations but cannot replace substrate or restore existing holes.");
                }
            }
        }
    }

    private void ValidateActors()
    {
        if (Actors.Count != Basis.Space.Actors.Count)
        {
            throw new ArgumentException("A contact scene must retain every original actor exactly once.");
        }
        for (int index = 0; index < Actors.Count; index++)
        {
            ActorPresence previous = Basis.Space.Actors[index];
            ActorPresence candidate = Actors[index];
            if (candidate.ActorId != previous.ActorId || candidate.Binding != previous.Binding
                || candidate.Condition.ComputeContentHash() != previous.Condition.ComputeContentHash()
                || candidate.HomePlaceId != previous.HomePlaceId || candidate.Scope != previous.Scope
                || candidate.Revision < previous.Revision)
            {
                throw new ArgumentException("Contact actors retain their exact binding, condition, home and nondecreasing revision; only poses may change.");
            }
        }
    }

    private void ValidateIntactPlacement(ConstructionPhysicalSnapshot retained)
    {
        foreach (ConstructionProject project in Basis.Construction!.Projects.Where(value => value.Installed is not null))
        {
            EntityId structure = project.Installed!.StructureId;
            ConstructionMemberState previousOrigin = retained.GetMember(structure, 0);
            ConstructionMemberState candidateOrigin = Physical.GetMember(structure, 0);
            for (uint index = 0; index < project.Definition.Members.Count; index++)
            {
                ConstructionMemberState previous = retained.GetMember(structure, index);
                ConstructionMemberState candidate = Physical.GetMember(structure, index);
                if (!HasUniformVerticalPose(previous, previousOrigin) || !HasUniformVerticalPose(candidate, candidateOrigin)
                    || candidate.Displacement.Z > previous.Displacement.Z
                    || candidate.ConditionParts != previous.ConditionParts || candidate.Revision < previous.Revision)
                {
                    throw new ArgumentException("Contact assemblies require intact uniform vertical poses at or below their retained origins, with unchanged condition.");
                }
            }
            _origins.Add(project.ProjectId, project.SiteFeet.Offset(0, 0, candidateOrigin.Displacement.Z));
        }
    }

    private static bool HasUniformVerticalPose(ConstructionMemberState member, ConstructionMemberState origin) =>
        member.Displacement == origin.Displacement && member.Disposition == origin.Disposition
        && member.Displacement.X == 0 && member.Displacement.Y == 0 && member.Displacement.Z <= 0
        && member.Disposition != ConstructionMemberDisposition.Falling;

    private void ValidateClearance(IReadOnlyList<SpatialStructure> structures)
    {
        var terrain = new TerrainVolumeQueries(Regions);
        BodyVolume[] occupants = Actors.Select(actor => BodyVolume.At(actor.Feet,
            actor.Condition.Definition.ClearanceDimensions)).ToArray();
        for (int index = 0; index < structures.Count; index++)
        {
            SpatialStructure structure = structures[index];
            foreach (SubunitBox box in structure.Boxes)
            {
                RequireTerrainClearance(terrain, box.Volume);
                // Rounded boxes inside one original assembly remain a union. Other assemblies retain
                // every full collider; exact physical overlap was also checked by the physical owner.
                if (structures.Take(index).Any(other => other.Boxes.Any(existing => box.Volume.Overlaps(existing.Volume)))
                    || occupants.Any(actor => box.Volume.Overlaps(actor)))
                {
                    throw new ArgumentException("Contact material cannot overlap another assembly or a corporeal actor.");
                }
            }
        }
        for (int index = 0; index < occupants.Length; index++)
        {
            RequireTerrainClearance(terrain, occupants[index]);
            if (occupants.Take(index).Any(previous => occupants[index].Overlaps(previous)))
            {
                throw new ArgumentException("Contact actors cannot overlap another corporeal actor.");
            }
        }
    }

    private static void RequireTerrainClearance(TerrainVolumeQueries terrain, BodyVolume volume)
    {
        if (terrain.Query(volume, requireGroundSupport: false) != SpatialOccupancy.Clear)
        {
            throw new ArgumentException("Every contact volume requires complete known terrain clearance.");
        }
    }

    private ContentHash ComputeInputPin() => ContentHash.Compute(CanonicalEncodingV1.Frame("construction.contact-scene.v1",
        Basis.ContentHash.ToByteArray(), Scope.WorldId.ToByteArray(), Scope.BranchId.ToByteArray(), Scope.RealmId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64((ulong)Basis.Space.GroundSupport), Physical.ContentHash.ToByteArray(),
        CanonicalEncodingV1.Frame("construction.contact-regions", Regions.Select(region =>
            (ReadOnlyMemory<byte>)region.ComputeContentHash().ToByteArray()).ToArray()),
        CanonicalEncodingV1.Frame("construction.contact-actors", Actors.Select(actor =>
            (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("construction.contact-actor",
                BodyContractEncodingV1.EncodeBinding(actor.Binding), BodyContractEncodingV1.EncodeCondition(actor.Condition),
                Signed(actor.Feet.X), Signed(actor.Feet.Y), Signed(actor.Feet.Z),
                CanonicalEncodingV1.Unsigned64(actor.Revision), actor.HomePlaceId.ToByteArray())).ToArray())));

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}
