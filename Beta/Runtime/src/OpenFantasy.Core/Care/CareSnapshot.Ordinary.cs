namespace OpenFantasy.Core.Care;

public sealed partial class CareSnapshot
{
    private void ValidateOrdinary()
    {
        if (Ordinary is not { } ordinary) return;
        foreach (var definition in ordinary.Definitions)
        {
            if (GetRemedy(definition.RemedyDefinitionId).Version != definition.RemedyVersion)
                throw new ArgumentException("Ordinary delivery must retain the exact admitted remedy.");
        }
        foreach (var use in ordinary.Uses)
        {
            var remedy = GetRemedy(ordinary.GetDefinition(use.UseDefinitionId).RemedyDefinitionId);
            int expected = checked(use.HpBefore + (int)Math.Min((uint)(use.MaximumHp - use.HpBefore), remedy.RestoredHp));
            if (use.HpAfter != expected)
                throw new ArgumentException("Ordinary use must retain the exact capped effect of its admitted remedy.");
        }
        var operations = OrdinaryPositions(ordinary).ToArray();
        var aftermathCommands = Acknowledgments.Select(value => value.CommandId)
            .Concat(Treatments.Select(value => value.CommandId)).Concat(Dispositions.Select(value => value.CommandId)).ToHashSet();
        var aftermathSequences = Acknowledgments.Select(value => value.WorldSequence)
            .Concat(Treatments.Select(value => value.WorldSequence)).Concat(Dispositions.Select(value => value.WorldSequence)).ToHashSet();
        if (operations.Any(value => aftermathCommands.Contains(value.Command) || aftermathSequences.Contains(value.Sequence)))
            throw new ArgumentException("Ordinary care cannot reuse an aftermath command or its exclusive world boundary.");
        foreach (var acknowledgment in Acknowledgments)
        {
            var treatment = Treatments.SingleOrDefault(value => value.AcknowledgmentId == acknowledgment.AcknowledgmentId);
            var disposition = Dispositions.SingleOrDefault(value => value.AcknowledgmentId == acknowledgment.AcknowledgmentId);
            ulong? terminalSequence = treatment?.WorldSequence ?? disposition?.WorldSequence;
            if (operations.Any(value => value.Sequence > acknowledgment.WorldSequence
                && (!terminalSequence.HasValue || value.Sequence < terminalSequence.Value)))
                throw new ArgumentException("Ordinary care cannot occur while an aftermath obligation is pending.");
        }
    }

    private void ValidateAddedPositions(CareSnapshot successor)
    {
        ulong lastAftermath = Acknowledgments.Select(value => value.WorldSequence)
            .Concat(Treatments.Select(value => value.WorldSequence)).Concat(Dispositions.Select(value => value.WorldSequence)).DefaultIfEmpty(0UL).Max();
        ulong lastOrdinary = 0;
        if (Ordinary is { } previous && successor.Ordinary is { } current)
        {
            var oldCommands = OrdinaryPositions(previous).Select(value => value.Command).ToHashSet();
            if (OrdinaryPositions(current).Any(value => !oldCommands.Contains(value.Command) && value.Sequence <= lastAftermath))
                throw new ArgumentException("A new ordinary care operation cannot be inserted before retained aftermath history.");
            lastOrdinary = OrdinaryPositions(previous).Select(value => value.Sequence).DefaultIfEmpty(0UL).Max();
        }
        // Aftermath owns an entire world sequence. A successor can append multiple completed
        // pairs, but cannot fill an earlier gap merely because its final snapshot is chronological.
        ulong lastSequence = Math.Max(lastOrdinary, lastAftermath);
        var oldAcknowledgments = Acknowledgments.Select(value => value.AcknowledgmentId).ToHashSet();
        var oldTreatments = Treatments.Select(value => value.TreatmentId).ToHashSet();
        var oldDispositions = Dispositions.Select(value => value.DispositionId).ToHashSet();
        if (successor.Dispositions.Any(value => !oldDispositions.Contains(value.DispositionId) && value.WorldSequence <= lastSequence)
            || successor.Acknowledgments.Any(value => !oldAcknowledgments.Contains(value.AcknowledgmentId) && value.WorldSequence <= lastSequence)
            || successor.Treatments.Any(value => !oldTreatments.Contains(value.TreatmentId) && value.WorldSequence <= lastSequence))
            throw new ArgumentException("New aftermath care must follow every retained care operation.");
    }

    private static IEnumerable<(EntityId Command, ulong Sequence, uint Ordinal)> OrdinaryPositions(OrdinaryCareSnapshot state) =>
        state.Uses.Select(value => (value.CommandId, value.WorldSequence, value.CommandOrdinal))
            .Concat(state.Permissions.Select(value => value.Revocation).OfType<CarePermissionRevocation>()
                .Select(value => (value.CommandId, value.WorldSequence, value.CommandOrdinal)));
}
