using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.History;

/// <summary>
/// Metadata for one indivisible committed transaction in a resolved branch stream.
/// A transaction may contain several ordered events; this value is not an event ID.
/// </summary>
public sealed record HistoryCommitStamp
{
    public HistoryCommitStamp(CommandCommitIdentity identity, WorldInstant authoritativeInstant)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
        AuthoritativeInstant = authoritativeInstant;
    }

    public CommandCommitIdentity Identity { get; }
    public WorldInstant AuthoritativeInstant { get; }
}
