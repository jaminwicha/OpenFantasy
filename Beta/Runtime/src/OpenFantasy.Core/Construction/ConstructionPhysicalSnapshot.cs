using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Construction;

/// <summary>
/// Complete physical member lifecycle bound to retained fabrication. Geometry does not establish terrain
/// contact, force admission or a fall trajectory, and every member keeps its original installed material backing.
/// </summary>
public sealed class ConstructionPhysicalSnapshot
{
    public const int MaximumMembers = ConstructionSnapshot.MaximumProjects * ConstructionDefinition.MaximumMembers;
    private readonly Dictionary<(EntityId Structure, uint Member), ConstructionMemberState> _members = [];
    private readonly Dictionary<(EntityId Structure, uint Member), StructuralMemberReference> _references = [];
    private readonly ReadOnlyCollection<SpatialStructure> _structures;

    public ConstructionPhysicalSnapshot(ConstructionSnapshot construction, ulong revision,
        IEnumerable<ConstructionMemberState> members)
    {
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(members);
        Construction = construction;
        ConstructionHash = construction.ContentHash;
        Revision = revision;
        foreach (ConstructionMemberState member in members)
        {
            ArgumentNullException.ThrowIfNull(member);
            if (_members.Count == MaximumMembers || member.Revision > revision
                || !_members.TryAdd((member.StructureId, member.MemberIndex), member))
            {
                throw new ArgumentException("Physical members require bounded unique identities and revisions within their owner.", nameof(members));
            }
        }
        HashSet<EntityId> structures = [];
        HashSet<EntityId> installedLots = [];
        var masses = new StructuralMassCatalog([], []);
        foreach (ConstructionProject project in construction.Projects.Where(value => value.Installed is not null))
        {
            InstalledStructure installed = project.Installed!;
            if (!structures.Add(installed.StructureId) || !installedLots.Add(installed.GuardLotId))
            {
                throw new ArgumentException("Each completed assembly requires exclusive structure and installed material identities.", nameof(construction));
            }
            var memberMasses = masses.MemberMasses(project.Definition);
            for (uint index = 0; index < project.Definition.Members.Count; index++)
            {
                if (!_members.TryGetValue((installed.StructureId, index), out ConstructionMemberState? state))
                {
                    throw new ArgumentException("Every installed member must retain exactly one physical state.", nameof(members));
                }
                _references.Add((installed.StructureId, index), new(state.ElementId, project.ProjectId,
                    installed.StructureId, installed.GuardLotId, index, memberMasses[(int)index],
                    new(project.Definition.Members[(int)index].VolumeMicrolitres)));
            }
        }
        if (_references.Count != _members.Count)
        {
            throw new ArgumentException("A physical state cannot name an unknown or unbuilt member.", nameof(members));
        }
        Members = Array.AsReadOnly(_members.Values.OrderBy(value => value.StructureId).ThenBy(value => value.MemberIndex).ToArray());
        _structures = ProjectStructures();
    }

    public EntityId WorldId => Construction.WorldId;
    public EntityId BranchId => Construction.BranchId;
    public ulong Revision { get; }
    public ContentHash ConstructionHash { get; }
    public ConstructionSnapshot Construction { get; }
    public ReadOnlyCollection<ConstructionMemberState> Members { get; }
    public ContentHash ContentHash => ContentHash.Compute(ConstructionPhysicalCodecV1.Encode(this));

    public static ConstructionPhysicalSnapshot Create(ConstructionSnapshot construction)
    {
        ArgumentNullException.ThrowIfNull(construction);
        return new(construction, 0, construction.Projects.Where(value => value.Installed is not null)
            .SelectMany(project => Enumerable.Range(0, project.Definition.Members.Count)
                .Select(index => Initial(project.Installed!.StructureId, (uint)index))));
    }

    public ConstructionMemberState GetMember(EntityId structureId, uint memberIndex) =>
        _members.TryGetValue((structureId, memberIndex), out ConstructionMemberState? member) ? member
            : throw new ArgumentException("The member is outside this retained physical construction.");

    /// <summary>Original conserved mass, exact volume and sole installed-lot backing, independent of placement or damage.</summary>
    public StructuralMemberReference GetMemberReference(EntityId structureId, uint memberIndex) =>
        _references.TryGetValue((structureId, memberIndex), out StructuralMemberReference? reference) ? reference
            : throw new ArgumentException("The member is outside this retained physical construction.");

    public ReadOnlyCollection<SpatialStructure> GetStructures() => _structures;

