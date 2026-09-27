using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Bounded immutable continuation context extracted from a structurally valid sealed boundary.
/// It retains no root or ancestor chain. Host journal verification supplies historical authentication.
/// </summary>
public sealed class WorldResourceArchiveAnchor
{
    internal WorldResourceArchiveAnchor(WorldResourceArchiveReference reference, WorldSnapshot boundary)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(boundary);
        if (reference.WorldId != boundary.WorldId || reference.BranchId != boundary.BranchId
            || reference.BoundarySequence != boundary.Revision || boundary.ResourceHistory is null
            || boundary.Progression is not { } progression || boundary.Care is not { } care
            || boundary.Quests is not { } quests || boundary.EncounterLifecycle is not { } lifecycle)
        {
            throw new ArgumentException("An archive anchor requires its complete scoped resource boundary.");
        }
        Reference = reference;
        Progression = progression;
        AdmissionVersion = boundary.AdmissionVersion;
        Care = care;
        Quests = quests;
        Lifecycle = lifecycle;
    }

    public WorldResourceArchiveReference Reference { get; }
    public ProgressionSnapshot Progression { get; }
    public ContentHash AdmissionVersion { get; }
    internal CareSnapshot Care { get; }
    internal QuestSnapshot Quests { get; }
    internal EncounterLifecycleSnapshot Lifecycle { get; }

    internal void ValidateSuccessor(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.WorldId != Reference.WorldId || world.BranchId != Reference.BranchId
            || world.AdmissionVersion != AdmissionVersion || world.Revision < Reference.BoundarySequence
            || world.Care is not { } care || world.Quests is not { } quests
            || world.EncounterLifecycle is not { } lifecycle)
        {
            throw new ArgumentException("A resource successor must retain its sealed world's scope and admission.");
        }
        ValidateQuests(quests);
        ValidateCare(care);
        ValidateLifecycle(lifecycle);
    }

    private void ValidateQuests(QuestSnapshot successor)
    {
        if (successor.WorldId != Quests.WorldId || successor.BranchId != Quests.BranchId
            || successor.Revision < Quests.Revision
            || !Quests.Definitions.Select(value => (value.DefinitionId, value.Version))
                .SequenceEqual(successor.Definitions.Select(value => (value.DefinitionId, value.Version))))
        {
            throw new ArgumentException("A resource successor cannot replace admitted quest definitions or regress quest history.");
        }
        foreach (var previous in Quests.Participations)
        {
            var current = successor.FindParticipation(previous.ActorId, previous.QuestId);
            if (current is null || current.AcceptanceCommandId != previous.AcceptanceCommandId
                || current.AcceptedAt != previous.AcceptedAt
                || previous.Status == ActorQuestStatus.Completed
                    && (current.CompletionCommandId != previous.CompletionCommandId || current.CompletedAt != previous.CompletedAt))
            {
                // These are every canonical participation field; an accepted record may gain completion.
                throw new ArgumentException("A resource successor preserves quest acceptance and every completed participation exactly.");
            }
        }
    }

    private void ValidateCare(CareSnapshot successor)
    {
        if (successor.WorldId != Care.WorldId || successor.BranchId != Care.BranchId
            || successor.Revision < Care.Revision
            || !Care.Remedies.Select(value => (value.DefinitionId, value.Version))
                .SequenceEqual(successor.Remedies.Select(value => (value.DefinitionId, value.Version)))
            || !Care.Agreements.Select(value => (value.AgreementId, value.Version))
                .SequenceEqual(successor.Agreements.Select(value => (value.AgreementId, value.Version))))
        {
            throw new ArgumentException("A resource successor retains every admitted remedy and care agreement.");
        }
        var acknowledgments = successor.Acknowledgments.ToDictionary(value => value.AcknowledgmentId);
        foreach (var previous in Care.Acknowledgments)
        {
            if (!acknowledgments.TryGetValue(previous.AcknowledgmentId, out var current)
                || !CareCodecV1.EncodeAcknowledgment(previous).AsSpan().SequenceEqual(CareCodecV1.EncodeAcknowledgment(current)))
            {
                throw new ArgumentException("A resource successor cannot remove or rewrite acknowledged care.");
            }
        }
        var treatments = successor.Treatments.ToDictionary(value => value.TreatmentId);
        foreach (var previous in Care.Treatments)
        {
            if (!treatments.TryGetValue(previous.TreatmentId, out var current)
                || !CareCodecV1.EncodeTreatment(previous).AsSpan().SequenceEqual(CareCodecV1.EncodeTreatment(current)))
            {
                throw new ArgumentException("A resource successor cannot remove or rewrite completed treatment.");
            }
        }
    }

    private void ValidateLifecycle(EncounterLifecycleSnapshot successor)
    {
        if (successor.WorldId != Lifecycle.WorldId || successor.BranchId != Lifecycle.BranchId
            || successor.Seed != Lifecycle.Seed || successor.Revision < Lifecycle.Revision
            || successor.EncounterCount < Lifecycle.EncounterCount
            || successor.ExplorationSteps < Lifecycle.ExplorationSteps || successor.TacticalTicks < Lifecycle.TacticalTicks
            || !Lifecycle.Abilities.Select(value => (value.DefinitionId, value.Version))
                .SequenceEqual(successor.Abilities.Select(value => (value.DefinitionId, value.Version)))
            || !Lifecycle.Scenarios.Select(value => (value.DefinitionId, value.Version))
                .SequenceEqual(successor.Scenarios.Select(value => (value.DefinitionId, value.Version))))
        {
            throw new ArgumentException("A resource successor retains encounter rules, seed and monotonic lifecycle history.");
        }
    }
}
