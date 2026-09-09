using OpenFantasy.Core.Actors;

namespace OpenFantasy.Core.Tests.Actors;

public sealed class BodyDefinitionTests
{
    [Fact]
    public void AuthoredFamilyDefinitionsMatchIndependentCanonicalOracle()
    {
        foreach (var expected in ActorFixture.Read().GetProperty("definitions").EnumerateArray())
        {
            BodyDefinition definition = ActorFixture.Definition(expected);
            Assert.Equal(expected.GetProperty("canonicalHex").GetString(),
                Convert.ToHexString(BodyContractEncodingV1.EncodeDefinition(definition)).ToLowerInvariant());
            Assert.Equal(expected.GetProperty("versionSha256").GetString(), definition.Version.Value);
            var reversed = new BodyDefinition(definition.DefinitionId, definition.Nature, definition.ClearanceDimensions,
                definition.Parts.Reverse(), definition.Locomotion.Reverse());
            Assert.Equal(definition.Version, reversed.Version);
        }
    }

    [Fact]
    public void TopologyRejectsMissingParentsCyclesMultipleRootsAndDuplicateSlots()
    {
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([]));
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([ActorFixture.Part("a"), ActorFixture.Part("b")]));
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([ActorFixture.Part("a"), ActorFixture.Part("a", "a")]));
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([ActorFixture.Part("root"), ActorFixture.Part("child", "missing")]));
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([ActorFixture.Part("root"), ActorFixture.Part("a", "b"), ActorFixture.Part("b", "a")]));
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([ActorFixture.Part("root"), ActorFixture.Part("self", "self")]));
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([ActorFixture.Part("a", "b"), ActorFixture.Part("b", "a")]));
    }

    [Fact]
    public void PositiveDimensionsUseFullSignedRangeAndCanonicalSlotKeysRejectAmbiguity()
    {
        Assert.Equal(long.MaxValue, new BodyDimensions(long.MaxValue, 1, 1).WidthSubunits);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BodyDimensions(0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BodyDimensions(1, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BodyDimensions(1, 1, 0));
        foreach (string key in new[] { "", "LeftHand", "1hand", "left/hand", "hánd", new string('a', 65) })
        {
            Assert.Throws<ArgumentException>(() => new BodySlotKey(key));
        }
        Assert.Equal(new BodySlotKey("left-hand"), new BodySlotKey("left-hand"));
    }

    [Fact]
    public void FunctionsAndLocomotionMustBeExplicitAndCompatibleWithNature()
    {
        Assert.Throws<ArgumentException>(() => ActorFixture.Part("root", null, BodyFunction.Sight, BodyFunction.Sight));
        Assert.Throws<ArgumentException>(() => ActorFixture.Part("root", null, (BodyFunction)0));
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([ActorFixture.Part("root")], [new LocomotionRequirement(LocomotionMode.Flight, 1)]));
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([ActorFixture.Part("root", null, BodyFunction.GroundContact)],
            [new LocomotionRequirement(LocomotionMode.Ground, 1), new LocomotionRequirement(LocomotionMode.Ground, 1)]));
        Assert.Throws<ArgumentException>(() => ActorFixture.Body([ActorFixture.Part("root", null, BodyFunction.SpectralMotion)]));
        Assert.Throws<ArgumentException>(() => new BodyDefinition(default, BodyNature.Incorporeal, new BodyDimensions(1, 1, 1),
            [ActorFixture.Part("presence", null, BodyFunction.Manipulation)], []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocomotionRequirement((LocomotionMode)999, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocomotionRequirement(LocomotionMode.Ground, 0));
    }

    [Fact]
    public void DefinitionCopiesCollectionsAndBindsClearancePartsAndRequirementsIntoVersion()
    {
        BodyFunction[] functions = [BodyFunction.GroundContact];
        var root = new BodyPartDefinition(new BodySlotKey("root"), null, new BodyDimensions(16, 16, 16), functions);
        BodyPartDefinition[] parts = [root];
        LocomotionRequirement[] requirements = [new(LocomotionMode.Ground, 1)];
        BodyDefinition definition = ActorFixture.Body(parts, requirements);
        functions[0] = BodyFunction.Manipulation;
        parts[0] = ActorFixture.Part("changed");
        requirements[0] = new LocomotionRequirement(LocomotionMode.Flight, 2);
        Assert.Equal(BodyFunction.GroundContact, Assert.Single(definition.Parts[0].Functions));
        Assert.Equal(LocomotionMode.Ground, Assert.Single(definition.Locomotion).Mode);
        Assert.Throws<NotSupportedException>(() => ((IList<BodyPartDefinition>)definition.Parts).Clear());
        Assert.NotEqual(definition.Version, new BodyDefinition(definition.DefinitionId, definition.Nature,
            new BodyDimensions(257, 256, 512), definition.Parts, definition.Locomotion).Version);
        Assert.NotEqual(definition.Version, new BodyDefinition(definition.DefinitionId, definition.Nature,
            definition.ClearanceDimensions, definition.Parts, []).Version);
        byte[] encoded = BodyContractEncodingV1.EncodeDefinition(definition);
        encoded.AsSpan().Clear();
        Assert.NotEqual(encoded, BodyContractEncodingV1.EncodeDefinition(definition));
    }

    [Fact]
    public void AnatomyBoundStopsEnumerationWithoutPretendingToBeWholeSpeciesCatalogLimit()
    {
        int yielded = 0;
        IEnumerable<BodyPartDefinition> Parts()
        {
            yield return ActorFixture.Part("root");
            yielded++;
            while (true)
            {
                yielded++;
                yield return ActorFixture.Part("part" + yielded, "root");
            }
        }

        Assert.Throws<ArgumentException>(() => ActorFixture.Body(Parts()));
        Assert.Equal(BodyDefinition.MaximumPartCount + 1, yielded);
        BodyDefinition maximum = ActorFixture.Body(Enumerable.Range(0, 64).Select(index =>
            ActorFixture.Part("part" + index, index == 0 ? null : "part" + (index - 1))));
        Assert.Equal(64, maximum.Parts.Count);
    }
}
