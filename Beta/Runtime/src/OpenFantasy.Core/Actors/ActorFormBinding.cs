using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Actors;

public enum ActorFormKind
{
    Corporeal = 1,
    IndependentSpirit = 2,
    TetheredProjection = 3
}

/// <summary>
/// Candidate identity/reference metadata for one form. Factories grant no control,
/// possession or inventory and do not prove uniqueness in a world or execute a transition.
/// </summary>
public sealed record ActorFormBinding
{
    private ActorFormBinding(EntityId actorId, EntityId formInstanceId, BodyDefinition definition,
        ActorFormKind kind, EntityId? physicalBodyId, EntityId? tetherAnchorId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (actorId == formInstanceId || actorId == physicalBodyId || actorId == tetherAnchorId
            || formInstanceId == physicalBodyId || formInstanceId == tetherAnchorId)
        {
            throw new ArgumentException("Actor, form, body and tether roles require distinct instance identities.");
        }

        if ((kind == ActorFormKind.Corporeal) != (definition.Nature == BodyNature.Corporeal))
        {
            throw new ArgumentException("The body definition's nature is incompatible with the selected form.", nameof(definition));
        }

        ActorId = actorId;
        FormInstanceId = formInstanceId;
        DefinitionId = definition.DefinitionId;
        DefinitionVersion = definition.Version;
        Kind = kind;
        PhysicalBodyId = physicalBodyId;
        TetherAnchorId = tetherAnchorId;
    }

    public EntityId ActorId { get; }
    public EntityId FormInstanceId { get; }
    public EntityId DefinitionId { get; }
    public ContentHash DefinitionVersion { get; }
    public ActorFormKind Kind { get; }
    public EntityId? PhysicalBodyId { get; }
    public EntityId? TetherAnchorId { get; }

    public static ActorFormBinding Corporeal(EntityId actorId, EntityId formInstanceId,
        EntityId physicalBodyId, BodyDefinition definition) =>
        new(actorId, formInstanceId, definition, ActorFormKind.Corporeal, physicalBodyId, null);

    public static ActorFormBinding IndependentSpirit(EntityId actorId, EntityId formInstanceId, BodyDefinition definition) =>
        new(actorId, formInstanceId, definition, ActorFormKind.IndependentSpirit, null, null);

    public static ActorFormBinding TetheredProjection(EntityId actorId, EntityId formInstanceId,
        EntityId tetherAnchorId, BodyDefinition definition) =>
        new(actorId, formInstanceId, definition, ActorFormKind.TetheredProjection, null, tetherAnchorId);

    /// <summary>Checks local pin/instance consistency only, never admission or current authority.</summary>
    public void ValidateCondition(BodyConditionSnapshot condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (condition.FormInstanceId != FormInstanceId || condition.Definition.DefinitionId != DefinitionId
            || condition.Definition.Version != DefinitionVersion)
        {
            throw new ArgumentException("The condition belongs to a different form instance or definition version.", nameof(condition));
        }
    }

    public ContentHash ComputeContentHash() => ContentHash.Compute(BodyContractEncodingV1.EncodeBinding(this));
}
