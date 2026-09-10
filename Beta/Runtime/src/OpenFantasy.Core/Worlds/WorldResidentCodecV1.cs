using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;
using static OpenFantasy.Core.Worlds.WorldSpatialCodecV1;

namespace OpenFantasy.Core.Worlds;

internal static class WorldResidentCodecV1
{
    internal const int MaximumResidentByteLength = 64 * 1024;

    internal static byte[] Encode(WorldResident resident) => CanonicalEncodingV1.Frame("world.resident",
        resident.ActorId.ToByteArray(), resident.InventoryCustodianId.ToByteArray(),
        CanonicalEncodingV1.Text(resident.DisplayName, nameof(resident.DisplayName)), Number((ulong)resident.ControllerKind),
        Signed(resident.HomeFeet.X), Signed(resident.HomeFeet.Y), Signed(resident.HomeFeet.Z),
        Number((ulong)resident.WanderRadiusCells), LanguageContractEncodingV1.EncodeProfile(resident.Comprehension),
        Number(resident.ExperiencedTime.Days), Number(resident.ExperiencedTime.MillisecondsWithinDay),
        resident.OpeningUtterance is { } utterance ? EncodeUtterance(utterance) : []);

    internal static WorldResident Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumResidentByteLength, 13);
        reader.Tag("world.resident"u8);
        EntityId actor = reader.Identity();
        EntityId inventory = reader.Identity();
        string name = reader.Text(128);
        ulong rawKind = reader.Number();
        if (rawKind is not 1 and not 2)
        {
            throw new FormatException("Unsupported resident controller kind.");
        }
        var home = new SubunitPosition(reader.Signed(), reader.Signed(), reader.Signed());
        ulong wander = reader.Number();
        if (wander > 8)
        {
            throw new FormatException("The admitted home amble exceeds this profile.");
        }
        ActorComprehensionProfile language = LanguageContractDecodingV1.DecodeProfile(
            reader.Field(LanguageContractDecodingV1.MaximumProfileByteLength));
        ulong exposureDays = reader.Number();
        ulong exposureMilliseconds = reader.Number();
        if (exposureMilliseconds >= LocalExposureDuration.MillisecondsPerDay)
        {
            throw new FormatException("The resident exposure is noncanonical.");
        }
        ReadOnlySpan<byte> utteranceBytes = reader.Field(2048);
        ResidentUtterance? utterance = utteranceBytes.IsEmpty ? null : DecodeUtterance(utteranceBytes);
        reader.Complete();
        return new WorldResident(actor, inventory, name, (ResidentControllerKind)rawKind, home, (int)wander,
            language, new LocalExposureDuration(exposureDays, (uint)exposureMilliseconds), utterance);
    }

    internal static byte[] EncodeUtterance(ResidentUtterance utterance) => CanonicalEncodingV1.Frame("world.utterance",
        utterance.DefinitionId.ToByteArray(), utterance.Channel.LanguageId.ToByteArray(),
        utterance.Channel.RegisterId.ToByteArray(), Number((ulong)utterance.Channel.Mode),
        utterance.ChannelVersion.ToByteArray(), CanonicalEncodingV1.Text(utterance.Text, nameof(utterance.Text)));

    private static ResidentUtterance DecodeUtterance(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 2048, 7);
        reader.Tag("world.utterance"u8);
        EntityId id = reader.Identity();
        EntityId language = reader.Identity();
        EntityId register = reader.Identity();
        ulong mode = reader.Number();
        if (mode is < 1 or > 3)
        {
            throw new FormatException("Unsupported utterance channel mode.");
        }
        var version = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
        string text = reader.Text(1024);
        reader.Complete();
        return new ResidentUtterance(id, new LanguageChannelKey(language, register, (CommunicationMode)mode), version, text);
    }
}
