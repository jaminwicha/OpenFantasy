using System.Text;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;

namespace OpenFantasy.Hosting.Content;

/// <summary>Verifies the three selected records against both the retained packet and original file witnesses.</summary>
internal sealed class EncounterFarmSources
{
    private const string Packet = "legacy/aldermere-first-loop/";
    private const string RecordsHash = "9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b";
    private const string WorldHash = "fc55db3ec2c8a9602e0432a7359e460b8b6a6e92a4123620df4df5190eaea4e2";
    private const string ConstantsHash = "7bdcec4fa6f7017bab1cb9c7ff028ee7e28b15ae37bc4503174f91b89e431742";

    private EncounterFarmSources(JsonElement strike, JsonElement squire, JsonElement wolf)
    {
        Strike = strike;
        Squire = squire;
        Wolf = wolf;
        Version = ContentHash.Compute(Encoding.UTF8.GetBytes("encounter-farm.sources.v1")
            .Concat(new ContentHash(RecordsHash).ToByteArray()).Concat(new ContentHash(WorldHash).ToByteArray())
            .Concat(new ContentHash(ConstantsHash).ToByteArray()).Concat(StrikeVersion.ToByteArray())
            .Concat(FarmCanonicalJson.Hash(Squire).ToByteArray()).Concat(WolfVersion.ToByteArray()).ToArray());
    }

    internal JsonElement Strike { get; }
    internal JsonElement Squire { get; }
    internal JsonElement Wolf { get; }
    internal ContentHash StrikeVersion => FarmCanonicalJson.Hash(Strike);
    internal ContentHash WolfVersion => FarmCanonicalJson.Hash(Wolf);
    internal ContentHash Version { get; }

    internal static EncounterFarmSources Load(FarmContentFiles files)
    {
        using JsonDocument records = FarmContentFiles.Parse(files.Read(Packet + "records.json", 65_536, RecordsHash));
        JsonElement root = records.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("packageId").GetString() != "openfantasy.aldermere"
            || root.GetProperty("records").GetArrayLength() != 49)
        {
            throw new InvalidDataException("The encounter source packet has an incompatible identity or shape.");
        }
        // These bounded, hash-checked witnesses pin the range grace, unit scale and damage rule;
        // the new clock/random-stream adaptation is explicit in the admission contract.
        files.Read(Packet + "source-support/src/Sim/World.cs.txt", 27_230, WorldHash);
        files.Read(Packet + "source-support/src/Sim/SimConstants.cs.txt", 2_216, ConstantsHash);
        JsonElement strike = Read(files, root, "abilities/strike", "data/abilities.json", "abilities", 0,
            "5cb75f014fef62fdf96d0597e9f51508737c1366ca7f341d5621ad9ca3d764d6",
            "974a3c16135eddfc9679deb3929c862476c78b62f38fc79cb7253aba7e02f70a");
        JsonElement squire = Read(files, root, "classes/squire", "data/classes.json", "classes", 0,
            "27c5956e9b269acdc36ecba339522c426380ea2f35bc33fead49ae778add6234",
            "b6defa5b6c7a49d13fe3297938c41952c78b22de5ba2187138654d84b2507cf9");
        JsonElement wolf = Read(files, root, "monsters/bramble_wolf", "data/monsters.json", "monsters", 1,
            "8a3599545e5ea3e1a8ecfdf396b4bd107d2f2f3c696fcaaa2ed76d9dab511493",
            "d8639a45b5219163e148fde39537388df45db56148bfcaaa8ebb58ea52e6d967");
        return new(strike, squire, wolf);
    }

    internal void ValidatePlayer(ActorProgressionState player)
    {
        JsonElement growth = Squire.GetProperty("growth");
        var expectedGrowth = new ActorStatModifiers(growth.GetProperty("hp").GetInt32(), growth.GetProperty("mp").GetInt32(),
            growth.GetProperty("attack").GetInt32(), growth.GetProperty("magic").GetInt32(),
            growth.GetProperty("defense").GetInt32(), growth.GetProperty("speed").GetInt32());
        if (player.Level != 1 || player.Experience != 0 || player.Profile.Kind != ActorStatProfileKind.Class
            || player.Profile.BaseStats != Stats(Squire.GetProperty("baseStats"))
            || player.Profile.Growth != expectedGrowth || player.Profile.HeritageModifiers != ActorStatModifiers.Zero
            || !Squire.GetProperty("abilities").EnumerateArray().Any(value => value.GetString() == "strike"))
        {
            throw new InvalidDataException("The encounter requires the retained fresh human Squire and its actual Strike grant.");
        }
    }

    internal static ActorStatValues Stats(JsonElement value) => new(value.GetProperty("hp").GetInt32(),
        value.GetProperty("mp").GetInt32(), value.GetProperty("attack").GetInt32(), value.GetProperty("magic").GetInt32(),
        value.GetProperty("defense").GetInt32(), value.GetProperty("speed").GetInt32());

    private static JsonElement Read(FarmContentFiles files, JsonElement records, string key, string path,
        string collection, int index, string fileHash, string payloadHash)
    {
        JsonElement record = records.GetProperty("records").EnumerateArray()
            .Single(value => value.GetProperty("targetKey").GetString() == "openfantasy.aldermere:" + key);
        JsonElement identity = record.GetProperty("sourceIdentity");
        JsonElement payload = record.GetProperty("sourcePayload");
        string pointer = "/" + collection + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using JsonDocument original = FarmContentFiles.Parse(files.Read(Packet + "source-support/" + path, 32_768, fileHash));
        if (identity.GetProperty("path").GetString() != path || identity.GetProperty("jsonPointer").GetString() != pointer
            || identity.GetProperty("inventoryId").GetString() != path + "#" + pointer
            || identity.GetProperty("payloadSha256").GetString() != payloadHash
            || FarmCanonicalJson.Hash(payload).Value != payloadHash
            || FarmCanonicalJson.Hash(original.RootElement.GetProperty(collection)[index]).Value != payloadHash)
        {
            throw new InvalidDataException("The selected combat record does not match its retained source identity and payload.");
        }
        return payload.Clone();
    }
}
