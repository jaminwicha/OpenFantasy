using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Construction;

/// <summary>A pinned timed assembly whose final material transform is exclusively construction-owned.</summary>
public sealed class ConstructionDefinition
{
    public const int MaximumMembers = 32;
    public const ulong MaximumRequiredWork = 4_096;
    public const long WorkReachSubunits = 448;

    public ConstructionDefinition(EntityId definitionId, string key, string displayName,
        CraftRecipeDefinition materialTransform, uint installedOutputIndex, ConstructionToolDefinition requiredTool,
        IEnumerable<MillimetreBox> members, WorkUnits requiredWork, ExposureRate workRate,
        ConstructionStockInterpretation stockInterpretation = ConstructionStockInterpretation.AuthoredJoineryBlankStock)
    {
        ArgumentNullException.ThrowIfNull(materialTransform);
        ArgumentNullException.ThrowIfNull(requiredTool);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(workRate);
        if (string.IsNullOrWhiteSpace(key) || key.Length > 96 || key.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '/' or '_' or '-'))
            || string.IsNullOrWhiteSpace(displayName) || displayName.Length > 128 || displayName.Any(char.IsControl)
            || requiredWork.Value is 0 or > MaximumRequiredWork || workRate.Numerator != 1
            || workRate.Denominator != ExposurePacingV1.ExplorationMillisecondsPerStep
            || !Enum.IsDefined(stockInterpretation) || installedOutputIndex >= materialTransform.Outputs.Count
            || materialTransform.Outputs[(int)installedOutputIndex].Role != RecipeMaterialRole.Output
            || materialTransform.Outputs[(int)installedOutputIndex].Count != 1
            || materialTransform.Outputs.Count(term => term.Role == RecipeMaterialRole.Output) != 1)
        {
            throw new ArgumentException("A construction requires one installed output, finite work and explicit stock/tool rules.");
        }
        List<MillimetreBox> copied = [];
        foreach (MillimetreBox member in members)
        {
            ArgumentNullException.ThrowIfNull(member);
            if (copied.Count == MaximumMembers || copied.Any(member.Overlaps))
            {
                throw new ArgumentException("Physical construction members must be bounded and nonoverlapping.");
            }
            copied.Add(member);
        }
        ulong volume = copied.Aggregate(0UL, (sum, member) => checked(sum + member.VolumeMicrolitres));
        var installed = materialTransform.BindingFor(materialTransform.ItemFor(materialTransform.Outputs[(int)installedOutputIndex]));
        if (copied.Count == 0 || copied.Min(value => value.MinZ) != 0 || volume != installed.Physical.VolumePerUnit.Value)
        {
            throw new ArgumentException("Exact member volumes must account for the installed physical material.");
        }
        ValidateConnected(copied);
        _ = new SpatialStructure(definitionId, copied.Select(member => member.At(new SubunitPosition(0, 0, 0))));
        var ids = new[] { definitionId, materialTransform.DefinitionId, materialTransform.PhysicalRecipe.DefinitionId,
            requiredTool.DefinitionId, requiredTool.Item.DefinitionId };
        if (ids.Distinct().Count() != ids.Length || materialTransform.Bindings.Any(binding =>
            binding.Item.DefinitionId == definitionId || binding.Physical.DefinitionId == definitionId
            || binding.Item.DefinitionId == requiredTool.DefinitionId || binding.Physical.DefinitionId == requiredTool.DefinitionId
            || binding.Item.DefinitionId == requiredTool.Item.DefinitionId || binding.Physical.DefinitionId == requiredTool.Item.DefinitionId))
        {
            throw new ArgumentException("Construction, tool and material definition roles must not alias.");
        }
        DefinitionId = definitionId;
        Key = key;
        DisplayName = displayName;
        MaterialTransform = materialTransform;
        InstalledOutputIndex = installedOutputIndex;
        RequiredTool = requiredTool;
        Members = Array.AsReadOnly(copied.OrderBy(value => value.MinX).ThenBy(value => value.MinY)
            .ThenBy(value => value.MinZ).ThenBy(value => value.MaxX).ThenBy(value => value.MaxY).ThenBy(value => value.MaxZ).ToArray());
        RequiredWork = requiredWork;
        WorkRate = workRate;
        StockInterpretation = stockInterpretation;
        Version = ContentHash.Compute(ConstructionCodecV1.EncodeDefinition(this));
    }

    public EntityId DefinitionId { get; }
    public string Key { get; }
    public string DisplayName { get; }
    public CraftRecipeDefinition MaterialTransform { get; }
    public uint InstalledOutputIndex { get; }
    public ConstructionToolDefinition RequiredTool { get; }
    public ReadOnlyCollection<MillimetreBox> Members { get; }
    public WorkUnits RequiredWork { get; }
    public ExposureRate WorkRate { get; }
    public ConstructionStockInterpretation StockInterpretation { get; }
    public ContentHash Version { get; }

    public SpatialStructure CreateStructure(EntityId structureId, SubunitPosition origin, bool supportsActors = false) =>
        new(structureId, Members.Select(member => member.At(origin)), supportsActors
            ? Members.Select(member => StructureBearingSurface.FromMember(member, origin)) : []);

    private static void ValidateConnected(IReadOnlyList<MillimetreBox> members)
    {
        // Actual positive-area physical faces establish this admitted assembly's support graph.
        // Rounded collision envelopes and corner/edge contact cannot provide a missing joint.
        HashSet<int> reached = [Enumerable.Range(0, members.Count).First(index => members[index].MinZ == 0)];
        Queue<int> pending = new(reached);
        while (pending.TryDequeue(out int current))
        {
            for (int index = 0; index < members.Count; index++)
            {
                if (!reached.Contains(index) && members[current].SharesFace(members[index]))
                {
                    reached.Add(index);
                    pending.Enqueue(index);
                }
            }
        }
        if (reached.Count != members.Count)
        {
            throw new ArgumentException("Every member must be joined to one connected ground-supported assembly.");
        }
    }
}
