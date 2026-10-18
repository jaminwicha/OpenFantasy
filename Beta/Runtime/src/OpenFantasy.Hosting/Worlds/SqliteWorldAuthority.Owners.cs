using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    /// <summary>Capture under the sole authority's admitted current head; this is preparation, not a work receipt.</summary>
    internal ConstructionWorkCohort CaptureStoredWorkCohort(WorldActionCommand command)
    {
        lock (_gate)
        {
            EnsureAvailable();
            if (!_database.SeparatesOwners) throw new InvalidOperationException("This save does not separate work owners.");
            var cohort = ConstructionWorkCohort.Capture(_state, command);
            if (cohort.DirectoryHash != _stateHash) throw new InvalidDataException("Work cohort differs from the committed directory.");
            return cohort;
        }
    }

    /// <summary>Read-only assessment. Head comparison and selected payload reads serialize with every writer.</summary>
    internal ConstructionWorkAssessment AssessStoredWorkCohort(ConstructionWorkCohort cohort)
    {
        ArgumentNullException.ThrowIfNull(cohort);
        lock (_gate)
        {
            EnsureAvailable();
            if (!_database.SeparatesOwners) throw new InvalidOperationException("This save does not separate work owners.");
            try { return cohort.Assess(_stateHash, _database.ReadOwner); }
            catch (Exception error) when (error is FormatException or InvalidDataException or IOException or Microsoft.Data.Sqlite.SqliteException)
            {
                _requiresRecovery = true;
                throw;
            }
        }
    }
}
