namespace OpenFantasy.Core.Content;

/// <summary>A stable, ordinal package namespace, independent of filenames and load order.</summary>
public sealed record PackageId
{
    public const int MaximumLength = 128;
    public string Value { get; }

    public PackageId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length is 0 or > MaximumLength)
        {
            throw new ArgumentException("A package namespace requires 1 to 128 characters.", nameof(value));
        }

        bool startsSegment = true;
        foreach (char character in value)
        {
            if (startsSegment)
            {
                if (character is < 'a' or > 'z')
                {
                    throw new ArgumentException("Each package namespace segment starts with a lowercase ASCII letter.", nameof(value));
                }
                startsSegment = false;
            }
            else if (character == '.')
            {
                startsSegment = true;
            }
            else if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-'))
            {
                throw new ArgumentException("Package namespaces use lowercase ASCII letters, digits, dots, underscores and hyphens.", nameof(value));
            }
        }

        if (startsSegment)
        {
            throw new ArgumentException("A package namespace cannot end with an empty segment.", nameof(value));
        }
        Value = value;
    }

    public override string ToString() => Value;
}
