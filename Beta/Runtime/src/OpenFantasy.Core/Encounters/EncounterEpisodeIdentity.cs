using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Encounters;

/// <summary>Authority-derived episode identity; caller-chosen command IDs cannot select a damage stream.</summary>
public static class EncounterEpisodeIdentity
{
    public static EntityId Derive(EncounterLifecycleSnapshot lifecycle, EntityId scenarioId)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        _ = checked(lifecycle.EncounterCount + 1);
        EntityId family = Family(lifecycle, scenarioId);
        return new EntityId(family.High, family.Low ^ lifecycle.EncounterCount);
    }

    internal static EntityId Family(EncounterLifecycleSnapshot lifecycle, EntityId scenarioId)
    {
        var scenario = lifecycle.GetScenario(scenarioId);
        byte[] hash = ContentHash.Compute(CanonicalEncodingV1.Frame("encounter.episode-identity.v1",
            lifecycle.WorldId.ToByteArray(), lifecycle.BranchId.ToByteArray(), lifecycle.Seed.ToByteArray(),
            scenario.DefinitionId.ToByteArray(), scenario.Version.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(1))).ToByteArray();
        return EntityId.FromBytes(hash.AsSpan(0, EntityId.ByteLength));
    }
}
