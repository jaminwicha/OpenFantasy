using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Host;

/// <summary>Read-only proposed removal and event descent against an exact historical world.</summary>
internal sealed class ConstructionDescentOperator
{
    internal string Inspect(string databasePath, string commitText, string siteText)
    {
        if (!ulong.TryParse(commitText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong revision))
        {
            throw new ArgumentException("The recorded commit must be an unsigned decimal integer.", nameof(commitText));
        }
        EntityId siteId = EntityId.Parse(siteText);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(databasePath);
        RecordedWorldState recorded = authority.ReadRecordedStateForHost(revision);
        WorldSnapshot basis = recorded.State;
        var site = basis.Excavation?.Sites.SingleOrDefault(value => value.SiteId == siteId)
            ?? throw new ArgumentException("The proposed site must belong to the exact recorded world.", nameof(siteText));
        if (site.IsComplete)
        {
            throw new ArgumentException("A completed site has no new terrain removal to assess.", nameof(siteText));
        }
        var regions = basis.Space.Regions.Select(region => region.Bounds.Contains(site.Cell)
            ? region.Excavate(region.Revision, [site.Cell]) : region).ToArray();
        ConstructionDescentAssessment assessment = ConstructionDescentAssessment.Begin(basis, regions);
        while (!assessment.IsComplete) { assessment = assessment.Advance(256); }
        return JsonSerializer.Serialize(new
        {
            Contract = "construction-descent-assessment-v1",
            PublicationAuthority = false,
            WorldId = basis.WorldId.ToString(),
            BranchId = basis.BranchId.ToString(),
            recorded.SourceHeadRevision,
            SourceHeadHash = recorded.SourceHeadHash.Value,
            RecordedRevision = basis.Revision,
            StateHash = recorded.StateHash.Value,
            SiteId = siteId.ToString(),
            InputPin = assessment.InputPin.Value,
            PolicyHash = basis.StructuralPolicy!.ContentHash.Value,
            SettlementPolicyHash = basis.StructuralSettlementPolicy!.ContentHash.Value,
            CoupledPolicyHash = basis.CoupledSettlementPolicy!.ContentHash.Value,
            assessment.CompletedWorkUnits,
            assessment.MaximumTotalWorkUnits,
            assessment.IsComplete,
            assessment.IsFailed,
            assessment.FailureReason,
            Events = assessment.Events.Select(value => new
            {
                value.DropSubunits,
                BodyIds = value.BodyIds.Select(identity => identity.ToString()).ToArray()
            }).ToArray(),
            ImpactDemands = assessment.ImpactDemands.Select(demand => new
            {
                BodyId = demand.BodyId.ToString(),
                demand.IsActor,
                demand.DropSubunits,
                demand.MassMilligrams,
                EnergyNumeratorPicojoules = demand.EnergyNumeratorPicojoules.ToString(CultureInfo.InvariantCulture),
                StructuralImpactDemand.EnergyDenominator,
                demand.FlightMilliseconds,
                demand.IsHarmless
            }).ToArray(),
            CandidatePhysical = assessment.IsFailed ? null : new ConstructionPhysicalDiagnosticReport(assessment.Result.Physical),
            CandidateActors = assessment.IsFailed ? null : assessment.Result.Space.Actors.Select(actor => new
            {
                ActorId = actor.ActorId.ToString(), actor.Feet, actor.Revision,
                ConditionHash = actor.Condition.ComputeContentHash().Value
            }).ToArray()
        });
    }
}
