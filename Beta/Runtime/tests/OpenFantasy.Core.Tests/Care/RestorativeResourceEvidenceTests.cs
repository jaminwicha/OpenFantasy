using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Progression.ProgressionFixture;

namespace OpenFantasy.Core.Tests.Care;

public sealed class RestorativeResourceEvidenceTests
{
    [Theory]
    [InlineData(false, IngredientLotKind.Source)]
    [InlineData(true, IngredientLotKind.Transformed)]
    public void Tagged_ordinary_evidence_preserves_exact_selected_request_and_ordered_capped_effect(bool assisted, IngredientLotKind kind)
    {
        // Structural codec evidence deliberately starts at an already-damaged component basis.
        // This is not an admitted world, real inventory, or authenticated permission history.
        var damaged = Create().GetActor(Id(10)).TakeDamage(13).Candidate;
        var basis = Create(damaged, snapshotRevision: 1);
        EntityId commandId = Id(1_000);
        EntityId useId = EntityId.DeriveSpawned(commandId, 1);
        var intent = Intent(assisted, kind, damaged.Revision);
        var request = new WorldActionCommand(commandId, Id(20), Id(30), assisted ? Id(11) : Id(10),
            basis.WorldId, basis.BranchId, 7, intent);
        var evidence = new WorldRestorativeUseResourceEvidence(request, useId);
        var healed = damaged.RestoreHealth(40);
        var batch = new ResourceChangeBatch(9, 3, commandId,
            [ActorResourceChange.FromTransition(0, new(useId, Id(301), ContentHash.Compute("remedy pin"u8)), healed)]);
        var history = new WorldResourceHistory(ContentHash.Compute("caller-admitted component basis"u8),
            new ActorResourceHistory(basis, 8, [batch]), [evidence]);
        byte[] bytes = WorldResourceHistoryCodec.Encode(history);
        Assert.True(bytes.AsSpan().IndexOf("world.resource-restorative-use.v1"u8) >= 0);
        WorldResourceHistory restored = WorldResourceHistoryCodec.Decode(bytes);
        var retained = Assert.IsType<WorldRestorativeUseResourceEvidence>(Assert.Single(restored.Evidence));
        Assert.Equal(useId, retained.UseId);
        Assert.Equal(commandId, retained.CommandId);
        Assert.Equal(WorldActionCodec.Encode(request), WorldActionCodec.Encode(retained.Request));
        Assert.Equal(intent, Assert.IsType<UseRestorativeItemIntent>(retained.Request.Intent));
        var actual = Assert.Single(restored.History.Batches);
        Assert.Equal(9UL, actual.WorldSequence);
        Assert.Equal(3U, actual.CommandOrdinal);
        var change = Assert.Single(actual.Changes);
        Assert.Equal(47, change.Before.CurrentHp);
        Assert.Equal(60, change.After.CurrentHp);
        Assert.Equal(40UL, change.RequestedAmount);
        Assert.Equal(change.Before.CurrentMp, change.After.CurrentMp);
        Assert.Equal(bytes, WorldResourceHistoryCodecV1.Encode(restored));
        Assert.Equal(bytes, WorldResourceHistoryCodec.Encode(restored));

        var wrongTag = bytes.ToArray();
        int tag = wrongTag.AsSpan().IndexOf("world.resource-restorative-use.v1"u8);
        wrongTag[tag] = (byte)'?';
        Assert.Throws<FormatException>(() => WorldResourceHistoryCodec.Decode(wrongTag));
        Assert.Throws<FormatException>(() => WorldResourceHistoryCodec.Decode(bytes.AsSpan(0, bytes.Length - 1)));
        Assert.Throws<FormatException>(() => WorldResourceHistoryCodec.Decode([.. bytes, 0]));
        Assert.Equal(bytes, WorldResourceHistoryCodec.Encode(history));
    }

    [Fact]
    public void Ordinary_evidence_cannot_mislabel_a_different_action_or_generated_identity()
    {
        var wrongAction = new WorldActionCommand(Id(1_000), Id(20), Id(30), Id(10), Id(1), Id(2), 0,
            new SpeakToActorIntent(Id(11), 0));
        Assert.Throws<ArgumentException>(() => new WorldRestorativeUseResourceEvidence(wrongAction,
            EntityId.DeriveSpawned(wrongAction.CommandId, 1)));
        var ordinary = new WorldActionCommand(Id(1_001), Id(20), Id(30), Id(10), Id(1), Id(2), 0,
            Intent(false, IngredientLotKind.Source, 0));
        Assert.Throws<ArgumentException>(() => new WorldRestorativeUseResourceEvidence(ordinary,
            EntityId.DeriveSpawned(ordinary.CommandId, 0))); // Consumption is not the completed use.
        Assert.Throws<ArgumentException>(() => new WorldRestorativeUseResourceEvidence(ordinary,
            EntityId.DeriveSpawned(Id(1_002), 1)));
    }

    private static UseRestorativeItemIntent Intent(bool assisted, IngredientLotKind kind, ulong patientRevision) => new(
        Id(10), Id(300), ContentHash.Compute("ordinary delivery pin"u8), assisted ? Id(302) : null,
        assisted ? ContentHash.Compute("original permission grant"u8) : null, assisted ? 0UL : null,
        4, patientRevision, 5, 6, new(kind, Id(400), 2, 1));
}
