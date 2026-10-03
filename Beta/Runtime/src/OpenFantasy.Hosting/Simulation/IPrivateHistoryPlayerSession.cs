namespace OpenFantasy.Hosting.Simulation;

/// <summary>Optional bounded discovery of terminal records for an explicitly captured participant selection.</summary>
public interface IPrivateHistoryPlayerSession
{
    bool SupportsPrivateHistoryDirectory { get; }
    LocalSelectedPlayerObservation ReadPrivateHistoryObservation();
    Task<LocalSelectedQueryResult<LocalPrivateHistoryPage>> ReadSelectedPrivateHistoryAsync(
        LocalActorSelection observed, LocalPrivateHistoryPageRequest request);
}
