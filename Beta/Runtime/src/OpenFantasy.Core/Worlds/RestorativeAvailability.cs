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
