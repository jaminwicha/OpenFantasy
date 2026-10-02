using OpenFantasy.Core;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Worlds;

internal enum WorldCommitKind
{
    AdvancingStep = 1,
    HeldInteraction = 2,
    ProfileMigration = 3,
    ConsequenceContinuation = 4,
    TacticalAdvance = 5,
    EncounterDecision = 6,
    EncounterResolution = 7,
    EncounterLifecycle = 8,
    Care = 9,
    ControllerLifecycle = 10
}

internal enum WorldIdentityKind
{
    CommitCause = 1,
    Command = 2,
    Result = 3,
    SpawnedEntity = 4,
    AdmittedDefinition = 5
}

// These internal rows carry bounded stored bytes. The world service owns canonical interpretation,
// replay, scope/permission validation and publication; constructing a row commits nothing.
internal sealed record StoredWorldHead(ulong Sequence, ulong AdvancingSteps, byte[] State);
internal sealed record StoredWorldCheckpoint(ulong Sequence, ulong AdvancingSteps, byte[] State);
internal sealed record StoredWorldCommit(ulong Sequence, WorldCommitKind Kind, EntityId CauseId,
    ulong AdvancingSteps, byte[] AcceptedBatch, byte[] Outcomes, ContentHash BeforeHash, ContentHash AfterHash);
internal sealed record StoredWorldCommandOutcome(EntityId PrincipalId, EntityId CommandId, ulong Sequence,
    byte[] Command, byte[] Receipt);
internal sealed record StoredWorldIdentity(EntityId Identity, ulong Sequence, WorldIdentityKind Kind);
