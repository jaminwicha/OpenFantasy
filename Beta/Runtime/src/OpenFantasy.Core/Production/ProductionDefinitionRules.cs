namespace OpenFantasy.Core.Production;

internal static class ProductionDefinitionRules
{
    internal const int MaximumKeyLength = 128;

    internal static void ValidateKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length is 0 or > MaximumKeyLength || key[0] is < 'a' or > 'z')
        {
            throw new ArgumentException("A definition key starts with a lowercase ASCII letter and has at most 128 characters.", nameof(key));
        }
        foreach (char character in key)
        {
            if (character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-'))
            {
                throw new ArgumentException("Definition keys use lowercase ASCII letters, digits, underscores and hyphens.", nameof(key));
            }
        }
    }
}
