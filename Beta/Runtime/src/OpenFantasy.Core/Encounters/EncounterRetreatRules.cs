using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// The pinned retreat law of the explicit retreat interpretation. A retreat walks one ordinary movement sample per
/// resolved boundary, each admitted by the movement owner and the complete world root, and charges each sample
/// ceil(10000·100/(800·speed)) readiness: one-eighth of a standard action rounded up, so a cell is never cheaper than
/// one standard action. Contact is broken only where every living opponent's body centre is horizontally farther than
/// its longest granted ability range plus one full cell, the authority's maximum single displacement, so no one step
/// by either party restores contact; sightline, missing cells and floor never shorten that distance.
/// </summary>
public static class EncounterRetreatRules
{
    /// <summary>Exactly the ordinary exploration movement sample.</summary>
    public const long SampleSubunits = 32;

    /// <summary>Four cells of ground; a longer captured route is refused rather than truncated.</summary>
    public const int MaximumRouteSamples = 32;

    /// <summary>Readiness per sample is ceil(10000·numerator / (denominator·normalizedSpeed)).</summary>
    public const uint CostNumerator = 100;

    /// <summary>Eight samples make one cell, so a cell costs at least one standard action at any speed.</summary>
    public const uint CostDenominator = 800;

    /// <summary>The contact margin beyond an opponent's longest reach: one full cell.</summary>
    public const long ContactMarginSubunits = LocalSpatialSnapshot.MaximumMoveSubunits;

    public const string RulesText =
        "encounter.retreat.v2;sample=32-subunit-cardinal-prepare-move-then-complete-root-admission;route=1..32;"
        + "one-sample-per-resolved-boundary;delay=ceil(10000*100/(800*normalizedSpeed));"
        + "clear=every-living-opponent-xy-body-center-distance>longest-granted-range+256;sightline-floor-ignored;"
        + "scope=exactly-two-positive-sides;actor-only-combatant-of-its-side-living-or-downed;ground-locomotion;"
        + "interrupted-sample-charges-no-movement;disengaged-ends-episode-to-exploration";

    public static ContentHash Pin { get; } = ContentHash.Compute(CanonicalEncodingV1.Text(RulesText, nameof(RulesText)));

    public static ulong SampleDelay(uint normalizedSpeed)
    {
        ArgumentOutOfRangeException.ThrowIfZero(normalizedSpeed);
        UInt128 numerator = (UInt128)10_000 * CostNumerator;
        UInt128 denominator = (UInt128)CostDenominator * normalizedSpeed;
        return checked((ulong)((numerator + denominator - 1) / denominator));
    }

    /// <summary>
    /// The supported scope over an episode's combatants: exactly two positive sides, and the actor the only combatant of
    /// its side whether its would-be allies are living or downed. Side zero (bystanders) never counts.
    /// </summary>
    public static bool IsSupportedScope(IEnumerable<EncounterCombatant> combatants, EntityId actorId)
    {
        ArgumentNullException.ThrowIfNull(combatants);
        EncounterCombatant[] all = combatants.ToArray();
        ushort side = all.Single(value => value.ActorId == actorId).Side;
        return side != 0 && all.All(value => value.ActorId == actorId || value.Side != side)
            && all.Where(value => value.Side != 0).Select(value => value.Side).Distinct().Count() == 2;
    }

    /// <summary>The contact radius for an opponent whose longest granted reach is known: always a full cell beyond it.</summary>
    public static long ContactRadius(long longestRangeSubunits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(longestRangeSubunits, 1);
        return checked(longestRangeSubunits + ContactMarginSubunits);
    }
}
