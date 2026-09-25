using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Host;

/// <summary>Inspects bounded timing proposals without admitting history or publishing world effects.</summary>
internal sealed class EncounterTimingOperator
{
    internal string Inspect(string path, string world, string branch, string encounter)
    {
        var expected = new EncounterTimingScope(EntityId.Parse(world), EntityId.Parse(branch), EntityId.Parse(encounter));
        byte[] bytes = ReadBoundedFile(path);
        EncounterTimingSnapshot clock = EncounterTimingCodecV1.Decode(bytes, expected);
        return JsonSerializer.Serialize(new
        {
            Contract = "encounter-timing-v1",
            Scope = "standalone-timing-diagnostic",
            Authority = "Caller-supplied scope, participants, debt and action pins are not authenticated world history.",
            WorldConsequencesPublished = false,
            InputHash = ContentHash.Compute(bytes).Value,
            Initial = Describe(clock),
            Advance = Propose(() => clock.TryAdvance(clock.Revision)),
            Confirmation = Propose(() => clock.TryConfirmNext(clock.Revision))
        });
    }

    private static object Propose(Func<EncounterTimingTransition> calculate)
    {
        try
        {
            EncounterTimingTransition result = calculate();
            return new
            {
                Available = true,
                Status = result.Status.ToString(),
                result.IsAccepted,
                ExperiencedMilliseconds = result.Experienced.MillisecondsWithinDay,
                ConfirmedDecision = result.ConfirmedDecision?.DecisionId.ToString(),
                State = Describe(result.State),
                ArchiveBase64 = Convert.ToBase64String(EncounterTimingCodecV1.Encode(result.State))
            };
        }
        catch (OverflowException)
        {
            return new { Available = false, Refusal = "Timing counter capacity exhausted; no proposal was published." };
        }
    }

    private static object Describe(EncounterTimingSnapshot clock) => new
    {
        World = clock.WorldId.ToString(), Branch = clock.BranchId.ToString(), Encounter = clock.EncounterId.ToString(),
        clock.Revision, clock.ElapsedTacticalUnits, Hash = clock.ContentHash.Value,
        RequiredDecisions = clock.RequiredDecisions.Select(actor => actor.ActorId.ToString()).ToArray(),
        DueActors = clock.DueActors.Select(actor => actor.ActorId.ToString()).ToArray(),
        Actors = clock.Actors.Select(actor => new
        {
            Id = actor.ActorId.ToString(), actor.NormalizedSpeed, actor.RemainingDelay,
            actor.StandardDelay, Control = actor.Control.ToString()
        }).ToArray(),
        Decisions = clock.Decisions.Select(decision => new
        {
            Actor = decision.ActorId.ToString(), Id = decision.DecisionId.ToString(),
            Kind = decision.Kind.ToString(), Pin = decision.ActionPin.Value
        }).ToArray()
    };

    private static byte[] ReadBoundedFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        long length = stream.Length;
        if (length > EncounterTimingCodecV1.MaximumEncodedByteLength)
        {
            throw new InvalidDataException("Encounter timing input exceeds its byte limit.");
        }
        byte[] bytes = new byte[checked((int)length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Encounter timing input changed length while reading.");
        }
        return bytes;
    }
}
