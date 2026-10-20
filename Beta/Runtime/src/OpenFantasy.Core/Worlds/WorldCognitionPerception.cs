using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>The admitted perception rule's public constants, shared by the law and the host policies that propose looks.</summary>
public static class WorldCognitionPerception
{
    /// <summary>
    /// Four cells of unobstructed sightline on the actor's own floor. A person notices a harvestable source a few steps
    /// away in plain view; terrain and structures block it, and nothing farther is ever observed.
    /// </summary>
    public const long ObservationRangeSubunits = 4 * SubunitPosition.SubunitsPerCell;
}
