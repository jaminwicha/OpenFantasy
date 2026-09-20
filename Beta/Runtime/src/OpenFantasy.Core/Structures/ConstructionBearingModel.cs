using System.Collections.ObjectModel;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

public sealed record StructuralMemberReference(EntityId ElementId, EntityId ProjectId, EntityId StructureId,
    EntityId InstalledLotId, uint MemberIndex, MassMilligrams Mass, VolumeMicrolitres Volume);

public sealed record StructuralActorReference(EntityId ElementId, EntityId ActorId, ContentHash BodyVersion,
    MassMilligrams BodyAndCarriedMass);

public sealed record StructuralMemberCondition(EntityId StructureId, uint MemberIndex, uint ConditionParts);

/// <summary>Read-only exact binding from funded construction and real occupants into a complete finite bearing graph.</summary>
public sealed class ConstructionBearingModel
{
    private ConstructionBearingModel(WorldSnapshot world, StructuralLoadGraph graph,
        IEnumerable<StructuralMemberReference> members, IEnumerable<StructuralActorReference> actors,
        IEnumerable<StableTerrainBearing> foundations, IEnumerable<ConstructionBearingTemplate> templates,
        ContentHash? candidateScenePin = null, ContentHash? policyPin = null)
    {
        WorldHash = world.ContentHash;
        CandidateScenePin = candidateScenePin;
        PolicyPin = policyPin;
        Graph = graph;
        Members = Array.AsReadOnly(members.OrderBy(value => value.ElementId).ToArray());
        Actors = Array.AsReadOnly(actors.OrderBy(value => value.ElementId).ToArray());
        Foundations = Array.AsReadOnly(foundations.OrderBy(value => value.ElementId).ToArray());
        Templates = Array.AsReadOnly(templates.OrderBy(value => value.DefinitionId).ToArray());
        InputPin = ContentHash.Compute(CanonicalEncodingV1.Frame("construction.bearing-model.v1", WorldHash.ToByteArray(),
            StructuralLoadCodec.EncodeGraph(graph),
            CanonicalEncodingV1.Frame("structure.template-pins", Templates.Select(value =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("structure.template-pin", value.DefinitionId.ToByteArray(), value.Version.ToByteArray())).ToArray()),
            CanonicalEncodingV1.Frame("structure.foundation-pins", Foundations.Select(value =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("structure.foundation-pin", value.ElementId.ToByteArray(),
                    CanonicalEncodingV1.Unsigned64(unchecked((ulong)value.Cell.X)),
                    CanonicalEncodingV1.Unsigned64(unchecked((ulong)value.Cell.Y)),
                    CanonicalEncodingV1.Unsigned64(unchecked((ulong)value.Cell.Z)), value.SourceId.ToByteArray(),
                    value.Material.DefinitionId.ToByteArray(), value.Material.Version.ToByteArray())).ToArray())));
        if (candidateScenePin is not null && policyPin is not null)
        {
            InputPin = ContentHash.Compute(CanonicalEncodingV1.Frame("construction.contact-assessment.v1",
                InputPin.ToByteArray(), candidateScenePin.ToByteArray(), policyPin.ToByteArray()));
        }
    }

    public ContentHash? CandidateScenePin { get; }
    public ContentHash? PolicyPin { get; }
    public ContentHash WorldHash { get; }
    public ContentHash InputPin { get; }
    public StructuralLoadGraph Graph { get; }
    public ReadOnlyCollection<StructuralMemberReference> Members { get; }
    public ReadOnlyCollection<StructuralActorReference> Actors { get; }
    public ReadOnlyCollection<StableTerrainBearing> Foundations { get; }
    public ReadOnlyCollection<ConstructionBearingTemplate> Templates { get; }

    public static ConstructionBearingModel Bind(WorldSnapshot world, IEnumerable<ConstructionBearingTemplate> templates,
        IEnumerable<StableTerrainBearing> foundations, StructuralMassCatalog masses,
        IEnumerable<StructuralMemberCondition>? conditions = null) =>
        BindCore(world, templates, foundations, masses, conditions);

    internal static ConstructionBearingModel AssessContacts(ConstructionContactScene scene,
        IEnumerable<ConstructionBearingTemplate> templates, ConstructionLoadPolicy policy) =>
        BindCore(scene.Basis, templates, policy.Foundations, policy.Masses, null, scene, policy.ContentHash);

