using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// One actor's retained withdrawal choice and walked prefix. This is evidence of a commitment,
/// not a second owner of position or readiness, and construction does not authenticate the command.
/// </summary>
public sealed class EncounterWithdrawalMember
{
    public EncounterWithdrawalMember(WorldActionCommand command, SubunitPosition origin,
        uint normalizedSpeed, int samplesWalked = 0)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfZero(normalizedSpeed);
        if (command.Intent is not QueueEncounterRetreatIntent intent
            || samplesWalked < 0 || samplesWalked > intent.Route.Count
            || QueueEncounterRetreatIntent.Endpoint(origin, intent.Route) != intent.Destination
            || WorldActionCodec.Encode(command).Length > EncounterRetreatState.MaximumCommandByteLength)
        {
            throw new ArgumentException("Withdrawal retains an exact bounded route, origin and walked prefix.");
        }
        Command = command;
        Intent = intent;
        Origin = origin;
        NormalizedSpeed = normalizedSpeed;
        SamplesWalked = samplesWalked;
    }

    public WorldActionCommand Command { get; }
    public QueueEncounterRetreatIntent Intent { get; }
    public EntityId ActorId => Command.ActorId;
    public SubunitPosition Origin { get; }
    public uint NormalizedSpeed { get; }
    public int SamplesWalked { get; }
    public bool HasArrived => SamplesWalked == Intent.Route.Count;
    public ulong SampleReadiness => EncounterRetreatRules.SampleDelay(NormalizedSpeed);
    public ulong WalkedReadiness => checked((ulong)SamplesWalked * SampleReadiness);
    public SubunitPosition ExpectedFeet => QueueEncounterRetreatIntent.Endpoint(Origin, Intent.Route.Take(SamplesWalked));

    /// <summary>The next physical sample, never a teleport to the final destination.</summary>
    public SubunitPosition NextFeet => !HasArrived
        ? QueueEncounterRetreatIntent.Endpoint(ExpectedFeet, [Intent.Route[SamplesWalked]])
        : throw new InvalidOperationException("An arrived member has no unwalked sample.");

    internal EncounterWithdrawalMember RecordMoved(SubunitPosition from, SubunitPosition to)
    {
        if (HasArrived || from != ExpectedFeet || to != NextFeet)
        {
            throw new ArgumentException("Withdrawal progress records exactly its next ordinary movement sample.");
        }
        return new(Command, Origin, NormalizedSpeed, checked(SamplesWalked + 1));
    }
}
