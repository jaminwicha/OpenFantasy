namespace OpenFantasy.Core.Scheduling;

public enum SchedulingFormatFailure
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

public sealed class SchedulingFormatException : FormatException
{
    public SchedulingFormatException(SchedulingFormatFailure failure, string message) : base(message)
    {
        Failure = failure;
    }

    public SchedulingFormatFailure Failure { get; }
}
