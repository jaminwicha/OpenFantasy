using System.Collections.ObjectModel;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Complete authored load interpretation for one terrain scope. Binding is an assessment;
/// this asset alone does not install a world rule or publish structural consequences.
/// </summary>
public sealed class ConstructionLoadPolicy
{
    public const int MaximumTemplates = ConstructionSnapshot.MaximumDefinitions;
    public const int MaximumFoundations = StructuralLoadGraph.MaximumElements;

    public ConstructionLoadPolicy(TerrainScope scope, IEnumerable<ConstructionBearingTemplate> templates,
        IEnumerable<StableTerrainBearing> foundations, StructuralMassCatalog masses)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(foundations);
        ArgumentNullException.ThrowIfNull(masses);
        ConstructionBearingTemplate[] ratings = templates.Take(MaximumTemplates + 1).ToArray();
        StableTerrainBearing[] anchors = foundations.Take(MaximumFoundations + 1).ToArray();
        if (ratings.Length is 0 or > MaximumTemplates || ratings.Any(value => value is null)
            || anchors.Length is 0 or > MaximumFoundations || anchors.Any(value => value is null))
        {
            throw new ArgumentException("Load policy catalogs must be nonempty, nonnull and bounded.");
        }
        if (ratings.Select(value => value.DefinitionId).Distinct().Count() != ratings.Length
            || anchors.Select(value => value.ElementId).Distinct().Count() != anchors.Length
            || anchors.Select(value => value.SourceId).Distinct().Count() != anchors.Length
            || anchors.Select(value => value.Cell).Distinct().Count() != anchors.Length)
        {
            throw new ArgumentException("Load policy templates, foundation identities, terrain sources and cells must be unique.");
        }
        ValidateIdentities(scope, ratings, anchors, masses);
        foreach (ConstructionBearingTemplate template in ratings)
        {
            ValidateTopology(template);
        }
        foreach (StableTerrainBearing anchor in anchors)
        {
            // Binding projects a whole-cell top into signed local spatial coordinates.
            // Reject overflow at asset admission, before any graph can be constructed.
            _ = checked(anchor.Cell.X * SubunitPosition.SubunitsPerCell);
            _ = checked((anchor.Cell.X + 1) * SubunitPosition.SubunitsPerCell);
            _ = checked(anchor.Cell.Y * SubunitPosition.SubunitsPerCell);
            _ = checked((anchor.Cell.Y + 1) * SubunitPosition.SubunitsPerCell);
            _ = checked(anchor.Cell.Z * SubunitPosition.SubunitsPerCell);
            _ = checked((anchor.Cell.Z + 1) * SubunitPosition.SubunitsPerCell);
        }
        Scope = scope;
        Templates = Array.AsReadOnly(ratings.OrderBy(value => value.DefinitionId).ToArray());
        Foundations = Array.AsReadOnly(anchors.OrderBy(value => value.ElementId).ToArray());
        Masses = masses;
        ContentHash = ContentHash.Compute(ConstructionLoadPolicyCodecV1.Encode(this));
    }

    public TerrainScope Scope { get; }
    public ReadOnlyCollection<ConstructionBearingTemplate> Templates { get; }
    public ReadOnlyCollection<StableTerrainBearing> Foundations { get; }
    public StructuralMassCatalog Masses { get; }
    public ContentHash ContentHash { get; }

    public ConstructionBearingModel Bind(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Space.Scope != Scope || world.Construction is not { } construction
            || construction.Definitions.Count != Templates.Count)
        {
            throw new ArgumentException("Load policy binding requires its exact terrain scope and complete construction catalog.", nameof(world));
        }
        Dictionary<EntityId, ConstructionBearingTemplate> byDefinition = Templates.ToDictionary(value => value.DefinitionId);
        foreach (ConstructionDefinition definition in construction.Definitions)
        {
            if (!byDefinition.TryGetValue(definition.DefinitionId, out ConstructionBearingTemplate? template))
            {
                throw new ArgumentException("Every admitted construction definition requires its policy template.", nameof(world));
            }
            template.ValidateFor(definition);
        }
        HashSet<EntityId> installed = construction.Projects.Where(value => value.Installed is not null)
            .Select(value => value.Definition.DefinitionId).ToHashSet();
        return ConstructionBearingModel.Bind(world, Templates.Where(value => installed.Contains(value.DefinitionId)),
            Foundations, Masses);
    }

    private static void ValidateTopology(ConstructionBearingTemplate template)
    {
        // All admitted definitions need a load order, including assemblies not yet built.
        // Missing supports or insufficient strength remain load-resolution outcomes.
        int count = template.Members.Count;
        int[] incoming = new int[count];
        List<int>[] outgoing = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        foreach (StructuralMemberJoint joint in template.Joints)
        {
            int supported = checked((int)joint.SupportedMemberIndex);
            int supporting = checked((int)joint.SupportingMemberIndex);
            outgoing[supported].Add(supporting);
            incoming[supporting]++;
        }
        Queue<int> ready = new(Enumerable.Range(0, count).Where(index => incoming[index] == 0));
        int visited = 0;
        while (ready.TryDequeue(out int current))
        {
            visited++;
            foreach (int supporting in outgoing[current])
            {
                if (--incoming[supporting] == 0)
                {
                    ready.Enqueue(supporting);
                }
            }
        }
        if (visited != count)
        {
            throw new ArgumentException("Every admitted structural template requires an acyclic load order.", nameof(template));
        }
    }

    private static void ValidateIdentities(TerrainScope scope, IEnumerable<ConstructionBearingTemplate> templates,
        IEnumerable<StableTerrainBearing> foundations, StructuralMassCatalog masses)
    {
        Dictionary<EntityId, string> roles = [];
        Dictionary<EntityId, ContentHash> itemPins = [];
        Add(scope.WorldId, "world");
        Add(scope.BranchId, "branch");
        Add(scope.RealmId, "realm");
        foreach (ConstructionBearingTemplate template in templates) { Add(template.DefinitionId, "construction definition"); }
        foreach (StructuralBodyMass body in masses.Bodies) { Add(body.DefinitionId, "body definition"); }
        foreach (StructuralItemMass item in masses.SupplementalItems) { Item(item.Item.DefinitionId, item.Item.Version); }
        foreach (StableTerrainBearing anchor in foundations)
        {
            Add(anchor.ElementId, "foundation");
            Item(anchor.Material.DefinitionId, anchor.Material.Version);
            Add(anchor.SourceId, "terrain source");
        }

        void Add(EntityId identity, string role)
        {
            if (!roles.TryAdd(identity, role))
            {
                throw new ArgumentException("Load policy identities cannot alias scope, definitions, sources or foundations.");
            }
        }

        void Item(EntityId identity, ContentHash version)
        {
            if (itemPins.TryGetValue(identity, out ContentHash? previous))
            {
                if (previous != version) { throw new ArgumentException("A material identity requires one exact policy pin."); }
            }
            else
            {
                Add(identity, "material definition");
                itemPins.Add(identity, version);
            }
        }
    }
}
