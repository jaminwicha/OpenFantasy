using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Progression;

internal static class ProgressionValidation
{
    internal static void Text(string value, int maximumBytes, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (CanonicalEncodingV1.Text(value, parameterName).Length > maximumBytes)
        {
            throw new ArgumentException("Progression text exceeds its finite encoding bound.", parameterName);
        }
    }

    internal static ReadOnlyCollection<T> Sorted<T>(IEnumerable<T> source, Func<T, EntityId> identity,
        int maximum, string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        SortedDictionary<EntityId, T> values = [];
        foreach (T value in source)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);
            if (values.Count == maximum || !values.TryAdd(identity(value), value))
            {
                throw new ArgumentException("Progression collections require bounded unique identities.", parameterName);
            }
        }
        return Array.AsReadOnly(values.Values.ToArray());
    }
}
