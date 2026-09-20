using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConstructionDescentAssessmentTests
{
    [Fact]
    public void Funded_group_assessment_retains_exact_mass_impact_and_geometry_through_every_saved_prefix_without_publication()
    {
        var fixture = new CoupledSettlementWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot basis = fixture.PrepareBeforeExcavation(files.Path);
        byte[] root = WorldSnapshotCodec.Encode(basis);
        byte[] database = File.ReadAllBytes(files.Path);
        TerrainRegionSnapshot[] regions = CandidateRegions(basis, fixture.Site.Cell);
        ConstructionSettlementResult oldResult = new ConstructionSettlementResolver().ResolveCoupled(basis, regions);
        ConstructionDescentAssessment initial = ConstructionDescentAssessment.Begin(basis, regions);
        Assert.False(initial.IsComplete, initial.FailureReason);
        Assert.Empty(initial.ImpactDemands);
        Assert.Throws<InvalidOperationException>(() => initial.Result);
        ConstructionDescentAssessment complete = Finish(initial, 256);
        Assert.False(complete.IsFailed, complete.FailureReason);
        AssertSameGeometry(oldResult, complete.Result);
        AssertConservedBackingAndBodies(basis, complete.Result, fixture);
        AssertImpactArithmetic(basis, complete, fixture);
        byte[] finalBytes = ConstructionDescentAssessmentCodecV1.Encode(complete);
        byte[] finalDemands = DemandBytes(complete);

        foreach (int budget in new[] { 1, 2, 256 })
        {
            ConstructionDescentAssessment sliced = Finish(ConstructionDescentAssessment.Begin(basis, regions.Reverse()), budget);
            Assert.Equal(finalBytes, ConstructionDescentAssessmentCodecV1.Encode(sliced));
            Assert.Equal(finalDemands, DemandBytes(sliced));
            AssertSameGeometry(complete.Result, sliced.Result);
        }
        ConstructionDescentAssessment prefix = initial;
        while (true)
        {
            byte[] archive = ConstructionDescentAssessmentCodecV1.Encode(prefix);
            ConstructionDescentAssessment restored = ConstructionDescentAssessmentCodecV1.Decode(archive,
                basis, regions.Reverse(), TestContext.Current.CancellationToken);
            Assert.Equal(archive, ConstructionDescentAssessmentCodecV1.Encode(restored));
            ConstructionDescentAssessment resumed = Finish(restored, 2);
            Assert.Equal(finalBytes, ConstructionDescentAssessmentCodecV1.Encode(resumed));
            Assert.Equal(finalDemands, DemandBytes(resumed));
            AssertSameGeometry(complete.Result, resumed.Result);
            if (prefix.IsComplete) { break; }
            Assert.Empty(prefix.ImpactDemands);
            Assert.Throws<InvalidOperationException>(() => prefix.Result);
            prefix = prefix.Advance(1);
        }

        Assert.False(initial.IsComplete);
        Assert.Equal(0UL, initial.CompletedWorkUnits);
        Assert.Empty(initial.ImpactDemands);
        Assert.Equal(root, WorldSnapshotCodec.Encode(basis));
        Assert.Equal(database, File.ReadAllBytes(files.Path));
        Assert.Equal(0UL, basis.Excavation!.Sites.Single().CompletedWork);
        Assert.DoesNotContain(basis.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        Assert.True(basis.Space.Regions.Single(region => region.Bounds.Contains(fixture.Site.Cell)).GetCell(fixture.Site.Cell).IsSolid);
        RetainEvidence(files.Path, basis, regions, complete, fixture);
    }

    [Fact]
    public void Assessment_archives_authenticate_the_whole_world_and_reject_forged_work_and_replaced_scene_inputs()
    {
        var fixture = new CoupledSettlementWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot basis = fixture.PrepareBeforeExcavation(files.Path);
        byte[] root = WorldSnapshotCodec.Encode(basis);
        byte[] database = File.ReadAllBytes(files.Path);
        TerrainRegionSnapshot[] regions = CandidateRegions(basis, fixture.Site.Cell);
        ConstructionDescentAssessment initial = ConstructionDescentAssessment.Begin(basis, regions);
        ConstructionDescentAssessment prefix = initial.Advance(1);
        Assert.False(prefix.IsComplete, prefix.FailureReason);
        ConstructionDescentAssessment complete = Finish(prefix, 256);
        Assert.False(complete.IsFailed, complete.FailureReason);
        AssertInvalidArchives(basis, regions, prefix, complete);
        AssertInvalidSceneInputs(basis, regions);
        AssertHarmfulPolicyRefusals(basis, regions, complete);
        Assert.Equal(root, WorldSnapshotCodec.Encode(basis));
        Assert.Equal(database, File.ReadAllBytes(files.Path));

        // This is a genuine later committed root with the same spatial and physical state. A cursor
        // belongs to its complete original world, even when both assessments reach the same geometry.
        WorldSnapshot later;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            later = authority.Commit(fixture.Actions.Batch(basis, [], [])).Candidate;
        }
        AssertSameGeometry(new(basis.Space, basis.ConstructionPhysical!), new(later.Space, later.ConstructionPhysical!));
        Assert.Equal(basis.Inventory.ContentHash, later.Inventory.ContentHash);
        Assert.Equal(basis.Production!.ContentHash, later.Production!.ContentHash);
        Assert.NotEqual(basis.Calendar, later.Calendar);
        Assert.NotEqual(basis.ContentHash, later.ContentHash);
        ConstructionDescentAssessment laterAssessment = ConstructionDescentAssessment.Begin(later, regions);
        Assert.NotEqual(initial.InputPin, laterAssessment.InputPin);
        Assert.Throws<FormatException>(() => ConstructionDescentAssessmentCodecV1.Decode(
            ConstructionDescentAssessmentCodecV1.Encode(prefix), later, regions, TestContext.Current.CancellationToken));
        AssertSameGeometry(complete.Result, Finish(laterAssessment, 2).Result);
        Assert.Equal(root, WorldSnapshotCodec.Encode(basis));
    }

    private static ConstructionDescentAssessment Finish(ConstructionDescentAssessment assessment, int budget)
    {
        while (!assessment.IsComplete)
        {
            Assert.True(assessment.CompletedWorkUnits < (ulong)assessment.MaximumTotalWorkUnits);
            ulong before = assessment.CompletedWorkUnits;
            assessment = assessment.Advance(budget);
            Assert.InRange(assessment.CompletedWorkUnits - before, 1UL, (ulong)budget);
        }
        return assessment;
    }

    private static TerrainRegionSnapshot[] CandidateRegions(WorldSnapshot basis, CellCoordinate cell) =>
        basis.Space.Regions.Select(region => region.Bounds.Contains(cell)
            ? region.Excavate(region.Revision, [cell]) : region).ToArray();

    private static void AssertInvalidArchives(WorldSnapshot basis, TerrainRegionSnapshot[] regions,
        ConstructionDescentAssessment prefix, ConstructionDescentAssessment complete)
    {
        byte[] archive = ConstructionDescentAssessmentCodecV1.Encode(prefix);
        byte[] terminal = ConstructionDescentAssessmentCodecV1.Encode(complete);
        ConstructionDescentAssessment Decode(byte[] bytes) => ConstructionDescentAssessmentCodecV1.Decode(bytes,
            basis, regions, TestContext.Current.CancellationToken);
        byte[] pin = (byte[])archive.Clone();
        pin[FieldOffset(pin, 1)] ^= 1;
        Assert.Throws<FormatException>(() => Decode(pin));
        foreach (ulong status in new[] { 1UL, 2UL, 3UL, ulong.MaxValue })
        {
            Assert.Throws<FormatException>(() => Decode(ChangeNumber(archive, 3, status)));
        }
        Assert.Throws<FormatException>(() => Decode(ChangeNumber(archive, 2, (ulong)prefix.MaximumTotalWorkUnits + 1)));
        Assert.Throws<FormatException>(() => Decode(ChangeNumber(archive, 2, ulong.MaxValue)));
        Assert.Throws<FormatException>(() => Decode(ChangeNumber(terminal, 2, complete.CompletedWorkUnits + 1)));
        Assert.Throws<FormatException>(() => Decode(ChangeNumber(terminal, 3, 0)));
        for (int length = 0; length < archive.Length; length++)
        {
            Assert.Throws<FormatException>(() => Decode(archive[..length]));
        }
        Assert.Throws<FormatException>(() => Decode([.. archive, 0]));
        byte[] excessiveFields = (byte[])archive.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(excessiveFields.AsSpan(8), uint.MaxValue);
        Assert.Throws<FormatException>(() => Decode(excessiveFields));
        Assert.Throws<OperationCanceledException>(() => ConstructionDescentAssessmentCodecV1.Decode(
            archive, basis, regions, new CancellationToken(canceled: true)));
        Assert.Throws<FormatException>(() => ConstructionDescentAssessmentCodecV1.Decode(
            archive, basis, basis.Space.Regions, TestContext.Current.CancellationToken));
    }

    private static void AssertInvalidSceneInputs(WorldSnapshot basis, TerrainRegionSnapshot[] regions)
    {
        Assert.Throws<ArgumentException>(() => ConstructionDescentAssessment.Begin(basis, regions.Skip(1)));
        Assert.Throws<ArgumentException>(() => ConstructionDescentAssessment.Begin(basis, regions.Append(regions[0])));
        int editedIndex = Array.FindIndex(regions, region => region.Cells.Any(cell => cell.IsExcavated));
        Assert.True(editedIndex >= 0);
        TerrainRegionSnapshot edited = regions[editedIndex];
        Assert.Contains(basis.Space.Regions.Single(region => region.Bounds == edited.Bounds).Cells, cell => cell.IsExcavated);
        TerrainRegionSnapshot regenerated = new StratifiedTerrainGenerator().Generate(edited.Scope, edited.Seed,
            edited.Rules, edited.Bounds);
        // Keep the complete partition, source calibration and revision, but illegally restore old
        // persistent holes. Rejection must not depend on a missing region or revision rewind.
        regenerated = TerrainRegionCodecV1.Decode(ChangeNumber(TerrainRegionCodecV1.Encode(regenerated), 5, edited.Revision));
        TerrainRegionSnapshot[] restoredHoles = (TerrainRegionSnapshot[])regions.Clone();
        restoredHoles[editedIndex] = regenerated;
        Assert.Throws<ArgumentException>(() => ConstructionDescentAssessment.Begin(basis, restoredHoles));

        // Replace the surface material's exact content pin inside an otherwise canonical terrain
        // archive. The region codec permits authored content, but this assessment must retain its basis.
        byte[] substitutedBytes = TerrainRegionCodecV1.Encode(edited);
        int rules = FieldOffset(substitutedBytes, 3);
        int surface = rules + FieldOffset(substitutedBytes[rules..], 4);
        int materialId = surface + FieldOffset(substitutedBytes[surface..], 1);
        new EntityId(0x64657363656e74, 1).ToByteArray().CopyTo(substitutedBytes, materialId);
        int materialVersion = surface + FieldOffset(substitutedBytes[surface..], 2);
        substitutedBytes[materialVersion] ^= 1;
        TerrainRegionSnapshot replacement = TerrainRegionCodecV1.Decode(substitutedBytes);
        Assert.Equal(edited.Bounds, replacement.Bounds);
        Assert.Equal(edited.Revision, replacement.Revision);
        Assert.Equal(edited.Cells.Select(cell => cell.IsExcavated), replacement.Cells.Select(cell => cell.IsExcavated));
        TerrainRegionSnapshot[] replacedSubstrate = (TerrainRegionSnapshot[])regions.Clone();
        replacedSubstrate[editedIndex] = replacement;
        Assert.Throws<ArgumentException>(() => ConstructionDescentAssessment.Begin(basis, replacedSubstrate));
    }

    private static void AssertHarmfulPolicyRefusals(WorldSnapshot basis, TerrainRegionSnapshot[] regions,
        ConstructionDescentAssessment harmless)
    {
        foreach (bool actorLimit in new[] { true, false })
        {
            // A separately supplied diagnostic root keeps the funded material and geometry. This
            // constructor does not mutate the saved policy or establish an admitted history transition.
            var policy = new StructuralSettlementPolicy(256, actorLimit ? 0 : 256, actorLimit ? 256 : 0,
                basis.StructuralSettlementPolicy!.GravityMillimetresPerSecondSquared);
            var counterfactual = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
                basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering,
                basis.Production, basis.Progression, basis.Quests, basis.Construction,
                excavation: basis.Excavation, constructionPhysical: basis.ConstructionPhysical,
                structuralPolicy: basis.StructuralPolicy, structuralSettlementPolicy: policy,
                consequencePolicy: basis.ConsequencePolicy, coupledSettlementPolicy: basis.CoupledSettlementPolicy);
            ConstructionDescentAssessment refused = Finish(ConstructionDescentAssessment.Begin(counterfactual, regions), 2);
            Assert.True(refused.IsFailed);
            Assert.False(string.IsNullOrEmpty(refused.FailureReason));
            Assert.NotEqual(harmless.InputPin, refused.InputPin);
            Assert.Equal(harmless.ImpactDemands.Count, refused.ImpactDemands.Count);
            Assert.Contains(refused.ImpactDemands, demand => !demand.IsHarmless);
            foreach (var demand in refused.ImpactDemands)
            {
                var original = Assert.Single(harmless.ImpactDemands, value => value.BodyId == demand.BodyId);
                Assert.Equal(original.MassMilligrams, demand.MassMilligrams);
                Assert.Equal(original.DropSubunits, demand.DropSubunits);
                Assert.Equal(original.EnergyNumeratorPicojoules, demand.EnergyNumeratorPicojoules);
                Assert.Equal(original.FlightMilliseconds, demand.FlightMilliseconds);
                Assert.Equal(demand.IsActor != actorLimit, demand.IsHarmless);
            }
            Assert.Throws<InvalidOperationException>(() => refused.Result);
            byte[] bytes = ConstructionDescentAssessmentCodecV1.Encode(refused);
            ConstructionDescentAssessment restored = ConstructionDescentAssessmentCodecV1.Decode(bytes,
                counterfactual, regions, TestContext.Current.CancellationToken);
            Assert.True(restored.IsFailed);
            Assert.Equal(bytes, ConstructionDescentAssessmentCodecV1.Encode(restored));
            Assert.Equal(DemandBytes(refused), DemandBytes(restored));
            Assert.Throws<InvalidOperationException>(() => restored.Result);
        }
    }

    private static void AssertConservedBackingAndBodies(WorldSnapshot basis, ConstructionSettlementResult result,
        CoupledSettlementWorldFixture fixture)
    {
        Assert.Equal(basis.Construction!.ContentHash, result.Physical.ConstructionHash);
        Assert.Equal(3, result.Physical.Members.Count);
        Assert.Equal(2, result.Physical.Members.Select(member => member.StructureId).Distinct().Count());
        foreach (ConstructionMemberState member in result.Physical.Members)
        {
            ConstructionMemberState original = basis.ConstructionPhysical!.GetMember(member.StructureId, member.MemberIndex);
            Assert.Equal(original.ElementId, member.ElementId);
            Assert.Equal(original.ConditionParts, member.ConditionParts);
            Assert.Equal(original.Displacement.Offset(0, 0, -256), member.Displacement);
            Assert.Equal(basis.ConstructionPhysical.GetMemberReference(member.StructureId, member.MemberIndex),
                result.Physical.GetMemberReference(member.StructureId, member.MemberIndex));
        }
        foreach (ActorPresence actor in basis.Space.Actors)
        {
            ActorPresence after = result.Space.GetActor(actor.ActorId);
            Assert.Equal(actor.Binding, after.Binding);
            Assert.Equal(actor.Condition.ComputeContentHash(), after.Condition.ComputeContentHash());
            Assert.Equal(actor.HomePlaceId, after.HomePlaceId);
            Assert.Equal(actor.ActorId == fixture.Builder ? actor.Feet.Offset(0, 0, -256) : actor.Feet, after.Feet);
        }
    }

    private static void AssertImpactArithmetic(WorldSnapshot basis, ConstructionDescentAssessment assessment,
        CoupledSettlementWorldFixture fixture)
    {
        var policy = basis.StructuralSettlementPolicy!;
        Assert.Equal(256UL, StructuralImpactDemand.EnergyDenominator);
        var assemblyMasses = basis.ConstructionPhysical!.Members.GroupBy(member => member.StructureId)
            .ToDictionary(group => group.Key, group => group.Aggregate(BigInteger.Zero, (sum, member) => sum
                + basis.ConstructionPhysical.GetMemberReference(member.StructureId, member.MemberIndex).Mass.Value));
        Assert.Equal(3, assessment.ImpactDemands.Count);
        Assert.Single(assessment.ImpactDemands, demand => demand.IsActor);
        BigInteger totalMass = BigInteger.Zero;
        BigInteger totalEnergyNumerator = BigInteger.Zero;
        foreach (var demand in assessment.ImpactDemands)
        {
            Assert.Equal(256L, demand.DropSubunits);
            Assert.True(demand.IsHarmless);
            BigInteger mass;
            if (demand.IsActor)
            {
                Assert.Equal(fixture.Builder, demand.BodyId);
                mass = ActorMass(basis, fixture.Builder);
                Assert.Equal(mass, BigInteger.Parse(basis.StructuralPolicy!.Masses.ActorLoad(basis, fixture.Builder)
                    .ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
            }
            else
            {
                Assert.True(assemblyMasses.TryGetValue(demand.BodyId, out mass));
            }
            Assert.Equal(mass, new BigInteger(demand.MassMilligrams));
            // mg * (mm/s^2) * mm equals picojoules. Retaining a denominator of 256 avoids
            // rounding the exact 1/256-metre displacement before computing impact demand.
            BigInteger numerator = mass * policy.GravityMillimetresPerSecondSquared * demand.DropSubunits * 1000;
            Assert.Equal(numerator, BigInteger.Parse(demand.EnergyNumeratorPicojoules.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture));
            BigInteger distanceNumerator = new BigInteger(demand.DropSubunits) * 2 * 1000 * 1_000_000;
            BigInteger reaches = new BigInteger(policy.GravityMillimetresPerSecondSquared) * 256;
            Assert.True(reaches * demand.FlightMilliseconds * demand.FlightMilliseconds >= distanceNumerator);
            Assert.True(demand.FlightMilliseconds > 0);
            BigInteger prior = demand.FlightMilliseconds - 1;
            Assert.True(reaches * prior * prior < distanceNumerator);
            totalMass += mass;
            totalEnergyNumerator += numerator;
        }
        Assert.Equal(assemblyMasses.Values.Aggregate(BigInteger.Zero, (sum, value) => sum + value)
            + ActorMass(basis, fixture.Builder), totalMass);
        Assert.Equal(totalMass * policy.GravityMillimetresPerSecondSquared * 256 * 1000, totalEnergyNumerator);
    }

    private static BigInteger ActorMass(WorldSnapshot world, EntityId actorId)
    {
        ActorPresence actor = world.Space.GetActor(actorId);
        StructuralMassCatalog masses = world.StructuralPolicy!.Masses;
        StructuralBodyMass body = Assert.Single(masses.Bodies, value => value.DefinitionId == actor.Binding.DefinitionId
            && value.BodyVersion == actor.Binding.DefinitionVersion);
        var itemMasses = masses.SupplementalItems.Select(item => (item.Item, Mass: item.MassPerUnit.Value))
            .Concat(world.Production!.Recipes.SelectMany(recipe => recipe.Bindings)
                .Select(binding => (binding.Item, Mass: binding.Physical.MassPerUnit.Value)))
            .GroupBy(binding => binding.Item).ToDictionary(group => group.Key,
                group => Assert.Single(group.Select(binding => binding.Mass).Distinct()));
        HashSet<EntityId> custodians = [world.GetResident(actorId).InventoryCustodianId];
        HashSet<EntityId> installed = [];
        foreach (ConstructionProject project in world.Construction!.Projects.Where(project => project.BuilderActorId == actorId))
        {
            Assert.Equal(ConstructionStockLocation.BuilderCarried, project.StockLocation);
            custodians.Add(project.ProjectCustodianId);
            if (project.Installed is { } structure) { installed.Add(structure.GuardLotId); }
        }
        BigInteger total = body.Mass.Value;
        foreach (CountedSourceLot lot in world.Inventory.Custody.Lots.Where(lot => custodians.Contains(lot.CustodianId)))
        {
            total += new BigInteger(lot.Quantity) * itemMasses[lot.Definition];
        }
        foreach (var lot in world.Production.Lots.Where(lot => custodians.Contains(lot.CustodianId) && !installed.Contains(lot.LotId)))
        {
            total += new BigInteger(lot.Quantity) * itemMasses[lot.Definition];
        }
        return total;
    }

    private static void AssertSameGeometry(ConstructionSettlementResult expected, ConstructionSettlementResult actual)
    {
        Assert.Equal(ConstructionPhysicalCodecV1.Encode(expected.Physical), ConstructionPhysicalCodecV1.Encode(actual.Physical));
        Assert.Equal(expected.Space.Scope, actual.Space.Scope);
        Assert.Equal(expected.Space.GroundSupport, actual.Space.GroundSupport);
        Assert.Equal(expected.Space.StructureSupport, actual.Space.StructureSupport);
        Assert.Equal(expected.Space.Regions.Select(region => region.ComputeContentHash()).ToArray(),
            actual.Space.Regions.Select(region => region.ComputeContentHash()).ToArray());
        Assert.Equal(expected.Space.Actors.Select(actor => actor.ActorId).ToArray(), actual.Space.Actors.Select(actor => actor.ActorId).ToArray());
        foreach (ActorPresence actor in expected.Space.Actors)
        {
            ActorPresence other = actual.Space.GetActor(actor.ActorId);
            Assert.Equal(actor.Binding, other.Binding);
            Assert.Equal(actor.Condition.ComputeContentHash(), other.Condition.ComputeContentHash());
            Assert.Equal(actor.Feet, other.Feet);
            Assert.Equal(actor.Revision, other.Revision);
            Assert.Equal(actor.HomePlaceId, other.HomePlaceId);
        }
    }

    private static byte[] DemandBytes(ConstructionDescentAssessment assessment) => JsonSerializer.SerializeToUtf8Bytes(
        assessment.ImpactDemands.Select(demand => new
        {
            BodyId = demand.BodyId.ToString(), demand.IsActor, demand.DropSubunits, demand.MassMilligrams,
            EnergyNumeratorPicojoules = demand.EnergyNumeratorPicojoules.ToString(CultureInfo.InvariantCulture),
            EnergyDenominator = 256, demand.FlightMilliseconds, demand.IsHarmless
        }).ToArray());

    private static byte[] ChangeNumber(byte[] bytes, int field, ulong value)
    {
        byte[] changed = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt64BigEndian(changed.AsSpan(FieldOffset(changed, field)), value);
        return changed;
    }

    private static int FieldOffset(byte[] bytes, int field)
    {
        int offset = 12;
        for (int index = 0; index < field; index++)
        {
            offset += checked(4 + (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
        }
        return offset + 4;
    }

    private static void RetainEvidence(string path, WorldSnapshot basis, TerrainRegionSnapshot[] regions,
        ConstructionDescentAssessment complete, CoupledSettlementWorldFixture fixture)
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_EVENT_DESCENT_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "basis.bin"), WorldSnapshotCodec.Encode(basis));
        File.Copy(path, Path.Combine(directory, "basis.sqlite"), overwrite: true);
        for (int index = 0; index < regions.Length; index++)
        {
            File.WriteAllBytes(Path.Combine(directory, $"candidate-region-{index}.bin"), TerrainRegionCodecV1.Encode(regions[index]));
        }
        File.WriteAllBytes(Path.Combine(directory, "complete-cursor.bin"), ConstructionDescentAssessmentCodecV1.Encode(complete));
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Contract = "construction-descent-assessment-fixture-v1", basis.ProfileVersion,
            WorldId = basis.WorldId.ToString(), BranchId = basis.BranchId.ToString(), SiteId = fixture.Site.SiteId.ToString(),
            SourceRevision = basis.Revision, SourceHash = basis.ContentHash.Value,
            InputPin = complete.InputPin.Value, PolicyHash = basis.StructuralPolicy!.ContentHash.Value,
            SettlementPolicyHash = basis.StructuralSettlementPolicy!.ContentHash.Value,
            CoupledPolicyHash = basis.CoupledSettlementPolicy!.ContentHash.Value,
            complete.CompletedWorkUnits, complete.MaximumTotalWorkUnits, complete.IsComplete, complete.IsFailed,
            ResultPhysicalHash = complete.Result.Physical.ContentHash.Value,
            CandidateRegionHashes = regions.Select(region => region.ComputeContentHash().Value).ToArray(),
            ResultActors = complete.Result.Space.Actors.Select(actor => new
            {
                ActorId = actor.ActorId.ToString(), actor.Feet, actor.Revision,
                BindingHash = actor.Binding.ComputeContentHash().Value, ConditionHash = actor.Condition.ComputeContentHash().Value
            }).ToArray(),
            ImpactDemands = JsonSerializer.Deserialize<JsonElement>(DemandBytes(complete))
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
