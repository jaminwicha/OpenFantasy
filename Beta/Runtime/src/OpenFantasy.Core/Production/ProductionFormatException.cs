namespace OpenFantasy.Core.Production;

public enum ProductionFormatFailure
{
    LimitExceeded,
    Truncated,
    InvalidFrame,
    UnsupportedVersion,
    UnknownType,
    InvalidValue,
    NonCanonical,
    TrailingData
}

/// <summary>Definition-format diagnostics only; not a gameplay transaction result.</summary>
public sealed class ProductionFormatException : FormatException
{
    public ProductionFormatException(ProductionFormatFailure failure, string message) : base(message)
    {
        Failure = failure;
    }

    public ProductionFormatFailure Failure { get; }
}
