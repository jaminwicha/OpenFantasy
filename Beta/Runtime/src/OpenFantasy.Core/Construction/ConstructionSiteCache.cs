using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Construction;

/// <summary>
/// One replaceable geometry qualification. Each use checks the actual action's current scene and
/// project; this cache owns no work, material, clock, authorization or publication state.
/// </summary>
internal sealed class ConstructionSiteCache
{
    private readonly object _gate = new();
    private ConstructionSiteEvidence? _site;

    internal ulong Captures { get; private set; }
    internal ulong Reuses { get; private set; }

    internal StructurePlacementResult Prepare(LocalSpatialSnapshot space, ConstructionProject project,
        SpatialStructure proposed, IEnumerable<GroundAccessRequirement> access)
    {
        ConstructionSiteEvidence site;
        lock (_gate)
        {
            if (_site is not null)
            {
                try
                {
                    _site.ValidateProject(project);
                    _site.ValidateCurrent(space);
                }
                catch (InvalidOperationException)
                {
                    // A changed dependency requires actual geometry qualification, never a grant
                    // inferred from the earlier success. Failed qualifications are reusable too.
                    _site = null;
                }
            }
            if (_site is null)
            {
                _site = ConstructionSiteEvidence.Capture(space, project, proposed.StructureId);
                Captures++;
            }
            else
            {
                Reuses++;
            }
            site = _site;
        }
        StructurePlacementResult result = ((IConstructionGeometry)site).PrepareStructurePlacement(proposed, access);
        if (result.Status != StructurePlacementStatus.Prepared) return result;
        try
        {
            // Retain ordinary complete-scene admission, including each new command-derived identity.
            // Only placement/access qualification is cached; the material and physical owners remain live.
            return new(result.Status, space.WithStructures(space.Structures.Append(proposed)));
        }
        catch (OverflowException)
        {
            return new(StructurePlacementStatus.NumericLimit);
        }
    }
}