    /// <summary>
    /// Stable-world contact admission: every exact connected component keeps its own terrain-grounded
    /// lowest members. This proves neither structural load safety nor a trajectory into the supplied placement.
    /// Complete clearance and exclusive occupancy remain the local spatial owner's responsibility.
    /// Terrain contact uses conservative envelopes, retaining the existing physical bottom gap of less than
    /// one subunit where outward quantization places the collider on a supported cell plane.
    /// </summary>
    public void ValidateStableGeometry(LocalSpatialSnapshot space)
    {
        ArgumentNullException.ThrowIfNull(space);
        if (space.Scope.WorldId != WorldId || space.Scope.BranchId != BranchId
            || Members.Any(value => value.Disposition == ConstructionMemberDisposition.Falling))
        {
            throw new ArgumentException("Stable physical construction requires its exact world scope and no falling members.", nameof(space));
        }
        var terrain = new TerrainVolumeQueries(space.Regions);
        foreach (ConstructionProject project in Construction.Projects.Where(value => value.Installed is not null))
        {
            int count = project.Definition.Members.Count;
            var exact = new ExactBox[count];
            var collision = new SubunitBox[count];
            for (int index = 0; index < count; index++)
            {
                MillimetreBox member = project.Definition.Members[index];
                SubunitPosition displacement = GetMember(project.Installed!.StructureId, (uint)index).Displacement;
                SubunitPosition origin = project.SiteFeet.Offset(displacement.X, displacement.Y, displacement.Z);
                exact[index] = ExactBox.At(member, origin);
                collision[index] = member.At(origin);
            }
            var visited = new bool[count];
            for (int first = 0; first < count; first++)
            {
                if (visited[first]) { continue; }
                List<int> component = [first];
                visited[first] = true;
                for (int cursor = 0; cursor < component.Count; cursor++)
                {
                    for (int other = 0; other < count; other++)
                    {
                        if (!visited[other] && exact[component[cursor]].SharesFace(exact[other]))
                        {
                            visited[other] = true;
                            component.Add(other);
                        }
                    }
                }
                Int128 lowest = component.Min(index => exact[index].MinZ);
                foreach (int index in component.Where(index => exact[index].MinZ == lowest))
                {
                    if (terrain.Query(collision[index].Volume, requireGroundSupport: true) != SpatialOccupancy.Clear)
                    {
                        throw new ArgumentException("Every detached physical component requires complete terrain support under its lowest members.", nameof(space));
                    }
                }
            }
        }
    }

    public ConstructionPhysicalSnapshot WithMember(ulong expectedSnapshotRevision, ConstructionMemberState updatedMember) =>
        WithMembers(expectedSnapshotRevision, [updatedMember]);

    /// <summary>One atomic owner transition; moved members are validated together at their final supplied placements.</summary>
    public ConstructionPhysicalSnapshot WithMembers(ulong expectedSnapshotRevision, IEnumerable<ConstructionMemberState> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (expectedSnapshotRevision != Revision)
        {
            throw new ArgumentException("The physical snapshot revision does not match.", nameof(expectedSnapshotRevision));
        }
        Dictionary<(EntityId Structure, uint Member), ConstructionMemberState> changed = [];
        foreach (ConstructionMemberState update in updates)
        {
            ArgumentNullException.ThrowIfNull(update);
            ConstructionMemberState previous = GetMember(update.StructureId, update.MemberIndex);
            if (changed.Count == MaximumMembers || !changed.TryAdd((update.StructureId, update.MemberIndex), update)
                || update.Revision != checked(previous.Revision + 1)
                || update.ConditionParts == previous.ConditionParts && update.Displacement == previous.Displacement
                    && update.Disposition == previous.Disposition)
            {
                throw new ArgumentException("Updates must be a unique changed subset, each advancing its exact member revision once.", nameof(updates));
            }
        }
        if (changed.Count == 0)
        {
            throw new ArgumentException("An atomic physical transition must change at least one member.", nameof(updates));
        }
        return new(Construction, checked(Revision + 1), Members.Select(member =>
            changed.GetValueOrDefault((member.StructureId, member.MemberIndex), member)));
    }

    public ConstructionPhysicalSnapshot ReconcileCompletions(ConstructionSnapshot updatedConstruction)
    {
        ArgumentNullException.ThrowIfNull(updatedConstruction);
        if (updatedConstruction.WorldId != WorldId || updatedConstruction.BranchId != BranchId
            || updatedConstruction.Revision < Construction.Revision
            || updatedConstruction.Projects.Count != Construction.Projects.Count
            || !updatedConstruction.Definitions.Select(value => (value.DefinitionId, value.Version))
                .SequenceEqual(Construction.Definitions.Select(value => (value.DefinitionId, value.Version))))
        {
            throw new ArgumentException("Fabrication reconciliation requires the same scope and admitted catalogs with nondecreasing work.", nameof(updatedConstruction));
        }
        foreach (ConstructionProject previous in Construction.Projects)
        {
            ConstructionProject? current = updatedConstruction.FindProject(previous.ProjectId);
            if (current is null || current.Revision < previous.Revision || !RetainsProjectBindings(previous, current)
                || previous.Installed is not null && previous.Installed != current.Installed)
            {
                throw new ArgumentException("Reconciliation cannot rewind work or replace a project's admitted bindings and completed source.", nameof(updatedConstruction));
            }
        }
        List<ConstructionMemberState> combined = [.. Members];
        foreach (ConstructionProject project in updatedConstruction.Projects.Where(value => value.Installed is not null))
        {
            for (uint index = 0; index < project.Definition.Members.Count; index++)
            {
                if (!_members.ContainsKey((project.Installed!.StructureId, index)))
                {
                    combined.Add(Initial(project.Installed.StructureId, index));
                }
            }
        }
        return updatedConstruction.ContentHash == ConstructionHash ? this
            : new(updatedConstruction, combined.Count == Members.Count ? Revision : checked(Revision + 1), combined);
    }

