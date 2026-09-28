namespace OpenFantasy.Core.Care;

/// <summary>
/// Watermark of one closed care boundary: the highest world sequence occupied by any archived, carried or
/// persisted operation. Aftermath operations own their whole sequence and a closure never splits a
/// publication, so every later operation needs a strictly greater world sequence.
/// </summary>
public readonly record struct CareContinuationPosition(ulong WorldSequence)
{
    public bool Closes(ulong worldSequence) => worldSequence <= WorldSequence;
}
