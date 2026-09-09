using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Authority;

/// <summary>Strict versioned access/source admission and existing custody bytes for durable reload.</summary>
public static class AuthorityEncodingV1
{
    public const int MaximumStateByteLength = 1_500_000;
    private const int ActorCollectionBytes = 20_544;
    private const int ControlCollectionBytes = 100_000;
    private const int PolicyCollectionBytes = 23_000;
    private const int SourceCollectionBytes = 128_000;
    private const int ReservationCollectionBytes = 81_984;
    private const int MaximumRecordBytes = 128;

    public static byte[] Encode(CountedAuthorityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return CanonicalEncodingV1.Frame("authority.counted-state", CanonicalEncodingV1.Unsigned64(1),
            CanonicalEncodingV1.Unsigned64(state.AdmittedUnitCapacity), CustodyEncodingV1.Encode(state.Custody),
            EncodeIdentities("authority.actors", state.Actors),
            CanonicalEncodingV1.Frame("authority.controls", state.Controls.Select(control =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("authority.control", control.PrincipalId.ToByteArray(),
                    control.SessionId.ToByteArray(), control.ActorId.ToByteArray())).ToArray()),
            CanonicalEncodingV1.Frame("authority.policies", state.Policies.Select(policy =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("authority.policy", policy.CustodianId.ToByteArray(),
                    policy.OwnerActorId.ToByteArray(), CanonicalEncodingV1.Unsigned64((ulong)policy.DepositRule))).ToArray()),
            CanonicalEncodingV1.Frame("authority.sources", state.Sources.Select(source =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("authority.source", source.SourceId.ToByteArray(),
                    source.Definition.DefinitionId.ToByteArray(), source.Definition.Version.ToByteArray(),
                    CanonicalEncodingV1.Unsigned64(source.UnitCapacity))).ToArray()),
            EncodeIdentities("authority.reservations", state.ReservationIds));
    }

    public static CountedAuthorityState Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            AuthorityFrameReader reader = new(bytes, MaximumStateByteLength, 9);
            reader.ExpectTag("authority.counted-state"u8);
            if (reader.ReadUnsigned64() != 1)
            {
                throw AuthorityFrameReader.Failure(AuthorityFormatFailure.UnsupportedVersion, "Authority version is unsupported.");
            }

