namespace OpenFantasy.Core.Custody;

public enum CustodyFormatFailure
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

/// <summary>A snapshot parsing failure; it does not describe a rejected gameplay command.</summary>
public sealed class CustodyFormatException(CustodyFormatFailure failure, string message) : FormatException(message)
{
    public CustodyFormatFailure Failure { get; } = failure;
}
