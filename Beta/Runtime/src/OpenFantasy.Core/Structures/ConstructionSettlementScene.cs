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
    private readonly HashSet<EntityId> _movingIds = [];
    private readonly WorldSnapshot? _basis;

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

    internal ConstructionSettlementScene(WorldSnapshot basis, IEnumerable<TerrainRegionSnapshot> regions)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(regions);
        if (basis.ProfileVersion != 17 || basis.PendingExcavation is not null)
        {
            throw new ArgumentException("Coupled settlement requires a stable world with its owned support policy.", nameof(basis));
        }
        _basis = basis;
        OriginalSpace = basis.Space;
        OriginalPhysical = basis.ConstructionPhysical!;
        Policy = basis.StructuralSettlementPolicy!;
        Regions = new VerticalDescentQuery(basis.Space.Scope, regions, [], []).Regions.ToArray();
        Terrain = new(Regions);
        InputPin = ContentHash.Compute(CanonicalEncodingV1.Frame("construction.settlement-input.v2",
            basis.ContentHash.ToByteArray(), basis.CoupledSettlementPolicy!.ContentHash.ToByteArray(),
            CanonicalEncodingV1.Frame("settlement.candidate-regions", Regions.Select(region =>
                (ReadOnlyMemory<byte>)region.ComputeContentHash().ToByteArray()).ToArray())));
    }

    internal LocalSpatialSnapshot OriginalSpace { get; }
    internal ConstructionPhysicalSnapshot OriginalPhysical { get; }
    internal StructuralSettlementPolicy Policy { get; }
    internal TerrainRegionSnapshot[] Regions { get; }
    internal TerrainVolumeQueries Terrain { get; }
    internal ContentHash InputPin { get; }
    internal SpatialStructure? Moving { get; private set; }
    internal SubunitBox[] MovingBoxes { get; private set; } = [];
    internal bool IsCoupled => _basis is not null;
    internal int MaximumWorkUnits => _basis?.CoupledSettlementPolicy!.MaximumWorkUnits
        ?? ConstructionSettlementResolution.MaximumWorkUnits;
    internal SubunitBox[] Bases { get; private set; } = [];
    internal ActorPresence[] Riders { get; private set; } = [];
    internal VerticalDescentQuery? MovingScene { get; private set; }
    internal bool IsRider(EntityId actorId) => _riderIds.Contains(actorId);
    internal bool IsMoving(EntityId structureId) => _movingIds.Contains(structureId);

    internal void Prepare()
    {
        if (_basis is not null)
        {
            PrepareCoupled(_basis);
            return;
        }
        var affected = OriginalPhysical.GetStructures().Where(structure => structure.Boxes
            .Where(box => box.Minimum.Z == structure.GroundPlaneZ)
            .Any(box => Terrain.Query(box.Volume, requireGroundSupport: true) != SpatialOccupancy.Clear)).ToArray();
        if (affected.Length > 1)
        {
            throw new ArgumentException("Interacting falling assemblies require a coupled collision owner.");
        }
        if (affected.Length == 0) { return; }
        Moving = affected[0];
        _movingIds.Add(Moving.StructureId);
        MovingBoxes = Moving.Boxes.ToArray();
        ConstructionProject project = OriginalPhysical.Construction.Projects.Single(value => value.Installed?.StructureId == Moving.StructureId);
        ValidateIntactPlacement(project);
        FindRiders();
        Bases = Moving.Boxes.Where(box => box.Minimum.Z == Moving.GroundPlaneZ).ToArray();
        Riders = OriginalSpace.Actors.Where(actor => IsRider(actor.ActorId)).ToArray();
        MovingScene = new(OriginalSpace.Scope, Regions,
            OriginalPhysical.GetStructures().Where(structure => structure.StructureId != Moving.StructureId),
            OriginalSpace.Actors.Where(actor => !IsRider(actor.ActorId)));
    }

    private void PrepareCoupled(WorldSnapshot basis)
    {
        SpatialStructure[] structures = OriginalPhysical.GetStructures().ToArray();
        // A lost terrain contact can invalidate one base while its dependents still stand on the
        // original material. Follow the old support graph upward, including whole rigid assemblies.
        foreach (SpatialStructure structure in structures)
        {
            if (structure.Boxes.Where(box => box.Minimum.Z == structure.GroundPlaneZ).Any(box =>
                FootingQuery.Query(Terrain, structures.Where(other => other.StructureId != structure.StructureId).ToArray(),
                    box.Volume) != SpatialOccupancy.Clear))
            {
                _movingIds.Add(structure.StructureId);
            }
        }
        if (_movingIds.Count == 0) { return; }
        ConstructionBearingModel model = basis.StructuralPolicy!.Bind(basis);
        var membership = model.Members.ToDictionary(member => member.ElementId, member => member.StructureId);
        bool expanded;
        do
        {
            expanded = false;
            foreach (StructuralBearing bearing in model.Graph.Bearings)
            {
                if (membership.TryGetValue(bearing.SupportingElementId, out EntityId supporting)
                    && _movingIds.Contains(supporting)
                    && membership.TryGetValue(bearing.SupportedElementId, out EntityId supported))
                {
                    expanded |= _movingIds.Add(supported);
                }
            }
        } while (expanded);
        if (_movingIds.Count > basis.CoupledSettlementPolicy!.MaximumAssemblies)
        {
            throw new ArgumentException("The coupled moving group exceeds its admitted assembly bound.");
        }
        SpatialStructure[] moving = structures.Where(structure => IsMoving(structure.StructureId)).ToArray();
        Moving = moving[0];
        MovingBoxes = moving.SelectMany(structure => structure.Boxes).ToArray();
        foreach (ConstructionProject project in OriginalPhysical.Construction.Projects.Where(project =>
            project.Installed is { } installed && IsMoving(installed.StructureId)))
        {
            ValidateIntactPlacement(project);
        }
        // Carried bases keep their internal group contacts. Only external bases seek a new floor;
        // every member still sweeps against all retained outside terrain, structures and bodies.
        var carriedBases = model.Graph.Bearings.Where(bearing =>
                membership.TryGetValue(bearing.SupportingElementId, out EntityId supporting) && IsMoving(supporting)
                && membership.TryGetValue(bearing.SupportedElementId, out EntityId supported) && supported != supporting)
            .Select(bearing => bearing.SupportedElementId).ToHashSet();
        Bases = moving.SelectMany(structure => structure.Boxes.Where((box, index) =>
            box.Minimum.Z == structure.GroundPlaneZ
            && !carriedBases.Contains(ConstructionBearingModel.MemberIdentity(structure.StructureId, (uint)index)))).ToArray();
        if (Bases.Length == 0) { throw new ArgumentException("A coupled group requires an external base."); }
        FindRiders(moving);
        Riders = OriginalSpace.Actors.Where(actor => IsRider(actor.ActorId)).ToArray();
        MovingScene = new(OriginalSpace.Scope, Regions, structures.Where(structure => !IsMoving(structure.StructureId)),
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
        => FindRiders([Moving!]);

    private void FindRiders(SpatialStructure[] moving)
    {
        const long ticks = StructureBearingSurface.TicksPerSubunit;
        foreach (ActorPresence actor in OriginalSpace.Actors)
        {
            BodyVolume volume = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
            var footprint = new BoundaryRectangle((Int128)volume.MinimumX * ticks, (Int128)volume.MinimumY * ticks,
                (Int128)volume.MaximumX * ticks, (Int128)volume.MaximumY * ticks);
            var surfaces = moving.SelectMany(structure => structure.BearingSurfaces
                .Where(surface => surface.TopTicks == (Int128)actor.Feet.Z * ticks
                    && surface.Bounds.MinU < footprint.MaxU && surface.Bounds.MaxU > footprint.MinU
                    && surface.Bounds.MinV < footprint.MaxV && surface.Bounds.MaxV > footprint.MinV)
                .Select(surface => new StructuralContactSurface(structure.StructureId, surface.Bounds,
                    surface.StandingZ, surface.TopTicks))).ToArray();
            if (surfaces.Length == 0) { continue; }
            _ = StructuralContactWeights.Resolve(footprint, surfaces);
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
