namespace OpenFantasy.Core.Commands;

public enum CommandFormatFailure
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

/// <summary>Bounded parsing diagnostics only; never a durable gameplay rejection receipt.</summary>
public sealed class CommandFormatException : FormatException
{
    public CommandFormatException(CommandFormatFailure failure, string message) : base(message)
    {
        Failure = failure;
    }

    public CommandFormatFailure Failure { get; }
}