            ulong capacity = reader.ReadUnsigned64();
            CustodySnapshot custody = CustodyEncodingV1.Decode(reader.ReadField(CustodyEncodingV1.MaximumSnapshotByteLength));
            List<EntityId> actors = ReadIdentities(reader.ReadField(ActorCollectionBytes), ActorCollectionBytes,
                CountedAuthorityState.MaximumActors, "authority.actors"u8);
            List<ActorControlBinding> controls = ReadControls(reader.ReadField(ControlCollectionBytes));
            List<CustodianAccessPolicy> policies = ReadPolicies(reader.ReadField(PolicyCollectionBytes));
            List<AdmittedCountedSource> sources = ReadSources(reader.ReadField(SourceCollectionBytes));
            List<EntityId> reservations = ReadIdentities(reader.ReadField(ReservationCollectionBytes), ReservationCollectionBytes,
                CountedAuthorityState.MaximumReservations, "authority.reservations"u8);
            reader.Complete();
            return new CountedAuthorityState(custody, capacity, actors, controls, policies, sources, reservations);
        }
        catch (CustodyFormatException)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "Nested custody state is invalid.");
        }
        catch (ArgumentException)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "Authority admission invariants are violated.");
        }
    }

    private static byte[] EncodeIdentities(string tag, IEnumerable<EntityId> identities) =>
        CanonicalEncodingV1.Frame(tag, identities.Select(identity => (ReadOnlyMemory<byte>)identity.ToByteArray()).ToArray());

    private static List<EntityId> ReadIdentities(ReadOnlySpan<byte> bytes, int maximumBytes, int maximumCount,
        ReadOnlySpan<byte> tag)
    {
        AuthorityFrameReader reader = new(bytes, maximumBytes, (uint)maximumCount + 1);
        reader.ExpectTag(tag);
        List<EntityId> identities = [];
        while (reader.RemainingFields > 0)
        {
            EntityId identity = reader.ReadIdentity();
            RequireIncreasing(identities.Count == 0 ? null : identities[^1], identity);
            identities.Add(identity);
        }

        reader.Complete();
        return identities;
    }

    private static List<ActorControlBinding> ReadControls(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, ControlCollectionBytes, CountedAuthorityState.MaximumControls + 1);
        reader.ExpectTag("authority.controls"u8);
        List<ActorControlBinding> controls = [];
        while (reader.RemainingFields > 0)
        {
            AuthorityFrameReader entry = new(reader.ReadField(MaximumRecordBytes), MaximumRecordBytes, 4);
            entry.ExpectTag("authority.control"u8);
            var control = new ActorControlBinding(entry.ReadIdentity(), entry.ReadIdentity(), entry.ReadIdentity());
            entry.Complete();
            RequireIncreasing(controls.Count == 0 ? null : controls[^1].ActorId, control.ActorId);
            controls.Add(control);
        }

        reader.Complete();
        return controls;
    }

    private static List<CustodianAccessPolicy> ReadPolicies(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, PolicyCollectionBytes, CustodySnapshot.MaximumCustodians + 1);
        reader.ExpectTag("authority.policies"u8);
        List<CustodianAccessPolicy> policies = [];
        while (reader.RemainingFields > 0)
        {
            AuthorityFrameReader entry = new(reader.ReadField(MaximumRecordBytes), MaximumRecordBytes, 4);
            entry.ExpectTag("authority.policy"u8);
            EntityId custodian = entry.ReadIdentity();
            EntityId owner = entry.ReadIdentity();
            ulong rule = entry.ReadUnsigned64();
            if (rule is not (1 or 2))
            {
                throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "Deposit rule is unsupported.");
            }

            entry.Complete();
            RequireIncreasing(policies.Count == 0 ? null : policies[^1].CustodianId, custodian);
            policies.Add(new CustodianAccessPolicy(custodian, owner, (CustodyDepositRule)rule));
        }

        reader.Complete();
        return policies;
    }

    private static List<AdmittedCountedSource> ReadSources(ReadOnlySpan<byte> bytes)
    {
        AuthorityFrameReader reader = new(bytes, SourceCollectionBytes, CountedAuthorityState.MaximumSources + 1);
        reader.ExpectTag("authority.sources"u8);
        List<AdmittedCountedSource> sources = [];
        while (reader.RemainingFields > 0)
        {
            AuthorityFrameReader entry = new(reader.ReadField(MaximumRecordBytes), MaximumRecordBytes, 5);
            entry.ExpectTag("authority.source"u8);
            EntityId source = entry.ReadIdentity();
            EntityId definition = entry.ReadIdentity();
            ReadOnlySpan<byte> hashBytes = entry.ReadField(32);
            if (hashBytes.Length != 32)
            {
                throw AuthorityFrameReader.Failure(AuthorityFormatFailure.InvalidValue, "A definition pin requires 32 hash bytes.");
            }

            var version = new ContentHash(Convert.ToHexString(hashBytes).ToLowerInvariant());
            ulong capacity = entry.ReadUnsigned64();
            entry.Complete();
            RequireIncreasing(sources.Count == 0 ? null : sources[^1].SourceId, source);
            sources.Add(new AdmittedCountedSource(source, new CountedItemDefinition(definition, version), capacity));
        }

        reader.Complete();
        return sources;
    }

    private static void RequireIncreasing(EntityId? previous, EntityId current)
    {
        if (previous is { } identity && identity >= current)
        {
            throw AuthorityFrameReader.Failure(AuthorityFormatFailure.NonCanonical, "Authority rows must be unique and strictly ordered.");
        }
    }
}
