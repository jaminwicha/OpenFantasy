using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Production;

internal static class ProductionCollections
{
    internal static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values, int maximum, Func<T, EntityId> identity) where T : class
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new SortedDictionary<EntityId, T>();
        foreach (T value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (result.Count == maximum || !result.TryAdd(identity(value), value))
            {
                throw new ArgumentException("Production entries must be bounded and have unique identities.", nameof(values));
            }
        }
        return Array.AsReadOnly(result.Values.ToArray());
    }
}
