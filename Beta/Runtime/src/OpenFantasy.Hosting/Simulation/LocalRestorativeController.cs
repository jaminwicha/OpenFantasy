using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Advises finite ordinary aid under the helper's own control and the patient's prior permission.</summary>
internal sealed class LocalRestorativeController
{
    internal const string PolicyVersion = "local.full-dose-assistance.v1";
    private static readonly byte[] SelectionDomain = Encoding.ASCII.GetBytes(PolicyVersion);

    internal IReadOnlyList<WorldActionCommand> SelectActions(WorldSnapshot state, Func<EntityId, bool> isOccupied,
        IReadOnlySet<EntityId> excludedActorIds, IReadOnlySet<EntityId>? deferredHelperIds = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(isOccupied);
        ArgumentNullException.ThrowIfNull(excludedActorIds);
        List<WorldActionCommand> selected = [];
        if (!LocalProfileBindings.AdmitsOrdinaryCare(state) || state.Care?.Ordinary is not { } ordinary
            || state.Care.Pending is not null || state.HasReadingHold || state.PendingExcavation is not null
            || state.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration)
        {
            return selected.AsReadOnly();
        }

        HashSet<EntityId> participating = new(excludedActorIds);
        HashSet<EntityId> selectedIdentities = [];
        foreach (var permission in ordinary.Permissions.OrderBy(value => value.HelperActorId)
            .ThenBy(value => value.PatientActorId).ThenBy(value => value.PermissionId))
        {
            EntityId helper = permission.HelperActorId, patient = permission.PatientActorId;
            if (!permission.IsActive || participating.Contains(helper) || participating.Contains(patient)
                || deferredHelperIds?.Contains(helper) == true
                || LocalCareController.HostControl(state, helper) is not { } control
                || !WorldActorAvailability.CanAct(state, helper))
            {
                continue;
            }
            var definition = ordinary.GetDefinition(permission.UseDefinitionId);
            if (definition.DeliveryMode is not RestorativeDeliveryMode.LivingAssistance and not RestorativeDeliveryMode.IncapacitatedAssistance) continue;
            var resources = state.Progression!.GetActor(patient);
            var remedy = state.Care.GetRemedy(definition.RemedyDefinitionId);
            bool wantsAid = definition.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance
                ? state.Recovery is not null && resources.CurrentHp == 0
                : FitsFullDose(resources.CurrentHp, resources.Stats.MaxHp, remedy.RestoredHp);
            if (!wantsAid
                || WorldRestorativeActions.Prepare(state, helper, patient, definition.DefinitionId,
                    permission.PermissionId) is not { } intent)
            {
                continue;
            }

            // Commit to the stable preferred proposal even if its identity is occupied. Trying
            // another permission or patient here would turn collision/rejection into a hidden retry.
            participating.Add(helper);
            participating.Add(patient);
            EntityId commandId = SelectionId(state, helper, intent);
            EntityId consumptionId = EntityId.DeriveSpawned(commandId, 0), useId = EntityId.DeriveSpawned(commandId, 1);
            if (commandId == consumptionId || commandId == useId || consumptionId == useId
                || Occupied(commandId) || Occupied(consumptionId) || Occupied(useId))
            {
                continue;
            }
            selectedIdentities.UnionWith([commandId, consumptionId, useId]);
            selected.Add(new(commandId, control.PrincipalId, control.SessionId, helper,
                state.WorldId, state.BranchId, state.Space.GetActor(helper).Revision, intent));
        }
        return selected.AsReadOnly();

        bool Occupied(EntityId identity) => selectedIdentities.Contains(identity)
            || state.IdentityIndex.Contains(identity) || isOccupied(identity);
    }

    /// <summary>Conservative local preference: a living patient must be able to receive the complete dose.</summary>
    internal static bool FitsFullDose(int currentHp, int maximumHp, uint restoredHp) =>
        currentHp > 0 && maximumHp > currentHp && restoredHp > 0
        && (long)maximumHp - currentHp >= restoredHp;

    /// <summary>
    /// Stable semantic choice, independent of clocks, movement and unrelated inventory activity.
    /// Commands still capture and revalidate those observational revisions at dispatch. Retrying an
    /// unchanged refused choice requires an explicit future policy, never automatic identity churn.
    /// </summary>
    internal static EntityId SelectionId(WorldSnapshot state, EntityId helperActorId, UseRestorativeItemIntent intent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(intent);
        if (intent.PermissionId is not { } permissionId || intent.PermissionGrantVersion is null
            || intent.ExpectedPermissionRevision is not { } permissionRevision || helperActorId == intent.RecipientActorId)
        {
            throw new ArgumentException("A helper selection requires its exact prior assistance permission.", nameof(intent));
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(SelectionDomain);
        foreach (EntityId identity in new[] { state.WorldId, state.BranchId, helperActorId,
            intent.RecipientActorId, intent.UseDefinitionId })
        {
            hash.AppendData(identity.ToByteArray());
        }
        hash.AppendData(intent.UseDefinitionVersion.ToByteArray());
        hash.AppendData(permissionId.ToByteArray());
        hash.AppendData(intent.PermissionGrantVersion.ToByteArray());
        AppendNumber(hash, permissionRevision);
        AppendNumber(hash, intent.ExpectedPatientProgressionRevision);
        AppendNumber(hash, (ulong)intent.Ingredient.Kind);
        hash.AppendData(intent.Ingredient.LotId.ToByteArray());
        AppendNumber(hash, intent.Ingredient.ExpectedRevision);
        AppendNumber(hash, intent.Ingredient.Quantity);
        return EntityId.FromBytes(hash.GetHashAndReset().AsSpan(0, EntityId.ByteLength));
    }

    private static void AppendNumber(IncrementalHash hash, ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }
}