    private static ConstructionBearingModel BindCore(WorldSnapshot world, IEnumerable<ConstructionBearingTemplate> templates,
        IEnumerable<StableTerrainBearing> foundations, StructuralMassCatalog masses,
        IEnumerable<StructuralMemberCondition>? conditions, ConstructionContactScene? candidate = null,
        ContentHash? policyPin = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(foundations);
        ArgumentNullException.ThrowIfNull(masses);
        if (world.Construction is null || world.Production is null)
        {
            throw new ArgumentException("Structural binding requires retained construction and actual production provenance.");
        }
        var ratings = templates.Take(ConstructionSnapshot.MaximumDefinitions + 1).ToArray();
        var anchors = foundations.Take(StructuralLoadGraph.MaximumElements + 1).ToArray();
        var wear = (conditions ?? []).Take(ConstructionSnapshot.MaximumProjects * ConstructionDefinition.MaximumMembers + 1).ToArray();
        bool allowsSettlement = candidate is not null || world.StructuralSettlementPolicy is not null;
        ConstructionPhysicalSnapshot? boundPhysical = candidate?.Physical ?? world.ConstructionPhysical;
        if (allowsSettlement && world.ConstructionPhysical is null)
        {
            throw new ArgumentException("Structural settlement requires retained physical member placement.");
        }
        if (world.ConstructionPhysical is { } physical && (wear.Length != 0
            || !allowsSettlement && physical.Members.Any(value => value.Disposition != ConstructionMemberDisposition.Standing)))
        {
            throw new ArgumentException("Retained physical condition cannot be replaced by diagnostic wear; detached material requires a new contact model.");
        }
        if (ratings.Length > ConstructionSnapshot.MaximumDefinitions || ratings.Select(value => value.DefinitionId).Distinct().Count() != ratings.Length
            || anchors.Length == 0 || anchors.Length > StructuralLoadGraph.MaximumElements
            || anchors.Select(value => value.ElementId).Distinct().Count() != anchors.Length
            || anchors.Select(value => value.Cell).Distinct().Count() != anchors.Length
            || wear.Length > ConstructionSnapshot.MaximumProjects * ConstructionDefinition.MaximumMembers
            || wear.Any(value => value.ConditionParts > 10000)
            || wear.Select(value => (value.StructureId, value.MemberIndex)).Distinct().Count() != wear.Length)
        {
            throw new ArgumentException("Structural binding requires finite unique definitions, anchors and member conditions.");
        }
        Dictionary<EntityId, ConstructionBearingTemplate> byDefinition = ratings.ToDictionary(value => value.DefinitionId);
        Dictionary<(EntityId Structure, uint Member), uint> conditionByMember = wear.ToDictionary(
            value => (value.StructureId, value.MemberIndex), value => value.ConditionParts);
        List<StructuralElement> elements = [];
        List<StructuralBearing> bearings = [];
        List<StructuralMemberReference> members = [];
        List<StructuralActorReference> actors = [];
        List<StructuralContactSurface> contacts = [];
        List<(EntityId Element, EntityId Structure, BoundaryRectangle Bounds, long StandingZ)> bases = [];
        foreach (StableTerrainBearing anchor in anchors.OrderBy(value => value.ElementId))
        {
            var region = (candidate?.Regions ?? world.Space.Regions).SingleOrDefault(value => value.Bounds.Contains(anchor.Cell));
            var cell = region?.GetCell(anchor.Cell);
            if (cell is null || cell.SourceId != anchor.SourceId || cell.Material != anchor.Material)
            {
                throw new ArgumentException("A declared stable bearing must match a retained solid terrain source and material.");
            }
            if (!cell.IsSolid)
            {
                // Stable roots retire contact only through exact completed excavation. A read-only
                // candidate may propose additional holes, without earning work or material rights.
                bool retired = candidate is not null && cell.IsExcavated
                    || allowsSettlement && cell.IsExcavated && world.Excavation is { } excavation
                    && excavation.Sites.Any(site => site.IsComplete && site.Cell == anchor.Cell
                        && site.TerrainSourceId == anchor.SourceId && site.Material.Item == anchor.Material);
                if (!retired)
                {
                    throw new ArgumentException("An inactive foundation requires its exact completed excavation owner.");
                }
                continue;
            }
            elements.Add(new(anchor.ElementId, new(0), new(0), anchor.Capacity, 10000, true, 0));
            const long cellTicks = SubunitPosition.SubunitsPerCell * StructureBearingSurface.TicksPerSubunit;
            var rectangle = new BoundaryRectangle((Int128)anchor.Cell.X * cellTicks, (Int128)anchor.Cell.Y * cellTicks,
                ((Int128)anchor.Cell.X + 1) * cellTicks, ((Int128)anchor.Cell.Y + 1) * cellTicks);
            long standingZ = checked((long)(((Int128)anchor.Cell.Z + 1) * SubunitPosition.SubunitsPerCell));
            contacts.Add(new(anchor.ElementId, rectangle, standingZ, (Int128)standingZ * StructureBearingSurface.TicksPerSubunit));
        }
        StructuralContactSurface[] terrainContacts = contacts.ToArray();
        HashSet<EntityId> usedTemplates = [];
        foreach (ConstructionProject project in world.Construction.Projects.Where(value => value.Installed is not null))
        {
            if (!byDefinition.TryGetValue(project.Definition.DefinitionId, out ConstructionBearingTemplate? template))
            {
                throw new ArgumentException("Every completed assembly requires its exact structural template.");
            }
            template.ValidateFor(project.Definition);
            usedTemplates.Add(template.DefinitionId);
            var installed = project.Installed!;
            SubunitPosition origin = candidate?.OriginFor(project) ?? (allowsSettlement
                ? SettledOrigin(project, world.ConstructionPhysical!) : project.SiteFeet);
            IReadOnlyList<MassMilligrams> memberMasses = masses.MemberMasses(project.Definition);
            for (uint index = 0; index < project.Definition.Members.Count; index++)
            {
                MillimetreBox member = project.Definition.Members[(int)index];
                EntityId id = MemberIdentity(installed.StructureId, index);
                StructuralMemberRating rating = template.Members[(int)index];
                uint condition = boundPhysical is { } retained
                    ? retained.GetMember(installed.StructureId, index).ConditionParts
                    : conditionByMember.Remove((installed.StructureId, index), out uint declared) ? declared : 10000;
                elements.Add(new(id, memberMasses[(int)index], new(0), rating.Capacity, condition, false, rating.MinimumSupportCount));
                members.Add(new(id, project.ProjectId, installed.StructureId, installed.GuardLotId, index,
                    memberMasses[(int)index], new(member.VolumeMicrolitres)));
                StructureBearingSurface surface = StructureBearingSurface.FromMember(member, origin);
                contacts.Add(new(id, surface.Bounds, surface.StandingZ, surface.TopTicks));
                if (member.MinZ == 0)
                {
                    bases.Add((id, installed.StructureId, surface.Bounds, origin.Z));
                }
            }
            foreach (StructuralMemberJoint joint in template.Joints)
            {
                EntityId upper = MemberIdentity(installed.StructureId, joint.SupportedMemberIndex);
                EntityId lower = MemberIdentity(installed.StructureId, joint.SupportingMemberIndex);
                ulong area = ConstructionBearingTemplate.JointArea(project.Definition.Members[(int)joint.SupportedMemberIndex],
                    project.Definition.Members[(int)joint.SupportingMemberIndex], joint.Kind);
                bearings.Add(new(BearingIdentity(upper, lower), upper, lower, area));
            }
        }
        if (conditionByMember.Count != 0 || usedTemplates.Count != ratings.Length)
        {
            throw new ArgumentException("A structural rating or condition cannot refer to an absent completed member.");
        }
        // Resolve base partitions after collecting every member surface. Assembly enumeration must
        // never determine whether a foreign supporting member exists. Each patch owns load once.
        Dictionary<EntityId, EntityId> structureByMember = members.ToDictionary(value => value.ElementId, value => value.StructureId);
        foreach (var footing in bases)
        {
            IEnumerable<StructuralContactSurface> eligible = candidate is null ? terrainContacts : contacts.Where(value =>
                !structureByMember.TryGetValue(value.ElementId, out EntityId owner) || owner != footing.Structure);
            var weights = StructuralContactWeights.Resolve(footing.Bounds, eligible.Where(value =>
                value.StandingZ == footing.StandingZ && (candidate is null
                    || value.PhysicalTop == (Int128)footing.StandingZ * StructureBearingSurface.TicksPerSubunit)));
            AddBearings(footing.Element, weights, bearings);
        }
        var candidateTerrain = candidate is null ? null : new TerrainVolumeQueries(candidate.Regions);
        foreach (ActorPresence actor in candidate?.Actors ?? world.Space.Actors)
        {
            ulong mass = checked((ulong)masses.ActorLoad(world, actor.ActorId));
            EntityId id = ActorIdentity(actor.ActorId);
            BodyVolume volume = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
            const long ticks = StructureBearingSurface.TicksPerSubunit;
            var footprint = new BoundaryRectangle((Int128)volume.MinimumX * ticks, (Int128)volume.MinimumY * ticks,
                (Int128)volume.MaximumX * ticks, (Int128)volume.MaximumY * ticks);
            long drop = world.Space.GroundSupport == GroundSupportMode.SteppedTerrainAndConstruction
                ? Math.Min(LocalSpatialSnapshot.MaximumOrdinaryStepHeightSubunits,
                    actor.Condition.Definition.ClearanceDimensions.HeightSubunits / 4) : 0;
            if (candidateTerrain is not null)
            {
                SpatialOccupancy footing = world.Space.GroundSupport switch
                {
                    GroundSupportMode.TerrainOnly => candidateTerrain.Query(volume, requireGroundSupport: true),
                    GroundSupportMode.SteppedTerrainAndConstruction => FootingQuery.QueryStepped(candidateTerrain,
                        candidate!.Physical.GetStructures(), volume, drop),
                    _ => FootingQuery.Query(candidateTerrain, candidate!.Physical.GetStructures(), volume)
                };
                if (footing != SpatialOccupancy.Clear)
                {
                    throw new ArgumentException("A candidate actor requires complete footing with contact at its actual feet plane.");
                }
            }
            long minimum = checked(actor.Feet.Z - drop);
            var weights = StructuralContactWeights.Resolve(footprint, contacts.Where(value =>
                value.StandingZ >= minimum && value.StandingZ <= actor.Feet.Z));
            // This load attachment is not an actor strength rating; members below decide their own capacity.
            elements.Add(new(id, new(0), new(mass), new(ulong.MaxValue), 10000, false));
            actors.Add(new(id, actor.ActorId, actor.Condition.Definition.Version, new(mass)));
            AddBearings(id, weights, bearings);
        }
        return new(world, new(elements, bearings), members, actors, anchors, ratings, candidate?.InputPin, policyPin);
    }

