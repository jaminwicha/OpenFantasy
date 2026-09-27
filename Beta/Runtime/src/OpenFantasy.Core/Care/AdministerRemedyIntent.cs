using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Selects one exact material dose against captured care, patient and custody revisions.</summary>
public sealed record AdministerRemedyIntent : WorldActionIntent
{
    public AdministerRemedyIntent(EntityId acknowledgmentId, ulong expectedCareRevision,
        ulong expectedPatientPresenceRevision, ulong expectedPatientProgressionRevision,
        ulong expectedStorageRevision, ulong expectedProductionRevision, IngredientRequest ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        AcknowledgmentId = acknowledgmentId;
        ExpectedCareRevision = expectedCareRevision;
        ExpectedPatientPresenceRevision = expectedPatientPresenceRevision;
        ExpectedPatientProgressionRevision = expectedPatientProgressionRevision;
        ExpectedStorageRevision = expectedStorageRevision;
        ExpectedProductionRevision = expectedProductionRevision;
        Ingredient = ingredient;
    }

    public EntityId AcknowledgmentId { get; }
    public ulong ExpectedCareRevision { get; }
    public ulong ExpectedPatientPresenceRevision { get; }
    public ulong ExpectedPatientProgressionRevision { get; }
    public ulong ExpectedStorageRevision { get; }
    public ulong ExpectedProductionRevision { get; }
    public IngredientRequest Ingredient { get; }
}
