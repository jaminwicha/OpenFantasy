using OpenFantasy.Core.Progression;

namespace OpenFantasy.Core.Tests.Progression;

internal static class ProgressionFixture
{
    internal static EntityId Id(ulong value) => new(0, value);
    internal static ActorStatProfileDefinition Squire() => new(Id(100), "human-squire.unequipped",
        "Human Squire (unequipped)", ActorStatProfileKind.Class, new(60, 10, 12, 4, 8, 8),
        new(9, 2, 3, 1, 2, 1), ActorStatModifiers.Zero);
    internal static ActorStatProfileDefinition Unassigned() => new(Id(101), "wren.unassigned",
        "Unassigned", ActorStatProfileKind.Unassigned, new(1, 0, 0, 0, 0, 1),
        ActorStatModifiers.Zero, ActorStatModifiers.Zero);
    internal static LevelProgressionDefinition Curve() => new(Id(110), "source.levels-1-10",
        [100, 282, 519, 800, 1118, 1469, 1852, 2262, 2700]);
    internal static CurrencyDefinition Gil() => new(Id(120), "gil", "Gil", "gil");
    internal static ActorRewardDefinition Reward(ulong experience = 420, ulong gil = 200) =>
        new(Id(130), "wren.saplings.reward", experience, Gil(), Id(200), gil);

    internal static ProgressionSnapshot Create(ActorProgressionState? player = null,
        ulong snapshotRevision = 0, ulong currencyRevision = 0, ulong reserveRevision = 0,
        ulong playerAccountRevision = 0, ulong reserveBalance = 200)
    {
        ActorStatProfileDefinition squire = player?.Profile ?? Squire();
        LevelProgressionDefinition curve = player?.Curve ?? Curve();
        ActorStatProfileDefinition unassigned = Unassigned();
        player ??= new ActorProgressionState(Id(10), 0, squire, curve, 1, 0, 60, 10);
        ActorProgressionState npc = new(Id(11), 0, unassigned, curve, 1, 0, 1, 0);
        CurrencyLedger currency = new(Id(1), Id(2), currencyRevision, Gil(), reserveBalance,
            [new(Id(200), CurrencyAccountKind.Reserve, null, reserveBalance, reserveRevision),
             new(Id(201), CurrencyAccountKind.Actor, Id(10), 0, playerAccountRevision),
             new(Id(202), CurrencyAccountKind.Actor, Id(11), 0, 0)]);
        return new ProgressionSnapshot(Id(1), Id(2), snapshotRevision,
            [squire, unassigned], [curve], [player, npc], currency);
    }
}
