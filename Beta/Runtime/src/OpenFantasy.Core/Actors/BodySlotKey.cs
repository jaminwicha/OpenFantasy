namespace OpenFantasy.Core.Actors;

/// <summary>A stable local anatomy slot key, scoped by its versioned body definition.</summary>
public sealed record BodySlotKey
{
    public const int MaximumLength = 64;

    public BodySlotKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length is < 1 or > MaximumLength || value[0] is < 'a' or > 'z'
            || value.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')))
        {
            throw new ArgumentException("A body slot requires a lowercase ASCII local key beginning with a letter.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}