    private ReadOnlyCollection<SpatialStructure> ProjectStructures()
    {
        List<SpatialStructure> structures = [];
        List<ExactBox> occupied = [];
        foreach (ConstructionProject project in Construction.Projects.Where(value => value.Installed is not null))
        {
            List<SubunitBox> boxes = [];
            List<StructureBearingSurface> surfaces = [];
            foreach (var (member, index) in project.Definition.Members.Select((member, index) => (member, index)))
            {
                ConstructionMemberState state = GetMember(project.Installed!.StructureId, (uint)index);
                SubunitPosition origin = project.SiteFeet.Offset(state.Displacement.X, state.Displacement.Y, state.Displacement.Z);
                ExactBox exact = ExactBox.At(member, origin);
                if (occupied.Any(exact.Overlaps))
                {
                    throw new ArgumentException("Physical member material cannot overlap after displacement.");
                }
                occupied.Add(exact);
                boxes.Add(member.At(origin));
                if (state.Disposition != ConstructionMemberDisposition.Falling && state.ConditionParts > 0)
                {
                    surfaces.Add(StructureBearingSurface.FromMember(member, origin));
                }
            }
            // Keep every collider, including failed and falling material. Existing finite span/box limits still apply.
            structures.Add(new(project.Installed!.StructureId, boxes, surfaces));
        }
        return Array.AsReadOnly(structures.OrderBy(value => value.StructureId).ToArray());
    }

    private static ConstructionMemberState Initial(EntityId structure, uint index) =>
        new(structure, index, 0, StructuralElement.MaximumConditionParts, default, ConstructionMemberDisposition.Standing);

    private static bool RetainsProjectBindings(ConstructionProject previous, ConstructionProject current) =>
        previous.Definition.DefinitionId == current.Definition.DefinitionId && previous.Definition.Version == current.Definition.Version
        && previous.SiteFeet == current.SiteFeet && previous.WorkFeet == current.WorkFeet
        && previous.RequiredAccess.SequenceEqual(current.RequiredAccess)
        && previous.OwnerActorId == current.OwnerActorId && previous.BuilderActorId == current.BuilderActorId
        && previous.QuestId == current.QuestId && previous.ProjectCustodianId == current.ProjectCustodianId
        && previous.ReservationId == current.ReservationId && previous.StockLocation == current.StockLocation;

    private readonly record struct ExactBox(Int128 MinX, Int128 MinY, Int128 MinZ, Int128 MaxX, Int128 MaxY, Int128 MaxZ)
    {
        internal static ExactBox At(MillimetreBox member, SubunitPosition origin) => new(
            Coordinate(origin.X, member.MinX), Coordinate(origin.Y, member.MinY), Coordinate(origin.Z, member.MinZ),
            Coordinate(origin.X, member.MaxX), Coordinate(origin.Y, member.MaxY), Coordinate(origin.Z, member.MaxZ));

        internal bool Overlaps(ExactBox other) => MinX < other.MaxX && MaxX > other.MinX
            && MinY < other.MaxY && MaxY > other.MinY && MinZ < other.MaxZ && MaxZ > other.MinZ;

        internal bool SharesFace(ExactBox other) =>
            (MaxX == other.MinX || MinX == other.MaxX) && MinY < other.MaxY && MaxY > other.MinY && MinZ < other.MaxZ && MaxZ > other.MinZ
            || (MaxY == other.MinY || MinY == other.MaxY) && MinX < other.MaxX && MaxX > other.MinX && MinZ < other.MaxZ && MaxZ > other.MinZ
            || (MaxZ == other.MinZ || MinZ == other.MaxZ) && MinX < other.MaxX && MaxX > other.MinX && MinY < other.MaxY && MaxY > other.MinY;

        private static Int128 Coordinate(long subunits, long millimetres) =>
            (Int128)subunits * StructureBearingSurface.TicksPerSubunit + (Int128)millimetres * StructureBearingSurface.TicksPerMillimetre;
    }
}
