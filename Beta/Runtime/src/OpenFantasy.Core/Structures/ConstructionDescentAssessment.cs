using System.Collections.ObjectModel;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Complete-world, read-only event descent with retained material, bodies and policy. A successful
/// proposal proves geometry and its original loads; it cannot publish excavations or new carried ore.
/// </summary>
public sealed class ConstructionDescentAssessment
{
    private readonly WorldSnapshot _basis;
    private readonly TerrainRegionSnapshot[] _regions;
    private readonly VerticalMotionResolution _motion;
    private readonly ConstructionSettlementResult? _result;

    private ConstructionDescentAssessment(WorldSnapshot basis, TerrainRegionSnapshot[] regions,
        VerticalMotionResolution motion, ContentHash inputPin)
    {
        _basis = basis;
        _regions = regions;
        _motion = motion;
        InputPin = inputPin;
        ImpactDemands = Array.AsReadOnly(Array.Empty<StructuralImpactDemand>());
        if (!motion.IsComplete) { return; }
        if (motion.IsFailed)
        {
            FailureReason = motion.FailureReason;
            IsFailed = true;
            return;
        }
        try
        {
            ImpactDemands = CreateImpactDemands();
            if (ImpactDemands.Any(demand => !demand.IsHarmless))
            {
                throw new ArgumentException("The descent needs an explicit harmful-impact response beyond the retained harmless policy.");
            }
            _result = AdmitCandidate();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            FailureReason = error.Message;
            IsFailed = true;
        }
    }

    public ContentHash InputPin { get; }
    public bool IsComplete => _motion.IsComplete;
    public bool IsFailed { get; }
    public string? FailureReason { get; }
    public ulong CompletedWorkUnits => _motion.CompletedWorkUnits;
    public int MaximumTotalWorkUnits => _motion.MaximumTotalWorkUnits;
    public ReadOnlyCollection<VerticalMotionEvent> Events => _motion.Events;
    public ReadOnlyCollection<StructuralImpactDemand> ImpactDemands { get; }
    public ConstructionSettlementResult Result => _result
        ?? throw new InvalidOperationException("Only a complete harmless, geometrically supported and load-safe assessment has a candidate result.");

