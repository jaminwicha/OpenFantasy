using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Actors;

/// <summary>A bounded, connected anatomy tree and its explicit locomotion requirements.</summary>
public sealed class BodyDefinition
{
    public const int MaximumPartCount = 64;
    private readonly Dictionary<BodySlotKey, BodyPartDefinition> _partsBySlot;

    public BodyDefinition(EntityId definitionId, BodyNature nature, BodyDimensions clearanceDimensions, IEnumerable<BodyPartDefinition> parts,
        IEnumerable<LocomotionRequirement> locomotion)
    {
        if (!Enum.IsDefined(nature))
        {
            throw new ArgumentOutOfRangeException(nameof(nature));
        }

        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(locomotion);
        ArgumentNullException.ThrowIfNull(clearanceDimensions);
        _partsBySlot = [];
        foreach (BodyPartDefinition part in parts)
        {
            ArgumentNullException.ThrowIfNull(part, nameof(parts));
            if (_partsBySlot.Count == MaximumPartCount || !_partsBySlot.TryAdd(part.Slot, part))
            {
                throw new ArgumentException("A body requires at most 64 uniquely named parts.", nameof(parts));
            }

            foreach (BodyFunction function in part.Functions)
            {
                bool spectral = function is BodyFunction.SpectralPerception or BodyFunction.SpectralMotion;
                if (spectral != (nature == BodyNature.Incorporeal))
                {
                    throw new ArgumentException("The declared function is incompatible with the body's nature.", nameof(parts));
                }
            }
        }

        ValidateTree();
        DefinitionId = definitionId;
        Nature = nature;
        ClearanceDimensions = clearanceDimensions;
        Parts = Array.AsReadOnly(_partsBySlot.Values.OrderBy(part => part.Slot.Value, StringComparer.Ordinal).ToArray());
        var requirements = new SortedDictionary<LocomotionMode, LocomotionRequirement>();
        foreach (LocomotionRequirement requirement in locomotion)
        {
            ArgumentNullException.ThrowIfNull(requirement, nameof(locomotion));
            if (!requirements.TryAdd(requirement.Mode, requirement))
            {
                throw new ArgumentException("A locomotion mode can be declared only once.", nameof(locomotion));
            }

            int availableParts = Parts.Count(part => part.Functions.Contains(requirement.SupportingFunction));
            if (availableParts < requirement.MinimumFunctionalParts)
            {
                throw new ArgumentException("The intact anatomy cannot satisfy its locomotion requirement.", nameof(locomotion));
            }
        }

        Locomotion = Array.AsReadOnly(requirements.Values.ToArray());
        Version = ContentHash.Compute(BodyContractEncodingV1.EncodeDefinition(this));
    }

    public EntityId DefinitionId { get; }
    public BodyNature Nature { get; }
    public BodyDimensions ClearanceDimensions { get; }
    public ReadOnlyCollection<BodyPartDefinition> Parts { get; }
    public ReadOnlyCollection<LocomotionRequirement> Locomotion { get; }
    public ContentHash Version { get; }

    public BodyPartDefinition GetPart(BodySlotKey slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return _partsBySlot.TryGetValue(slot, out BodyPartDefinition? part)
            ? part
            : throw new ArgumentException("The body definition has no such part slot.", nameof(slot));
    }

    internal bool IsWithinSubtree(BodySlotKey slot, BodySlotKey subtreeRoot)
    {
        BodyPartDefinition? part = GetPart(slot);
        while (part is not null)
        {
            if (part.Slot == subtreeRoot)
            {
                return true;
            }

            part = part.ParentSlot is null ? null : GetPart(part.ParentSlot);
        }

        return false;
    }

    private void ValidateTree()
    {
        if (_partsBySlot.Count == 0 || _partsBySlot.Values.Count(part => part.ParentSlot is null) != 1)
        {
            throw new ArgumentException("A body requires exactly one root part.");
        }

        foreach (BodyPartDefinition part in _partsBySlot.Values)
        {
            var path = new HashSet<BodySlotKey>();
            BodyPartDefinition current = part;
            while (true)
            {
                if (!path.Add(current.Slot))
                {
                    throw new ArgumentException("The body topology contains a cycle.");
                }

                if (current.ParentSlot is null)
                {
                    break;
                }

                if (!_partsBySlot.TryGetValue(current.ParentSlot, out BodyPartDefinition? parent))
                {
                    throw new ArgumentException("A body part references a missing parent slot.");
                }

                current = parent;
            }
        }
    }
}
