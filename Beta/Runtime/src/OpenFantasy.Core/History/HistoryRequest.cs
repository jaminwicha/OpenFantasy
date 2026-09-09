using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.History;

/// <summary>
/// Immutable location and prefix metadata for exact historical preparation.
/// This is not a prepared state, authorization grant or reconstruction service.
/// </summary>
public sealed record HistoryRequest
{
    public HistoryRequest(HistoryAddress address, HistoryBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        Address = address;
        Boundary = boundary;
    }

    public HistoryAddress Address { get; }
    public HistoryBoundary Boundary { get; }

    /// <summary>
    /// Tests a commit from an already resolved branch stream. Its causes may occur
    /// anywhere in the world; realm and coordinates do not filter the causal prefix.
    /// </summary>
    public bool Includes(HistoryCommitStamp commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        return commit.Identity.WorldId == Address.WorldId
            && Boundary.Includes(commit.AuthoritativeInstant, commit.Identity.Sequence);
    }

    public ContentHash ComputeContentHash() => ContentHash.Compute(HistoryRequestCodecV1.Encode(this));
}
