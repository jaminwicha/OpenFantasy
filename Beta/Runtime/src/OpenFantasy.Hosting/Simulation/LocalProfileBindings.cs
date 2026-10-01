using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// Local action bindings for explicit saved interpretations. Each action names the profiles whose contract this
/// adapter can execute; sharing a Core owner alone grants no local route. Paired profile 28 remains privileged.
/// </summary>
internal static class LocalProfileBindings
{
    /// <summary>Saved bilateral material proposals and the NPC gift choices built on them.</summary>
    internal static bool AdmitsSavedProposals(WorldSnapshot state) =>
        state.ProfileVersion is 30 or WorldPaidExchangeAdmission.ProfileVersion or WorldMarketAdmission.ProfileVersion
            or WorldEncounterRetreatAdmission.ProfileVersion;

    /// <summary>Ordinary self-use, patient-owned permission withdrawal and permitted helper aid.</summary>
    internal static bool AdmitsOrdinaryCare(WorldSnapshot state) =>
        state.ProfileVersion is 26 or 27 or 30 or WorldPaidExchangeAdmission.ProfileVersion or WorldMarketAdmission.ProfileVersion
            or WorldEncounterRetreatAdmission.ProfileVersion;

    /// <summary>Existing ordinary and deferred excavation bindings, excluding the separate privileged paired route.</summary>
    internal static bool AdmitsExcavation(WorldSnapshot state) =>
        state.ProfileVersion is 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18
            or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 30
            or WorldPaidExchangeAdmission.ProfileVersion or WorldMarketAdmission.ProfileVersion
            or WorldEncounterRetreatAdmission.ProfileVersion;

    /// <summary>The explicit committed-retreat interpretation; no older profile reads a retreat request.</summary>
    internal static bool AdmitsRetreat(WorldSnapshot state) => state.ProfileVersion == WorldEncounterRetreatAdmission.ProfileVersion;

    /// <summary>An advancing exploration cutoff with no reading hold, retained consequence, care obligation or encounter.</summary>
    internal static bool IsUnheldExploration(WorldSnapshot state) =>
        !state.HasReadingHold && state.PendingExcavation is null && state.Care?.Pending is null && state.Pacing?.Encounter is null
        && state.EncounterLifecycle?.Phase is null or EncounterLifecyclePhase.Exploration;
}
