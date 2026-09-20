using System.Buffers.Binary;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>Fixed dependencies shared by continuations. Preparation scans the finite complete scene once.</summary>
internal sealed class ConstructionSettlementScene
{
    private readonly HashSet<EntityId> _riderIds = [];

    internal ConstructionSettlementScene(LocalSpatialSnapshot space, IEnumerable<TerrainRegionSnapshot> regions,
        ConstructionPhysicalSnapshot physical, StructuralSettlementPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(physical);
        ArgumentNullException.ThrowIfNull(policy);
        OriginalSpace = space;
        OriginalPhysical = physical;
        Policy = policy;
        // Validate the catalog without requiring yet-unsupported occupants to stand in the candidate.
        Regions = new VerticalDescentQuery(space.Scope, regions, [], []).Regions.ToArray();
        Terrain = new(Regions);
        InputPin = ComputeInputPin();
    }

    internal LocalSpatialSnapshot OriginalSpace { get; }
    internal ConstructionPhysicalSnapshot OriginalPhysical { get; }
    internal StructuralSettlementPolicy Policy { get; }
    internal TerrainRegionSnapshot[] Regions { get; }
    internal TerrainVolumeQueries Terrain { get; }
    internal ContentHash InputPin { get; }
    internal SpatialStructure? Moving { get; private set; }
    internal SubunitBox[] Bases { get; private set; } = [];
    internal ActorPresence[] Riders { get; private set; } = [];
    internal VerticalDescentQuery? MovingScene { get; private set; }
    internal bool IsRider(EntityId actorId) => _riderIds.Contains(actorId);

    internal void Prepare()
    {
        var affected = OriginalPhysical.GetStructures().Where(structure => structure.Boxes
            .Where(box => box.Minimum.Z == structure.GroundPlaneZ)
            .Any(box => Terrain.Query(box.Volume, requireGroundSupport: true) != SpatialOccupancy.Clear)).ToArray();
        if (affected.Length > 1)
        {
            throw new ArgumentException("Interacting falling assemblies require a coupled collision owner.");
        }
        if (affected.Length == 0) { return; }
        Moving = affected[0];
        ConstructionProject project = OriginalPhysical.Construction.Projects.Single(value => value.Installed?.StructureId == Moving.StructureId);
        ValidateIntactPlacement(project);
        FindRiders();
        Bases = Moving.Boxes.Where(box => box.Minimum.Z == Moving.GroundPlaneZ).ToArray();
        Riders = OriginalSpace.Actors.Where(actor => IsRider(actor.ActorId)).ToArray();
        MovingScene = new(OriginalSpace.Scope, Regions,
            OriginalPhysical.GetStructures().Where(structure => structure.StructureId != Moving.StructureId),
            OriginalSpace.Actors.Where(actor => !IsRider(actor.ActorId)));
    }

    internal static void ValidateSweep(VerticalDescentQuery scene, SubunitBox box, long drop)
    {
        VerticalDescentResult result = scene.QueryBox(box, drop);
        if (result.DropSubunits != drop || result.Status is not VerticalDescentStatus.Landed and not VerticalDescentStatus.NoContactWithinLimit)
        {
            throw new ArgumentException("Every moving member and rider requires an unobstructed complete swept trajectory.");
        }
    }

    internal static SubunitBox Box(BodyVolume volume) => new(new(volume.MinimumX, volume.MinimumY, volume.MinimumZ),
        new(volume.MaximumX, volume.MaximumY, volume.MaximumZ));

    private void ValidateIntactPlacement(ConstructionProject project)
    {
        ConstructionMemberState first = OriginalPhysical.GetMember(project.Installed!.StructureId, 0);
        foreach (ConstructionMemberState member in OriginalPhysical.Members.Where(value => value.StructureId == project.Installed.StructureId))
        {
            MillimetreBox box = project.Definition.Members[(int)member.MemberIndex];
            if (member.Displacement != first.Displacement || member.Disposition != first.Disposition
                || member.Displacement.X != 0 || member.Displacement.Y != 0 || member.Displacement.Z > 0
                || member.Disposition == ConstructionMemberDisposition.Falling
                || (Int128)box.MinZ * StructureBearingSurface.TicksPerMillimetre % StructureBearingSurface.TicksPerSubunit != 0
                || (Int128)box.MaxZ * StructureBearingSurface.TicksPerMillimetre % StructureBearingSurface.TicksPerSubunit != 0)
            {
                throw new ArgumentException("Intact settling requires uniform vertical placement and exact subunit contact planes.");
            }
        }
    }

    private void FindRiders()
    {
        const long ticks = StructureBearingSurface.TicksPerSubunit;
        foreach (ActorPresence actor in OriginalSpace.Actors)
        {
            BodyVolume volume = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
            var footprint = new BoundaryRectangle((Int128)volume.MinimumX * ticks, (Int128)volume.MinimumY * ticks,
                (Int128)volume.MaximumX * ticks, (Int128)volume.MaximumY * ticks);
            var surfaces = Moving!.BearingSurfaces.Where(surface => surface.TopTicks == (Int128)actor.Feet.Z * ticks
                && surface.Bounds.MinU < footprint.MaxU && surface.Bounds.MaxU > footprint.MinU
                && surface.Bounds.MinV < footprint.MaxV && surface.Bounds.MaxV > footprint.MinV).ToArray();
            if (surfaces.Length == 0) { continue; }
            _ = StructuralContactWeights.Resolve(footprint, surfaces.Select(surface =>
                new StructuralContactSurface(Moving.StructureId, surface.Bounds, surface.StandingZ, surface.TopTicks)));
            _riderIds.Add(actor.ActorId);
        }
    }

    private ContentHash ComputeInputPin()
    {
        var originalStructures = OriginalSpace.Structures.Select(structure => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame(
            "settlement.structure", structure.StructureId.ToByteArray(),
            CanonicalEncodingV1.Frame("settlement.boxes", structure.Boxes.Select(box => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame(
                "settlement.box", Signed(box.Minimum.X), Signed(box.Minimum.Y), Signed(box.Minimum.Z),
                Signed(box.Maximum.X), Signed(box.Maximum.Y), Signed(box.Maximum.Z))).ToArray()),
            CanonicalEncodingV1.Frame("settlement.surfaces", structure.BearingSurfaces.Select(surface => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame(
                "settlement.surface", Wide(surface.Bounds.MinU), Wide(surface.Bounds.MinV), Wide(surface.Bounds.MaxU),
                Wide(surface.Bounds.MaxV), Wide(surface.TopTicks))).ToArray()))).ToArray();
        return ContentHash.Compute(CanonicalEncodingV1.Frame("construction.settlement-input.v1",
            ContentHash.Compute(WorldSpatialCodecV1.EncodeComponents(OriginalSpace, (ulong)OriginalSpace.GroundSupport)).ToByteArray(),
            ContentHash.Compute(CanonicalEncodingV1.Frame("settlement.structures", originalStructures)).ToByteArray(),
            OriginalPhysical.ContentHash.ToByteArray(),
            CanonicalEncodingV1.Frame("settlement.candidate-regions", Regions.Select(region =>
                (ReadOnlyMemory<byte>)region.ComputeContentHash().ToByteArray()).ToArray()), Policy.ContentHash.ToByteArray()));
    }

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Wide(Int128 value)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, unchecked((ulong)(value >> 64)));
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8), unchecked((ulong)value));
        return bytes;
    }
}