    public static EntityId MemberIdentity(EntityId structureId, uint memberIndex) => Identity("structure.member.v1",
        structureId.ToByteArray(), CanonicalEncodingV1.Unsigned64(memberIndex));

    public static EntityId ActorIdentity(EntityId actorId) => Identity("structure.actor-load.v1", actorId.ToByteArray());

    /// <summary>
    /// The settlement profile admits only intact rigid vertical translation onto rated terrain.
    /// Uniform placement preserves the exact authored joint faces; this does not qualify a trajectory,
    /// impact response, cross-assembly resting contact or a replacement material-strength rating.
    /// </summary>
    private static SubunitPosition SettledOrigin(ConstructionProject project, ConstructionPhysicalSnapshot physical)
    {
        EntityId structureId = project.Installed!.StructureId;
        ConstructionMemberState first = physical.GetMember(structureId, 0);
        SubunitPosition displacement = first.Displacement;
        if (first.Disposition is not ConstructionMemberDisposition.Standing and not ConstructionMemberDisposition.Resting
            || displacement.X != 0 || displacement.Y != 0 || displacement.Z > 0
            || first.Disposition == ConstructionMemberDisposition.Standing && displacement != default)
        {
            throw new ArgumentException("Settlement supports only standing or vertically descended rigid assemblies.");
        }
        for (uint index = 1; index < project.Definition.Members.Count; index++)
        {
            ConstructionMemberState member = physical.GetMember(structureId, index);
            if (member.Displacement != displacement || member.Disposition != first.Disposition)
            {
                throw new ArgumentException("Every assembly member must retain one uniform settlement pose and disposition.");
            }
        }
        return project.SiteFeet.Offset(0, 0, displacement.Z);
    }

    private static EntityId BearingIdentity(EntityId upper, EntityId lower) =>
        Identity("structure.bearing.v1", upper.ToByteArray(), lower.ToByteArray());

    private static EntityId Identity(string tag, params ReadOnlyMemory<byte>[] fields) =>
        EntityId.FromBytes(ContentHash.Compute(CanonicalEncodingV1.Frame(tag, fields)).ToByteArray().AsSpan(0, EntityId.ByteLength));

    private static void AddBearings(EntityId supported, IReadOnlyDictionary<EntityId, ulong> weights,
        ICollection<StructuralBearing> bearings)
    {
        foreach (var pair in weights)
        {
            bearings.Add(new(BearingIdentity(supported, pair.Key), supported, pair.Key, pair.Value));
        }
    }
}
