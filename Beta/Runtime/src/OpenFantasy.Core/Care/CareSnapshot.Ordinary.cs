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
            .Concat(Treatments.Select(value => value.CommandId)).ToHashSet();
        var aftermathSequences = Acknowledgments.Select(value => value.WorldSequence)
            .Concat(Treatments.Select(value => value.WorldSequence)).ToHashSet();
        if (operations.Any(value => aftermathCommands.Contains(value.Command) || aftermathSequences.Contains(value.Sequence)))
            throw new ArgumentException("Ordinary care cannot reuse an aftermath command or its exclusive world boundary.");
        foreach (var acknowledgment in Acknowledgments)
        {
            var treatment = Treatments.SingleOrDefault(value => value.AcknowledgmentId == acknowledgment.AcknowledgmentId);
            if (operations.Any(value => value.Sequence > acknowledgment.WorldSequence
                && (treatment is null || value.Sequence < treatment.WorldSequence)))
                throw new ArgumentException("Ordinary care cannot occur while an aftermath obligation is pending.");
        }
    }

    private void ValidateAddedOrdinaryPositions(CareSnapshot successor)
    {
        if (Ordinary is not { } previous || successor.Ordinary is not { } current) return;
        var oldCommands = OrdinaryPositions(previous).Select(value => value.Command).ToHashSet();
        ulong lastAftermath = Acknowledgments.Select(value => value.WorldSequence)
            .Concat(Treatments.Select(value => value.WorldSequence)).DefaultIfEmpty(0UL).Max();
        if (OrdinaryPositions(current).Any(value => !oldCommands.Contains(value.Command) && value.Sequence <= lastAftermath))
            throw new ArgumentException("A new ordinary care operation cannot be inserted before retained aftermath history.");
        ulong lastOrdinary = OrdinaryPositions(previous).Select(value => value.Sequence).DefaultIfEmpty(0UL).Max();
        var oldAcknowledgments = Acknowledgments.Select(value => value.AcknowledgmentId).ToHashSet();
        var oldTreatments = Treatments.Select(value => value.TreatmentId).ToHashSet();
        if (successor.Acknowledgments.Any(value => !oldAcknowledgments.Contains(value.AcknowledgmentId) && value.WorldSequence <= lastOrdinary)
            || successor.Treatments.Any(value => !oldTreatments.Contains(value.TreatmentId) && value.WorldSequence <= lastOrdinary))
            throw new ArgumentException("New aftermath care must follow retained ordinary care.");
    }

    private static IEnumerable<(EntityId Command, ulong Sequence, uint Ordinal)> OrdinaryPositions(OrdinaryCareSnapshot state) =>
        state.Uses.Select(value => (value.CommandId, value.WorldSequence, value.CommandOrdinal))
            .Concat(state.Permissions.Select(value => value.Revocation).OfType<CarePermissionRevocation>()
                .Select(value => (value.CommandId, value.WorldSequence, value.CommandOrdinal)));
}
