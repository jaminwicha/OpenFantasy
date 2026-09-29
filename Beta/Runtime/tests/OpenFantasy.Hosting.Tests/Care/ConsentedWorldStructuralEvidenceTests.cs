using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Hosting.Tests.Care.WorldResourceJourneyTests;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Evidence_cannot_describe_reversed_actor_order_or_two_actions_from_one_actor(bool repeatedActor)
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        fixture.Journey.Approach(session);
        for (int index = 0; index < (repeatedActor ? 2 : 1); index++)
        {
            var before = session.ReadStateForHost();
            var pair = Pair(before, fixture.Journey, RawTerms(before, fixture.Helper, fixture.Player));
            CareRewardJourney.Commit(session, fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep,
                [pair.Offer, pair.Acceptance]));
        }
        byte[] original = WorldSnapshotCodec.Encode(session.ReadStateForHost());
        var root = ReadFrame(original);
        var coordination = ReadFrame(root[22]);
        var history = ReadFrame(coordination[4]);
        var first = ReadFrame(history[1]);
        if (repeatedActor)
        {
            var second = ReadFrame(history[2]);
            first[1] = second[1];
            // Keep distinct positions and increasing offer ordinals, but the same two actors
            // could not have submitted twice in this single world batch.
            bool senderFirst = fixture.Helper.CompareTo(fixture.Player) < 0;
            first[2] = Number(senderFirst ? 0UL : 1UL);
            first[3] = Number(senderFirst ? 1UL : 0UL);
            second[2] = Number(senderFirst ? 2UL : 3UL);
            second[3] = Number(senderFirst ? 3UL : 2UL);
            history[2] = WriteFrame(second);
        }
        else
        {
            (first[2], first[3]) = (first[3], first[2]);
        }
        history[1] = WriteFrame(first);
        coordination[4] = WriteFrame(history);
        root[22] = WriteFrame(coordination);
        var refusal = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(WriteFrame(root)));
        Assert.Contains("Consent evidence action positions must follow unique actor order within each world sequence.",
            refusal.ToString());
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(original)));
    }

    [Fact]
    public void Raw_evidence_cannot_claim_unissued_rights_from_an_admitted_gathering_source()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        fixture.Journey.Approach(session);
        var before = session.ReadStateForHost();
        var pair = Pair(before, fixture.Journey, RawTerms(before, fixture.Helper, fixture.Player));
        var gifted = CareRewardJourney.Commit(session, fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep,
            [pair.Offer, pair.Acceptance])).Candidate;
        var node = gifted.Gathering!.Nodes.First(value => value.NextUnissuedUnit
            < gifted.Inventory.Sources.Single(source => source.SourceId == value.SourceId).UnitCapacity);
        var source = gifted.Inventory.Sources.Single(value => value.SourceId == node.SourceId);
        byte[] original = WorldSnapshotCodec.Encode(gifted);
        var root = ReadFrame(original);
        var coordination = ReadFrame(root[22]);
        var history = ReadFrame(coordination[4]);
        var record = ReadFrame(history[1]);
        var command = ConsentedMaterialCodecV1.DecodeCommand(record[4]);
        var terms = command.Terms;
        var altered = new ConsentedMaterialCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
            new(terms.WorldId, terms.BranchId, terms.SenderActorId, terms.RecipientActorId, terms.LotKind,
                terms.LotId, source.Definition, terms.SourceCustodianId, terms.DestinationCustodianId,
                terms.Quantity, terms.ExpectedLotRevision));
        var acceptance = ConsentedMaterialCodecV1.DecodeAcceptance(record[7]);
        record[4] = ConsentedMaterialCodecV1.Encode(altered);
        record[7] = ConsentedMaterialCodecV1.Encode(ConsentedMaterialAcceptance.Approving(altered,
            acceptance.AcceptanceId, acceptance.PrincipalId, acceptance.OriginSessionId));
        record[11] = source.SourceId.ToByteArray();
        record[13] = Number(node.NextUnissuedUnit);
        record[14] = Number(node.NextUnissuedUnit + terms.Quantity);
        Assert.True(node.NextUnissuedUnit + terms.Quantity <= source.UnitCapacity);
        history[1] = WriteFrame(record);
        coordination[4] = WriteFrame(history);
        Assert.Single(WorldConsentedMaterialCodec.Decode(coordination[4]).Transfers);
        root[22] = WriteFrame(coordination);
        var error = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(WriteFrame(root)));
        Assert.Equal("Raw transfer evidence cannot exceed actual source issuance.",
            Assert.IsType<ArgumentException>(error.InnerException).Message);
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(original)));
    }
}
