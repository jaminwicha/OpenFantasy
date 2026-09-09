namespace OpenFantasy.Core.Authority;

public enum AuthorityFormatFailure
{
    InvalidFrame,
    Truncated,
    UnsupportedVersion,
    UnknownType,
    LimitExceeded,
    InvalidValue,
    NonCanonical,
    TrailingData
}

public sealed class AuthorityFormatException(AuthorityFormatFailure failure, string message) : FormatException(message)
{
    public AuthorityFormatFailure Failure { get; } = failure;
}
