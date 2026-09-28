namespace OpenFantasy.Hosting.Worlds;

/// <summary>Checks the backup's retained rows without materializing the complete journal again.</summary>
internal static class WorldBackupComparison
{
    internal static void RequireExactCopy(WorldDatabase source, WorldDatabase copy)
    {
        StoredWorldHead before = source.ReadHead();
        StoredWorldHead after = copy.ReadHead();
        if (source.SchemaVersion != copy.SchemaVersion || !source.ReadGenesis().AsSpan().SequenceEqual(copy.ReadGenesis())
            || before.Sequence != after.Sequence || before.AdvancingSteps != after.AdvancingSteps
            || !before.State.AsSpan().SequenceEqual(after.State))
        {
            throw new InvalidDataException("The staged backup changed a retained world boundary.");
        }
        RequireEqual(source.ReadJournal(), copy.ReadJournal(), (left, right) =>
            left.Sequence == right.Sequence && left.Kind == right.Kind && left.CauseId == right.CauseId
            && left.AdvancingSteps == right.AdvancingSteps && left.BeforeHash == right.BeforeHash && left.AfterHash == right.AfterHash
            && left.AcceptedBatch.AsSpan().SequenceEqual(right.AcceptedBatch) && left.Outcomes.AsSpan().SequenceEqual(right.Outcomes));
        RequireEqual(source.ReadCommandOutcomes(), copy.ReadCommandOutcomes(), (left, right) =>
            left.PrincipalId == right.PrincipalId && left.CommandId == right.CommandId && left.Sequence == right.Sequence
            && left.Command.AsSpan().SequenceEqual(right.Command) && left.Receipt.AsSpan().SequenceEqual(right.Receipt));
        RequireEqual(source.ReadIdentities(), copy.ReadIdentities(), (left, right) => left == right);
        RequireEqual(source.ReadCheckpoints(), copy.ReadCheckpoints(), (left, right) =>
            left.Sequence == right.Sequence && left.AdvancingSteps == right.AdvancingSteps
            && left.State.AsSpan().SequenceEqual(right.State));
        RequireEqual(source.ReadResourceArchives(), copy.ReadResourceArchives(), (left, right) =>
            left.Reference == right.Reference && left.PublicationSequence == right.PublicationSequence
            && left.State.AsSpan().SequenceEqual(right.State));
        RequireEqual(source.ReadMaterialCareArchives(), copy.ReadMaterialCareArchives(), (left, right) =>
            left.Reference == right.Reference && left.PublicationSequence == right.PublicationSequence
            && left.State.AsSpan().SequenceEqual(right.State));
    }

    private static void RequireEqual<T>(IEnumerable<T> source, IEnumerable<T> copy, Func<T, T, bool> equal)
    {
        using IEnumerator<T> left = source.GetEnumerator();
        using IEnumerator<T> right = copy.GetEnumerator();
        while (left.MoveNext())
        {
            if (!right.MoveNext() || !equal(left.Current, right.Current))
            {
                throw new InvalidDataException("The staged backup changed a retained journal row.");
            }
        }
        if (right.MoveNext())
        {
            throw new InvalidDataException("The staged backup contains additional journal rows.");
        }
    }
}
