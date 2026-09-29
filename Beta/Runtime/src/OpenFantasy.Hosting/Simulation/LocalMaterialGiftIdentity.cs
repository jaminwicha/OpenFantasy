using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Semantic decisions survive clocks, observed revisions, refusal and archive rotation.</summary>
internal static class LocalMaterialGiftIdentity
{
    internal static EntityId Offer(WorldSnapshot state, LocalMaterialGiftPolicy policy, ConsentedMaterialTerms terms)
    {
        using var hash = Begin(state, policy, "local.material-gift.offer.v1");
        hash.AppendData(policy.SelfUseDefinitionId.ToByteArray());
        hash.AppendData(policy.SelfUseVersion.ToByteArray());
        hash.AppendData(terms.LotId.ToByteArray());
        Number(hash, (ulong)terms.LotKind);
        var patient = state.Progression!.GetActor(policy.PartnerActorId);
        Number(hash, checked((ulong)patient.CurrentHp));
        Number(hash, checked((ulong)patient.Stats.MaxHp));
        return Finish(hash);
    }

    internal static EntityId Answer(WorldSnapshot state, LocalMaterialGiftPolicy policy, EntityId proposalId)
    {
        using var hash = Begin(state, policy, "local.material-gift.answer.v1");
        hash.AppendData(proposalId.ToByteArray());
        return Finish(hash);
    }

    private static IncrementalHash Begin(WorldSnapshot state, LocalMaterialGiftPolicy policy, string domain)
    {
        var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(domain));
        foreach (EntityId identity in new[] { state.WorldId, state.BranchId, policy.ActorId, policy.PartnerActorId })
        {
            hash.AppendData(identity.ToByteArray());
        }
        return hash;
    }

    private static EntityId Finish(IncrementalHash hash) =>
        EntityId.FromBytes(hash.GetHashAndReset().AsSpan(0, EntityId.ByteLength));

    private static void Number(IncrementalHash hash, ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }
}
