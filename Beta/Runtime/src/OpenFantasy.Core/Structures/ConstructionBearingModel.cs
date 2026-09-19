using System.Collections.ObjectModel;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
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
        IEnumerable<StableTerrainBearing> foundations, IEnumerable<ConstructionBearingTemplate> templates)
    {
        WorldHash = world.ContentHash;
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
    }

    public ContentHash WorldHash { get; }
    public ContentHash InputPin { get; }
    public StructuralLoadGraph Graph { get; }
    public ReadOnlyCollection<StructuralMemberReference> Members { get; }
    public ReadOnlyCollection<StructuralActorReference> Actors { get; }
    public ReadOnlyCollection<StableTerrainBearing> Foundations { get; }
    public ReadOnlyCollection<ConstructionBearingTemplate> Templates { get; }

    public static ConstructionBearingModel Bind(WorldSnapshot world, IEnumerable<ConstructionBearingTemplate> templates,
        IEnumerable<StableTerrainBearing> foundations, StructuralMassCatalog masses,
        IEnumerable<StructuralMemberCondition>? conditions = null)
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
        foreach (StableTerrainBearing anchor in anchors.OrderBy(value => value.ElementId))
        {
            var region = world.Space.Regions.SingleOrDefault(value => value.Bounds.Contains(anchor.Cell));
            var cell = region?.GetCell(anchor.Cell);
            if (cell is null || !cell.IsSolid || cell.SourceId != anchor.SourceId || cell.Material != anchor.Material)
            {
                throw new ArgumentException("A declared stable bearing must match a retained solid terrain source and material.");
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
            IReadOnlyList<MassMilligrams> memberMasses = masses.MemberMasses(project.Definition);
            for (uint index = 0; index < project.Definition.Members.Count; index++)
            {
                MillimetreBox member = project.Definition.Members[(int)index];
                EntityId id = MemberIdentity(installed.StructureId, index);
                StructuralMemberRating rating = template.Members[(int)index];
                uint condition = conditionByMember.Remove((installed.StructureId, index), out uint declared) ? declared : 10000;
                elements.Add(new(id, memberMasses[(int)index], new(0), rating.Capacity, condition, false, rating.MinimumSupportCount));
                members.Add(new(id, project.ProjectId, installed.StructureId, installed.GuardLotId, index,
                    memberMasses[(int)index], new(member.VolumeMicrolitres)));
                StructureBearingSurface surface = StructureBearingSurface.FromMember(member, project.SiteFeet);
                contacts.Add(new(id, surface.Bounds, surface.StandingZ, surface.TopTicks));
                if (member.MinZ == 0)
                {
                    var weights = StructuralContactWeights.Resolve(surface.Bounds,
                        terrainContacts.Where(value => value.StandingZ == project.SiteFeet.Z));
                    AddBearings(id, weights, bearings);
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
        foreach (ActorPresence actor in world.Space.Actors)
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
            long minimum = checked(actor.Feet.Z - drop);
            var weights = StructuralContactWeights.Resolve(footprint, contacts.Where(value =>
                value.StandingZ >= minimum && value.StandingZ <= actor.Feet.Z));
            // This load attachment is not an actor strength rating; members below decide their own capacity.
            elements.Add(new(id, new(0), new(mass), new(ulong.MaxValue), 10000, false));
            actors.Add(new(id, actor.ActorId, actor.Condition.Definition.Version, new(mass)));
            AddBearings(id, weights, bearings);
        }
        return new(world, new(elements, bearings), members, actors, anchors, ratings);
    }

    public static EntityId MemberIdentity(EntityId structureId, uint memberIndex) => Identity("structure.member.v1",
        structureId.ToByteArray(), CanonicalEncodingV1.Unsigned64(memberIndex));

    public static EntityId ActorIdentity(EntityId actorId) => Identity("structure.actor-load.v1", actorId.ToByteArray());

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
