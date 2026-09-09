using OpenFantasy.Core.Actors;

namespace OpenFantasy.Core.Tests.Actors;

public sealed class ActorFormBindingTests
{
    [Fact]
    public void IndependentReferenceBindingsMatchOracleAndRetainOneActorIdentity()
    {
        var actorIds = new HashSet<EntityId>();
        foreach (var expected in ActorFixture.Read().GetProperty("bindings").EnumerateArray())
        {
            BodyDefinition definition = ActorFixture.Definition(EntityId.Parse(expected.GetProperty("definitionId").GetString()));
            EntityId actor = EntityId.Parse(expected.GetProperty("actorId").GetString());
            EntityId form = EntityId.Parse(expected.GetProperty("formInstanceId").GetString());
            ActorFormBinding binding = (ActorFormKind)expected.GetProperty("kind").GetInt32() switch
            {
                ActorFormKind.Corporeal => ActorFormBinding.Corporeal(actor, form,
                    EntityId.Parse(expected.GetProperty("physicalBodyId").GetString()), definition),
                ActorFormKind.IndependentSpirit => ActorFormBinding.IndependentSpirit(actor, form, definition),
                ActorFormKind.TetheredProjection => ActorFormBinding.TetheredProjection(actor, form,
                    EntityId.Parse(expected.GetProperty("tetherAnchorId").GetString()), definition),
                _ => throw new InvalidOperationException("The fixture has an unsupported form.")
            };
            actorIds.Add(binding.ActorId);
            Assert.Equal(expected.GetProperty("canonicalHex").GetString(),
                Convert.ToHexString(BodyContractEncodingV1.EncodeBinding(binding)).ToLowerInvariant());
            Assert.Equal(expected.GetProperty("sha256").GetString(), binding.ComputeContentHash().Value);
            binding.ValidateCondition(new BodyConditionSnapshot(form, definition, []));
            if (binding.Kind != ActorFormKind.Corporeal)
            {
                Assert.Null(binding.PhysicalBodyId);
            }
        }
        Assert.Single(actorIds);
    }

    [Fact]
    public void InstanceRolesCannotAliasOrSelectIncompatibleDefinitions()
    {
        BodyDefinition body = ActorFixture.Definition("humanoid-candidate");
        BodyDefinition spirit = ActorFixture.Definition("incorporeal-presence-candidate");
        EntityId actor = new(0, 1);
        EntityId form = new(0, 2);
        EntityId bodyId = new(0, 3);
        Assert.Throws<ArgumentException>(() => ActorFormBinding.Corporeal(actor, actor, bodyId, body));
        Assert.Throws<ArgumentException>(() => ActorFormBinding.Corporeal(actor, form, actor, body));
        Assert.Throws<ArgumentException>(() => ActorFormBinding.Corporeal(actor, form, form, body));
        Assert.Throws<ArgumentException>(() => ActorFormBinding.IndependentSpirit(actor, actor, spirit));
        Assert.Throws<ArgumentException>(() => ActorFormBinding.TetheredProjection(actor, form, actor, spirit));
        Assert.Throws<ArgumentException>(() => ActorFormBinding.TetheredProjection(actor, form, form, spirit));
        Assert.Throws<ArgumentException>(() => ActorFormBinding.Corporeal(actor, form, bodyId, spirit));
        Assert.Throws<ArgumentException>(() => ActorFormBinding.IndependentSpirit(actor, form, body));
        Assert.Throws<ArgumentException>(() => ActorFormBinding.TetheredProjection(actor, form, bodyId, body));
    }

    [Fact]
    public void ConditionBindingRejectsDifferentFormOrVersion()
    {
        BodyDefinition definition = ActorFixture.Definition("humanoid-candidate");
        EntityId form = new(0, 101);
        ActorFormBinding binding = ActorFormBinding.Corporeal(new EntityId(0, 401), form, new EntityId(0, 501), definition);
        Assert.Throws<ArgumentException>(() => binding.ValidateCondition(new BodyConditionSnapshot(new EntityId(0, 102), definition, [])));
        var changed = new BodyDefinition(definition.DefinitionId, definition.Nature,
            new BodyDimensions(181, 140, 464), definition.Parts, definition.Locomotion);
        Assert.Throws<ArgumentException>(() => binding.ValidateCondition(new BodyConditionSnapshot(form, changed, [])));
        Assert.Equal(definition.Version, binding.DefinitionVersion);
        Assert.Throws<ArgumentNullException>(() => binding.ValidateCondition(null!));
    }
}
