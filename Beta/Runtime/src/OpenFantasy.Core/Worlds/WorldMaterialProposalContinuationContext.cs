using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Bounded proposal context of one coordinated closing boundary: the exact offers that were open
/// in the actual closing owner. It retains no closing root, terminal outcome or earlier context.
/// Its presence is the saved-proposal capability; an empty roster still selects that capability.
/// It grants and authenticates nothing.
/// </summary>
public sealed class WorldMaterialProposalContinuationContext
{
    public const int MaximumByteLength =
        (WorldMaterialProposal.MaximumByteLength + sizeof(uint)) * WorldMaterialProposalBook.MaximumOpen + 128;

    private WorldMaterialProposalContinuationContext(IEnumerable<WorldMaterialProposal> carried)
    {
        // The owner already proved bounded strict identity order and distinct identities.
        Carried = carried.ToList().AsReadOnly();
        byte[] bytes = CanonicalEncodingV1.Frame("world.material-proposal-continuation.v1",
            Carried.Select(value => (ReadOnlyMemory<byte>)value.Encode()).ToArray());
        if (Carried.Count > WorldMaterialProposalBook.MaximumOpen || bytes.Length > MaximumByteLength)
        {
            throw new ArgumentException("A proposal continuation context exceeds its bound.", nameof(carried));
        }
        ContentHash = ContentHash.Compute(bytes);
    }

    /// <summary>Every offer that was open at the closing boundary, in strict identity order.</summary>
    public ReadOnlyCollection<WorldMaterialProposal> Carried { get; }
    public ContentHash ContentHash { get; }

    internal static WorldMaterialProposalContinuationContext Capture(WorldMaterialProposalBook closing)
    {
        ArgumentNullException.ThrowIfNull(closing);
        return new(closing.Open);
    }

    /// <summary>
    /// Every carried offer stays exact, either open or in one terminal outcome. An offer the closing
    /// owner did not carry must have been created after the boundary.
    /// </summary>
    internal void ValidateSuccessor(WorldMaterialProposalBook successor, ulong closingWorldSequence)
    {
        ArgumentNullException.ThrowIfNull(successor);
        Dictionary<EntityId, WorldMaterialProposal> pending = Carried.ToDictionary(value => value.ProposalId);
        foreach (WorldMaterialProposal proposal in successor.Open.Concat(successor.Outcomes.Select(value => value.Proposal)))
        {
            if (pending.Remove(proposal.ProposalId, out WorldMaterialProposal? carried))
            {
                if (carried.ContentHash != proposal.ContentHash)
                {
                    throw new ArgumentException("A carried proposal must remain exact after its closing boundary.");
                }
            }
            else if (proposal.CreatedSequence <= closingWorldSequence)
            {
                throw new ArgumentException("A proposal the closing owner did not carry must follow its closing boundary.");
            }
        }
        if (pending.Count != 0)
        {
            throw new ArgumentException("Every carried proposal stays open or reaches one terminal outcome after its boundary.");
        }
    }
}