    public static ConstructionDescentAssessment Begin(WorldSnapshot basis,
        IEnumerable<TerrainRegionSnapshot> candidateRegions)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(candidateRegions);
        if (basis.ProfileVersion != 17 || basis.PendingExcavation is not null)
        {
            throw new ArgumentException("Event descent assessment requires a stable coupled structural world.", nameof(basis));
        }
        // Counterfactual holes retain every original region, substrate and actor. This validates
        // a proposed scene, not the labor, control or source rights needed to commit it.
        var contact = new ConstructionContactScene(basis, candidateRegions,
            basis.ConstructionPhysical!, basis.Space.Actors);
        TerrainRegionSnapshot[] regions = contact.Regions.ToArray();
        var affected = new ConstructionSettlementScene(basis, regions);
        affected.Prepare();
        SpatialStructure[] fixedStructures = basis.ConstructionPhysical!.GetStructures()
            .Where(structure => !affected.IsMoving(structure.StructureId)).ToArray();
        List<VerticalMotionBody> moving = basis.ConstructionPhysical.GetStructures()
            .Where(structure => affected.IsMoving(structure.StructureId))
            .Select(VerticalMotionBody.ForStructure).ToList();
        var movingActors = new HashSet<EntityId>();
        foreach (ActorPresence actor in basis.Space.Actors)
        {
            BodyVolume volume = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
            long stepHeight = Math.Min(LocalSpatialSnapshot.MaximumOrdinaryStepHeightSubunits,
                actor.Condition.Definition.ClearanceDimensions.HeightSubunits / 4);
            if (affected.IsRider(actor.ActorId)
                || FootingQuery.QueryStepped(affected.Terrain, fixedStructures, volume, stepHeight) != SpatialOccupancy.Clear)
            {
                moving.Add(VerticalMotionBody.ForActor(actor));
                movingActors.Add(actor.ActorId);
            }
        }
        var scene = new VerticalMotionScene(basis.Space.Scope, regions, fixedStructures,
            basis.Space.Actors.Where(actor => !movingActors.Contains(actor.ActorId)), moving,
            basis.StructuralSettlementPolicy!.MaximumDropSubunits);
        ContentHash pin = ContentHash.Compute(CanonicalEncodingV1.Frame("construction.descent-assessment.v1",
            basis.ContentHash.ToByteArray(), contact.InputPin.ToByteArray(), scene.InputPin.ToByteArray(),
            basis.StructuralPolicy!.ContentHash.ToByteArray(), basis.StructuralSettlementPolicy.ContentHash.ToByteArray(),
            basis.CoupledSettlementPolicy!.ContentHash.ToByteArray()));
        return new(basis, regions, VerticalMotionResolution.Begin(scene), pin);
    }

    public ConstructionDescentAssessment Advance(int workBudget = 16)
    {
        // Let the kernel validate even terminal-call budgets consistently.
        VerticalMotionResolution next = _motion.Advance(workBudget);
        return ReferenceEquals(next, _motion) ? this : new(_basis, _regions, next, InputPin);
    }

    private ReadOnlyCollection<StructuralImpactDemand> CreateImpactDemands()
    {
        Dictionary<EntityId, ulong> masses = _basis.ConstructionPhysical!.Members
            .GroupBy(member => member.StructureId).ToDictionary(group => group.Key,
                group => group.Aggregate(0UL, (mass, member) => checked(mass
                    + _basis.ConstructionPhysical.GetMemberReference(member.StructureId, member.MemberIndex).Mass.Value)));
        var actors = _basis.Space.Actors.Select(actor => actor.ActorId).ToHashSet();
        return Array.AsReadOnly(_motion.Result.Drops.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key)
            .Select(pair => new StructuralImpactDemand(pair.Key, actors.Contains(pair.Key),
                actors.Contains(pair.Key) ? checked((ulong)_basis.StructuralPolicy!.Masses.ActorLoad(_basis, pair.Key))
                    : masses[pair.Key], pair.Value, _basis.StructuralSettlementPolicy!)).ToArray());
    }

    private ConstructionSettlementResult AdmitCandidate()
    {
        ConstructionPhysicalSnapshot original = _basis.ConstructionPhysical!;
        IReadOnlyDictionary<EntityId, long> drops = _motion.Result.Drops;
        ConstructionMemberState[] updates = original.Members.Where(member => drops.GetValueOrDefault(member.StructureId) > 0)
            .Select(member => member.WithState(member.Revision, member.ConditionParts,
                member.Displacement.Offset(0, 0, -drops[member.StructureId]), ConstructionMemberDisposition.Resting)).ToArray();
        ConstructionPhysicalSnapshot physical = updates.Length == 0 ? original : original.WithMembers(original.Revision, updates);
        ActorPresence[] actors = _basis.Space.Actors.Select(actor =>
        {
            long drop = drops.GetValueOrDefault(actor.ActorId);
            return drop > 0 ? actor.At(actor.Feet.Offset(0, 0, -drop)) : actor;
        }).ToArray();
        var space = new LocalSpatialSnapshot(_basis.Space.Scope, _regions, actors, physical.GetStructures(),
            _basis.Space.GroundSupport, _basis.Space.StructureSupport);
        physical.ValidateStableGeometry(space);
        ConstructionBearingModel contacts = _basis.StructuralPolicy!.AssessContacts(
            new ConstructionContactScene(_basis, _regions, physical, actors));
        StructuralLoadResolution loads = StructuralLoadResolution.Begin(contacts.Graph, []);
        while (!loads.IsComplete) { loads = loads.Advance(StructuralLoadResolution.MaximumEvaluationsPerAdvance); }
        if (loads.Failures.Count != 0)
        {
            throw new ArgumentException("Stopped material requires a supported, failure-free complete load graph under the retained policy.");
        }
        // Complete graph construction/resolution and scene admission are outside geometric query
        // slicing. A transaction owner must revalidate after its own source/custody/time changes.
        return new(space, physical);
    }
}
