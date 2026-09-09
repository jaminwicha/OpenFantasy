using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Actors;

/// <summary>
/// Immutable condition metadata for one form instance. Missing ancestors suppress
/// descendant functions; scars do not. No lifecycle or custody transition is performed.
/// </summary>
public sealed class BodyConditionSnapshot
{
    public const int MaximumInjuryCount = 256;
    private readonly Dictionary<BodyFunction, ReadOnlyCollection<BodySlotKey>> _functionalSlots;

    public BodyConditionSnapshot(EntityId formInstanceId, BodyDefinition definition, IEnumerable<BodyInjury> injuries)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(injuries);
        var retained = new SortedDictionary<EntityId, BodyInjury>();
        foreach (BodyInjury injury in injuries)
        {
            ArgumentNullException.ThrowIfNull(injury, nameof(injuries));
            if (retained.Count == MaximumInjuryCount || !retained.TryAdd(injury.InjuryId, injury))
            {
                throw new ArgumentException("Condition records require bounded unique injury identities.", nameof(injuries));
            }

            BodyPartDefinition part = definition.GetPart(injury.Slot);
            if (injury.LostFunction is BodyFunction function && !part.Functions.Contains(function))
            {
                throw new ArgumentException("The injured slot does not provide the named function.", nameof(injuries));
            }

            if (definition.Nature == BodyNature.Incorporeal && injury.Kind != BodyInjuryKind.FunctionLoss)
            {
                throw new ArgumentException("Physical scars and missing parts require a corporeal body definition.", nameof(injuries));
            }
        }

        FormInstanceId = formInstanceId;
        Definition = definition;
        Injuries = Array.AsReadOnly(retained.Values.ToArray());
        _functionalSlots = [];
        foreach (BodyFunction function in Enum.GetValues<BodyFunction>())
        {
            BodySlotKey[] active = definition.Parts.Where(part => part.Functions.Contains(function)
                && !Injuries.Any(injury => injury.Kind == BodyInjuryKind.MissingPart
                    && definition.IsWithinSubtree(part.Slot, injury.Slot))
                && !Injuries.Any(injury => injury.Slot == part.Slot && injury.LostFunction == function))
                .Select(part => part.Slot).ToArray();
            _functionalSlots.Add(function, Array.AsReadOnly(active));
        }
    }

    public EntityId FormInstanceId { get; }
    public BodyDefinition Definition { get; }
    public ReadOnlyCollection<BodyInjury> Injuries { get; }

    public ReadOnlyCollection<BodySlotKey> FunctionalSlots(BodyFunction function) =>
        _functionalSlots.TryGetValue(function, out ReadOnlyCollection<BodySlotKey>? slots)
            ? slots
            : throw new ArgumentOutOfRangeException(nameof(function));

    public bool SupportsFunction(BodyFunction function, int minimumParts = 1)
    {
        if (minimumParts is < 1 or > BodyDefinition.MaximumPartCount)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumParts));
        }

        return FunctionalSlots(function).Count >= minimumParts;
    }

    public bool CanLocomote(LocomotionMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        LocomotionRequirement? requirement = Definition.Locomotion.SingleOrDefault(item => item.Mode == mode);
        return requirement is not null && SupportsFunction(requirement.SupportingFunction, requirement.MinimumFunctionalParts);
    }

    public ContentHash ComputeContentHash() => ContentHash.Compute(BodyContractEncodingV1.EncodeCondition(this));
}
