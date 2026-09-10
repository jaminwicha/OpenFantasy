using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Closed dispatch for the retained migration frames. A failed decoder never selects another interpreter.</summary>
internal sealed class WorldProfileMigrationReplay
{
    private readonly WorldMigrationPreparation _production = new();
    private readonly WorldQuestMigrationPreparation _quests = new();
    private readonly WorldConstructionMigrationPreparation _construction = new();

    internal ExpectedJournalEntry Prepare(StoredWorldCommit stored, WorldSnapshot state,
        WorldIdentityIndex genesisIdentities, ContentHash stateHash, Func<EntityId, bool> historicallyOccupied)
    {
        ReadOnlySpan<byte> tag = ReadTag(stored.AcceptedBatch);
        if (tag.SequenceEqual("world.production-migration"u8))
        {
            WorldProductionMigration request = WorldProductionMigrationCodecV1.Decode(stored.AcceptedBatch);
            PreparedWorldMigration expected = _production.Prepare(state, genesisIdentities,
                stateHash, request, historicallyOccupied);
            return new(expected.Decision.Candidate, expected.CandidateBytes, expected.Stored, [], expected.Identities);
        }
        if (tag.SequenceEqual("world.quest-migration"u8))
        {
            WorldQuestMigration request = WorldQuestMigrationCodecV1.Decode(stored.AcceptedBatch);
            PreparedWorldQuestMigration expected = _quests.Prepare(state, genesisIdentities,
                stateHash, request, historicallyOccupied);
            return new(expected.Decision.Candidate, expected.CandidateBytes, expected.Stored, [], expected.Identities);
        }
        if (tag.SequenceEqual("world.construction-migration"u8))
        {
            WorldConstructionMigration request = WorldConstructionMigrationCodecV1.Decode(stored.AcceptedBatch);
            PreparedWorldConstructionMigration expected = _construction.Prepare(state, genesisIdentities,
                stateHash, request, historicallyOccupied);
            return new(expected.Decision.Candidate, expected.CandidateBytes, expected.Stored, [], expected.Identities);
        }
        throw new InvalidDataException("The stored profile migration tag is unsupported.");
    }

    private static ReadOnlySpan<byte> ReadTag(ReadOnlySpan<byte> bytes)
    {
        const int frameHeaderLength = 12;
        const int lengthPrefixLength = 4;
        const int tagOffset = frameHeaderLength + lengthPrefixLength;
        if (bytes.Length < tagOffset || bytes.Length > WorldDatabase.MaximumBatchByteLength
            || !bytes[..4].SequenceEqual("OFC\0"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]) != 1
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) < 2)
        {
            throw new InvalidDataException("The stored migration has an invalid canonical frame header.");
        }
        uint tagLength = BinaryPrimitives.ReadUInt32BigEndian(bytes[frameHeaderLength..]);
        if (tagLength is 0 or > 64 || tagLength > (uint)(bytes.Length - tagOffset))
        {
            throw new InvalidDataException("The stored migration tag exceeds its frame.");
        }
        // The selected strict decoder validates every remaining field, version and canonical byte.
        return bytes.Slice(tagOffset, checked((int)tagLength));
    }
}
