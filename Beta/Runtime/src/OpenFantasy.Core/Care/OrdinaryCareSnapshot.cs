using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Care;

/// <summary>
/// Bounded ordinary care catalogs, immutable use history and one-way permission revocations.
/// These are structural witnesses; the world authenticates and publishes each operation.
/// </summary>
public sealed class OrdinaryCareSnapshot
{
    public const int MaximumDefinitions = 16;
    public const int MaximumPermissions = 32;
    public const int MaximumUses = 256;

    public OrdinaryCareSnapshot(IEnumerable<RestorativeUseDefinition> definitions,
        IEnumerable<CarePermission> permissions, IEnumerable<RestorativeUse> uses)
    {
        Definitions = Copy(definitions, value => value.DefinitionId, MaximumDefinitions);
        Permissions = Copy(permissions, value => value.PermissionId, MaximumPermissions);
        Uses = Copy(uses, value => value.UseId, MaximumUses);
        if (Definitions.Count == 0) throw new ArgumentException("Ordinary care requires an admitted use definition.");
        foreach (var permission in Permissions)
        {
            var definition = GetDefinition(permission.UseDefinitionId);
            if (definition.Version != permission.UseDefinitionVersion
                || definition.DeliveryMode != RestorativeDeliveryMode.LivingAssistance)
                throw new ArgumentException("Permission must name the exact ordinary assistance definition.");
        }
        HashSet<EntityId> commands = [];
        HashSet<(ulong Sequence, uint Ordinal)> positions = [];
        foreach (var revocation in Permissions.Select(value => value.Revocation).OfType<CarePermissionRevocation>())
            RequirePosition(revocation.CommandId, revocation.WorldSequence, revocation.CommandOrdinal, commands, positions);
        foreach (var use in Uses)
        {
            RequirePosition(use.CommandId, use.WorldSequence, use.CommandOrdinal, commands, positions);
            var definition = GetDefinition(use.UseDefinitionId);
            bool self = use.ActorId == use.RecipientActorId;
            if (definition.Version != use.UseDefinitionVersion || definition.ExplorationMilliseconds != use.ExplorationMilliseconds
                || self != (definition.DeliveryMode == RestorativeDeliveryMode.SelfUse))
                throw new ArgumentException("Ordinary use must preserve its admitted definition, delivery and time rule.");
            if (use.PermissionId is { } permissionId)
            {
                var permission = GetPermission(permissionId);
                if (permission.PatientActorId != use.RecipientActorId || permission.HelperActorId != use.ActorId
                    || permission.UseDefinitionId != use.UseDefinitionId || permission.UseDefinitionVersion != use.UseDefinitionVersion
                    || permission.GrantVersion != use.PermissionGrantVersion || use.PermissionRevision != 0
                    || permission.Revocation is { } revoked
                        && Compare(use.WorldSequence, use.CommandOrdinal, revoked.WorldSequence, revoked.CommandOrdinal) >= 0)
                    throw new ArgumentException("Ordinary assistance must precede revocation of its exact patient-owned permission.");
            }
        }
        Revision = checked((ulong)Uses.Count + (ulong)Permissions.Count(value => value.Revocation is not null));
    }

    public ReadOnlyCollection<RestorativeUseDefinition> Definitions { get; }
    public ReadOnlyCollection<CarePermission> Permissions { get; }
    public ReadOnlyCollection<RestorativeUse> Uses { get; }
    public ulong Revision { get; }

    public RestorativeUseDefinition GetDefinition(EntityId id) => Definitions.SingleOrDefault(value => value.DefinitionId == id)
        ?? throw new ArgumentException("The use definition is outside this ordinary care owner.");
    public CarePermission GetPermission(EntityId id) => Permissions.SingleOrDefault(value => value.PermissionId == id)
        ?? throw new ArgumentException("The permission is outside this ordinary care owner.");
    public RestorativeUse GetUse(EntityId id) => Uses.SingleOrDefault(value => value.UseId == id)
        ?? throw new ArgumentException("The completed use is outside this ordinary care owner.");

