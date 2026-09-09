using OpenFantasy.Core.Actors;

namespace OpenFantasy.Core.Tests.Actors;

public sealed class BodyConditionTests
{
    [Fact]
    public void ConditionBytesFunctionsAndLocomotionMatchIndependentOracle()
    {
        foreach (var expected in ActorFixture.Read().GetProperty("conditions").EnumerateArray())
        {
            BodyConditionSnapshot condition = ActorFixture.Condition(expected);
            Assert.Equal(expected.GetProperty("canonicalHex").GetString(),
                Convert.ToHexString(BodyContractEncodingV1.EncodeCondition(condition)).ToLowerInvariant());
            Assert.Equal(expected.GetProperty("sha256").GetString(), condition.ComputeContentHash().Value);
            foreach (BodyFunction function in Enum.GetValues<BodyFunction>())
            {
                Assert.Equal(expected.GetProperty("functionalSlots").GetProperty(((int)function).ToString())
                    .EnumerateArray().Select(value => value.GetString()), condition.FunctionalSlots(function).Select(slot => slot.Value));
            }
            Assert.Equal(expected.GetProperty("locomotion").EnumerateArray().Select(value => (LocomotionMode)value.GetInt32()),
                Enum.GetValues<LocomotionMode>().Where(condition.CanLocomote));
        }
    }

