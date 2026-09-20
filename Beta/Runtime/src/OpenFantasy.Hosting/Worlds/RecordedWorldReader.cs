namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Privileged inspection of one exact commit in a closed world save. The exclusive database lease
/// covers schema checks and complete journal, checkpoint, receipt, identity and head verification.
/// The selected immutable state is returned only after that verification succeeds.
/// </summary>
public static class RecordedWorldReader
{
    /// <summary>
    /// Reads and verifies the entire bounded trace once, retaining the requested historical state.
    /// This synchronous operator operation grants no player visibility or authority to mutate or travel.
    /// An already open world must use its owning authority's serialized historical-read operation.
    /// </summary>
    public static RecordedWorldState ReadForHost(string path, ulong revision)
    {
        using WorldDatabase database = WorldDatabase.Open(path, create: false);
        return new WorldJournalRecovery().ReadRecordedState(database, revision);
    }
}
