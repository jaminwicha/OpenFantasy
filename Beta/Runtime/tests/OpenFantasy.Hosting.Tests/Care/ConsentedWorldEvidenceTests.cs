using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Hosting.Tests.Care.WorldResourceJourneyTests;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    private static void AssertInvalidEvidenceRejectedByWorld(WorldSnapshot gifted)
    {
        byte[] original = WorldSnapshotCodec.Encode(gifted);
        const string mismatch = "Custody evidence must retain the actual producer and exact moved prefix.";
        for (int variant = 0; variant < 4; variant++)
        {
            var root = ReadFrame(original);
            var coordination = ReadFrame(root[22]);
            var history = ReadFrame(coordination[4]);
            if (variant == 0)
            {
                coordination[4] = WorldConsentedMaterialCodec.Encode(new WorldConsentedMaterialHistory([]));
            }
            else
            {
                var record = ReadFrame(history[1]);
                if (variant == 1)
                {
                    // Same quantity, different prefix: no owner's stock, total or hash is altered.
                    record[13] = Number(1);
                    record[14] = Number(4);
                }
                else if (variant == 2) record[11] = new EntityId(0xc028000000000000, 9999).ToByteArray();
                else
                {
                    // Both parties consistently approve a different admitted definition. The
                    // unchanged producing batch and actual lot must still bind that claim.
                    var prior = ConsentedMaterialCodecV1.DecodeCommand(record[4]);
                    var terms = prior.Terms;
                    var different = gifted.Inventory.Custody.Lots.First(value => value.Definition != terms.Definition).Definition;
                    var altered = new ConsentedMaterialCommand(prior.CommandId, prior.PrincipalId, prior.OriginSessionId,
                        new(terms.WorldId, terms.BranchId, terms.SenderActorId, terms.RecipientActorId, terms.LotKind,
                            terms.LotId, different, terms.SourceCustodianId, terms.DestinationCustodianId,
                            terms.Quantity, terms.ExpectedLotRevision));
                    var accepted = ConsentedMaterialCodecV1.DecodeAcceptance(record[7]);
                    record[4] = ConsentedMaterialCodecV1.Encode(altered);
                    record[7] = ConsentedMaterialCodecV1.Encode(ConsentedMaterialAcceptance.Approving(altered,
                        accepted.AcceptanceId, accepted.PrincipalId, accepted.OriginSessionId));
                }
                history[1] = WriteFrame(record);
                coordination[4] = WriteFrame(history);
                // It remains a structurally valid approval/history. Complete admission must bind it.
                Assert.Single(WorldConsentedMaterialCodec.Decode(coordination[4]).Transfers);
            }
            root[22] = WriteFrame(coordination);
            var error = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(WriteFrame(root)));
            var admission = Assert.IsType<ArgumentException>(error.InnerException);
            Assert.Equal(variant == 0
                ? "Live produced lots must equal the partition replayed from retirements and ordered custody evidence."
                : variant == 3 ? "Custody evidence must retain the actual item definition and version."
                : mismatch, admission.Message);
        }
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(original)));
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }
}
