using OpenFantasy.Core.Care;

namespace OpenFantasy.Core.Worlds;

/// <summary>Read-only explanations, not persisted receipt codes or new gameplay rules.</summary>
public enum RestorativeAvailabilityReason
{
    Available,
    UnsupportedProfile,
    UnknownActor,
    UnknownDefinition,
    PendingAftermathCare,
    EncounterContext,
    ResolvingConsequences,
    ReadingHold,
    StaleSelection,
    SupplierIncapacitated,
    RecipientIncapacitated,
    FullHealth,
    ActionDebt,
    ManipulationUnavailable,
    UnsupportedForm,
    PermissionRequired,
    PermissionMismatch,
    PermissionRevoked,
    OutOfReach,
    NoOwnedDose,
    MaterialHistoryCapacity,
    OrdinaryHistoryCapacity
}

/// <summary>
/// One exact selection or its current blocker. An executable selection remains a concurrency token,
/// never a control grant; dispatch authenticates and reevaluates against its current owners.
/// </summary>
public sealed class RestorativeAvailability
{
    /// <summary>
    /// Recreates read-only availability advice from explicit values. It performs no world admission and grants
    /// no authority to use the selected item; normal authenticated submission must reevaluate the intent.
    /// </summary>
    public static RestorativeAvailability FromObservation(RestorativeAvailabilityReason reason,
        WorldActionRejection rejection, UseRestorativeItemIntent? intent)
    {
        if (!Enum.IsDefined(reason) || !Enum.IsDefined(rejection)
            || (reason == RestorativeAvailabilityReason.Available) != (intent is not null)
            || (reason == RestorativeAvailabilityReason.Available) != (rejection == WorldActionRejection.None))
        {
            throw new ArgumentException("Availability advice requires a known blocker or a complete available selection.");
        }
        return new RestorativeAvailability(reason, rejection, intent);
    }

    internal RestorativeAvailability(RestorativeAvailabilityReason reason, WorldActionRejection rejection,
        UseRestorativeItemIntent? intent = null)
    {
        Reason = reason;
        Rejection = rejection;
        Intent = intent;
    }

    public RestorativeAvailabilityReason Reason { get; }
    public WorldActionRejection Rejection { get; }
    public UseRestorativeItemIntent? Intent { get; }
    public bool CanUse => Intent is not null && Reason == RestorativeAvailabilityReason.Available;
}