    public OrdinaryCareSnapshot RecordUse(RestorativeUse use)
    {
        ArgumentNullException.ThrowIfNull(use);
        RequireLater(use.WorldSequence, use.CommandOrdinal);
        return new(Definitions, Permissions, Uses.Append(use));
    }

    public OrdinaryCareSnapshot Revoke(CarePermissionRevocation revocation)
    {
        ArgumentNullException.ThrowIfNull(revocation);
        RequireLater(revocation.WorldSequence, revocation.CommandOrdinal);
        var permission = GetPermission(revocation.PermissionId).Revoke(revocation);
        return new(Definitions, Permissions.Select(value => value.PermissionId == permission.PermissionId ? permission : value), Uses);
    }

    internal void ValidateSuccessor(OrdinaryCareSnapshot successor)
    {
        if (successor.Revision < Revision
            || !Definitions.Select(value => (value.DefinitionId, value.Version))
                .SequenceEqual(successor.Definitions.Select(value => (value.DefinitionId, value.Version)))
            || !Permissions.Select(value => (value.PermissionId, value.GrantVersion))
                .SequenceEqual(successor.Permissions.Select(value => (value.PermissionId, value.GrantVersion))))
            throw new ArgumentException("An ordinary care successor retains its exact definitions and authored permission scopes.");
        foreach (var previous in Permissions)
        {
            var current = successor.GetPermission(previous.PermissionId);
            if (previous.Revocation is { } oldRevocation)
            {
                if (current.Revocation is not { } currentRevocation
                    || !CareCodecV2.EncodeRevocation(oldRevocation).AsSpan().SequenceEqual(CareCodecV2.EncodeRevocation(currentRevocation)))
                    throw new ArgumentException("An ordinary care successor cannot remove or rewrite a permission revocation.");
            }
            else if (current.Revocation is { } added) RequireLater(added.WorldSequence, added.CommandOrdinal);
        }
        foreach (var previous in Uses)
        {
            var current = successor.GetUse(previous.UseId);
            if (!CareCodecV2.EncodeUse(previous).AsSpan().SequenceEqual(CareCodecV2.EncodeUse(current)))
                throw new ArgumentException("An ordinary care successor cannot rewrite a completed use.");
        }
        var oldUseIds = Uses.Select(value => value.UseId).ToHashSet();
        foreach (var added in successor.Uses.Where(value => !oldUseIds.Contains(value.UseId)))
            RequireLater(added.WorldSequence, added.CommandOrdinal);
    }

    private void RequireLater(ulong sequence, uint ordinal)
    {
        if (Uses.Any(value => Compare(sequence, ordinal, value.WorldSequence, value.CommandOrdinal) <= 0)
            || Permissions.Any(value => value.Revocation is { } revoked
                && Compare(sequence, ordinal, revoked.WorldSequence, revoked.CommandOrdinal) <= 0))
            throw new ArgumentException("A new ordinary care operation must follow every retained operation.");
    }

    private static void RequirePosition(EntityId command, ulong sequence, uint ordinal, HashSet<EntityId> commands,
        HashSet<(ulong Sequence, uint Ordinal)> positions)
    {
        if (!commands.Add(command) || !positions.Add((sequence, ordinal)))
            throw new ArgumentException("Ordinary care operations require unique command identities and world positions.");
    }

    private static int Compare(ulong sequence, uint ordinal, ulong otherSequence, uint otherOrdinal) =>
        sequence == otherSequence ? ordinal.CompareTo(otherOrdinal) : sequence.CompareTo(otherSequence);

    private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> source, Func<T, EntityId> identity, int maximum) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        SortedDictionary<EntityId, T> values = [];
        foreach (var value in source)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (values.Count == maximum || !values.TryAdd(identity(value), value))
                throw new ArgumentException("Ordinary care catalogs and histories require bounded unique identities.");
        }
        return Array.AsReadOnly(values.Values.ToArray());
    }
}