    [Fact]
    public void MissingArmSuppressesChildGripAndOverlappingLossRetainsBothCauses()
    {
        BodyDefinition body = ActorFixture.Definition("humanoid-candidate");
        BodyInjury[] injuries = [Injury(1, "left-arm", BodyInjuryKind.MissingPart),
            Injury(2, "left-hand", BodyInjuryKind.FunctionLoss, BodyFunction.Manipulation),
            Injury(3, "left-hand", BodyInjuryKind.Scar)];
        var condition = new BodyConditionSnapshot(new EntityId(0, 101), body, injuries);
        Assert.Equal("right-hand", Assert.Single(condition.FunctionalSlots(BodyFunction.Manipulation)).Value);
        Assert.True(condition.SupportsFunction(BodyFunction.Manipulation));
        Assert.False(condition.SupportsFunction(BodyFunction.Manipulation, 2));
        Assert.True(condition.CanLocomote(LocomotionMode.Ground));
        Assert.Equal(3, condition.Injuries.Count);
        Assert.Equal(condition.ComputeContentHash(), new BodyConditionSnapshot(condition.FormInstanceId, body, injuries.Reverse()).ComputeContentHash());
        Assert.Throws<NotSupportedException>(() => ((IList<BodyInjury>)condition.Injuries).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<BodySlotKey>)condition.FunctionalSlots(BodyFunction.Manipulation)).Clear());
    }

    [Fact]
    public void EyeLossChangesAvailableSensesAndRootLossSuppressesAllFunctions()
    {
        BodyDefinition body = ActorFixture.Definition("humanoid-candidate");
        var oneEye = new BodyConditionSnapshot(default, body, [Injury(1, "left-eye", BodyInjuryKind.MissingPart)]);
        Assert.True(oneEye.SupportsFunction(BodyFunction.Sight));
        Assert.False(oneEye.SupportsFunction(BodyFunction.Sight, 2));
        var blind = new BodyConditionSnapshot(default, body,
            [Injury(1, "left-eye", BodyInjuryKind.MissingPart), Injury(2, "right-eye", BodyInjuryKind.FunctionLoss, BodyFunction.Sight)]);
        Assert.False(blind.SupportsFunction(BodyFunction.Sight));
        var absentRoot = new BodyConditionSnapshot(default, body, [Injury(3, "torso", BodyInjuryKind.MissingPart)]);
        Assert.All(Enum.GetValues<BodyFunction>(), function => Assert.Empty(absentRoot.FunctionalSlots(function)));
        Assert.False(absentRoot.CanLocomote(LocomotionMode.Ground));
    }

    [Fact]
    public void SerpentAndMountExamplesRequireTheirActualAnatomyWithoutHumanGripOrLegAssumptions()
    {
        var serpent = new BodyConditionSnapshot(default, ActorFixture.Definition("reptilian-serpent-candidate"), []);
        Assert.True(serpent.CanLocomote(LocomotionMode.Slithering));
        Assert.True(serpent.CanLocomote(LocomotionMode.Swimming));
        Assert.False(serpent.CanLocomote(LocomotionMode.Ground));
        Assert.False(serpent.SupportsFunction(BodyFunction.Manipulation));
        Assert.Empty(serpent.FunctionalSlots(BodyFunction.GroundContact));

        BodyDefinition quadruped = ActorFixture.Definition("quadruped-mount-anatomy-candidate");
        var oneLoss = new BodyConditionSnapshot(default, quadruped, [Injury(1, "fore-left", BodyInjuryKind.MissingPart)]);
        Assert.True(oneLoss.CanLocomote(LocomotionMode.Ground));
        var twoLosses = new BodyConditionSnapshot(default, quadruped,
            [Injury(1, "fore-left", BodyInjuryKind.MissingPart), Injury(2, "hind-left", BodyInjuryKind.MissingPart)]);
        Assert.False(twoLosses.CanLocomote(LocomotionMode.Ground));
        Assert.False(twoLosses.SupportsFunction(BodyFunction.Manipulation));
    }

    [Fact]
    public void InvalidInjurySlotsFunctionsAndPhysicalSpiritDamageReject()
    {
        BodyDefinition body = ActorFixture.Definition("humanoid-candidate");
        Assert.Throws<ArgumentException>(() => new BodyConditionSnapshot(default, body, [Injury(1, "missing", BodyInjuryKind.Scar)]));
        Assert.Throws<ArgumentException>(() => new BodyConditionSnapshot(default, body,
            [Injury(1, "head", BodyInjuryKind.FunctionLoss, BodyFunction.Manipulation)]));
        BodyInjury first = Injury(1, "head", BodyInjuryKind.Scar);
        Assert.Throws<ArgumentException>(() => new BodyConditionSnapshot(default, body, [first, first]));
        Assert.Throws<ArgumentException>(() => Injury(1, "head", BodyInjuryKind.Scar, BodyFunction.Sight));
        Assert.Throws<ArgumentException>(() => Injury(1, "head", BodyInjuryKind.FunctionLoss));
        Assert.Throws<ArgumentOutOfRangeException>(() => Injury(1, "head", (BodyInjuryKind)0));
        BodyDefinition spirit = ActorFixture.Definition("incorporeal-presence-candidate");
        Assert.Throws<ArgumentException>(() => new BodyConditionSnapshot(default, spirit, [Injury(1, "presence", BodyInjuryKind.MissingPart)]));
        Assert.Throws<ArgumentException>(() => new BodyConditionSnapshot(default, spirit, [Injury(1, "presence", BodyInjuryKind.Scar)]));
    }

    [Fact]
    public void InjuryInputIsCopiedAndBoundedWithoutDiscardingCausalRecords()
    {
        BodyDefinition body = ActorFixture.Definition("humanoid-candidate");
        BodyInjury[] source = [Injury(1, "head", BodyInjuryKind.Scar)];
        var condition = new BodyConditionSnapshot(default, body, source);
        source[0] = Injury(2, "head", BodyInjuryKind.MissingPart);
        Assert.Equal(BodyInjuryKind.Scar, Assert.Single(condition.Injuries).Kind);
        Assert.Equal(2, condition.FunctionalSlots(BodyFunction.Sight).Count);
        int yielded = 0;
        IEnumerable<BodyInjury> Endless()
        {
            while (true)
            {
                yielded++;
                yield return Injury((ulong)yielded, "head", BodyInjuryKind.Scar);
            }
        }
        Assert.Throws<ArgumentException>(() => new BodyConditionSnapshot(default, body, Endless()));
        Assert.Equal(BodyConditionSnapshot.MaximumInjuryCount + 1, yielded);
        Assert.Equal(256, new BodyConditionSnapshot(default, body, Enumerable.Range(1, 256)
            .Select(index => Injury((ulong)index, "head", BodyInjuryKind.Scar))).Injuries.Count);
    }

    private static BodyInjury Injury(ulong id, string slot, BodyInjuryKind kind, BodyFunction? lost = null) =>
        new(new EntityId(0, id), new EntityId(0, 900 + id), new BodySlotKey(slot), kind, lost);
}
