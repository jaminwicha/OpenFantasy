using System.Buffers.Binary;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Progression;

/// <summary>Canonical bounded restoration of actual progression, definition pins and conserved balances.</summary>
public static class ProgressionCodecV1
{
    public const int MaximumSnapshotByteLength = 512 * 1024;
    public const int MaximumProfileByteLength = 2048;
    public const int MaximumCurveByteLength = 4096;
    public const int MaximumCurrencyByteLength = 1024;
    public const int MaximumRewardByteLength = 2048;
    public const int MaximumLedgerByteLength = 128 * 1024;
    private const int MaximumActorByteLength = 512;
    private const int MaximumAccountByteLength = 256;
    private const int MaximumCollectionByteLength = 192 * 1024;

    public static byte[] Encode(ProgressionSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Frame("progression.snapshot", Number(1), Id(state.WorldId), Id(state.BranchId), Number(state.Revision),
            Collection("progression.profiles", state.Profiles.Select(EncodeProfile)),
            Collection("progression.curves", state.Curves.Select(EncodeCurve)),
            Collection("progression.actors", state.Actors.Select(EncodeActor)), EncodeLedger(state.Currency));
    }

    public static ProgressionSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new ProgressionFrameReader(bytes, MaximumSnapshotByteLength, 9);
            reader.Tag("progression.snapshot"u8);
            reader.Version();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong revision = reader.Number();
            var profiles = new ProgressionFrameReader(reader.Field(MaximumCollectionByteLength),
                MaximumCollectionByteLength, ProgressionSnapshot.MaximumDefinitions + 1);
            profiles.Tag("progression.profiles"u8);
            List<ActorStatProfileDefinition> profileValues = [];
            while (profiles.RemainingFields != 0)
            {
                profileValues.Add(DecodeProfile(profiles.Field(MaximumProfileByteLength)));
            }
            profiles.Complete();
            var curves = new ProgressionFrameReader(reader.Field(MaximumCollectionByteLength),
                MaximumCollectionByteLength, ProgressionSnapshot.MaximumDefinitions + 1);
            curves.Tag("progression.curves"u8);
            List<LevelProgressionDefinition> curveValues = [];
            while (curves.RemainingFields != 0)
            {
                curveValues.Add(DecodeCurve(curves.Field(MaximumCurveByteLength)));
            }
            curves.Complete();
            var actors = new ProgressionFrameReader(reader.Field(MaximumCollectionByteLength),
                MaximumCollectionByteLength, ProgressionSnapshot.MaximumActors + 1);
            actors.Tag("progression.actors"u8);
            List<ActorProgressionState> actorValues = [];
            while (actors.RemainingFields != 0)
            {
                actorValues.Add(DecodeActor(actors.Field(MaximumActorByteLength), profileValues, curveValues));
            }
            actors.Complete();
            CurrencyLedger currency = DecodeLedger(reader.Field(MaximumLedgerByteLength));
            reader.Complete();
            var result = new ProgressionSnapshot(world, branch, revision, profileValues, curveValues, actorValues, currency);
            Canonical(bytes, Encode(result));
            return result;
        }
        catch (Exception error) when (IsInvalidValue(error))
        {
            throw InvalidValue(error);
        }
    }

    public static byte[] EncodeProfile(ActorStatProfileDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Frame("progression.stat-profile", Number(1), Id(definition.DefinitionId), Text(definition.Key),
            Text(definition.DisplayName), Number((ulong)definition.Kind), EncodeStats(definition.BaseStats),
            EncodeModifiers(definition.Growth), EncodeModifiers(definition.HeritageModifiers));
    }

    public static ActorStatProfileDefinition DecodeProfile(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new ProgressionFrameReader(bytes, MaximumProfileByteLength, 9);
            reader.Tag("progression.stat-profile"u8);
            reader.Version();
            EntityId id = reader.Identity();
            string key = reader.Text(128);
            string name = reader.Text(128);
            var kind = (ActorStatProfileKind)checked((int)reader.Number());
            ActorStatValues stats = DecodeStats(reader.Field(256));
            ActorStatModifiers growth = DecodeModifiers(reader.Field(256));
            ActorStatModifiers modifiers = DecodeModifiers(reader.Field(256));
            reader.Complete();
            var result = new ActorStatProfileDefinition(id, key, name, kind, stats, growth, modifiers);
            Canonical(bytes, EncodeProfile(result));
            return result;
        }
        catch (Exception error) when (IsInvalidValue(error))
        {
            throw InvalidValue(error);
        }
    }

    public static byte[] EncodeCurve(LevelProgressionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Frame("progression.level-curve", Number(1), Id(definition.DefinitionId), Text(definition.Key),
            Collection("progression.thresholds", definition.Thresholds.Select(Number)));
    }

    public static LevelProgressionDefinition DecodeCurve(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new ProgressionFrameReader(bytes, MaximumCurveByteLength, 5);
            reader.Tag("progression.level-curve"u8);
            reader.Version();
            EntityId id = reader.Identity();
            string key = reader.Text(128);
            var thresholds = new ProgressionFrameReader(reader.Field(MaximumCurveByteLength),
                MaximumCurveByteLength, LevelProgressionDefinition.MaximumSupportedLevel);
            thresholds.Tag("progression.thresholds"u8);
            List<ulong> values = [];
            while (thresholds.RemainingFields != 0)
            {
                values.Add(thresholds.Number());
            }
            thresholds.Complete();
            reader.Complete();
            var result = new LevelProgressionDefinition(id, key, values);
            Canonical(bytes, EncodeCurve(result));
            return result;
        }
        catch (Exception error) when (IsInvalidValue(error))
        {
            throw InvalidValue(error);
        }
    }

    public static byte[] EncodeCurrency(CurrencyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Frame("progression.currency", Number(1), Id(definition.DefinitionId), Text(definition.Key),
            Text(definition.DisplayName), Text(definition.UnitName));
    }

    public static CurrencyDefinition DecodeCurrency(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new ProgressionFrameReader(bytes, MaximumCurrencyByteLength, 6);
            reader.Tag("progression.currency"u8);
            reader.Version();
            var result = new CurrencyDefinition(reader.Identity(), reader.Text(128), reader.Text(128), reader.Text(64));
            reader.Complete();
            Canonical(bytes, EncodeCurrency(result));
            return result;
        }
        catch (Exception error) when (IsInvalidValue(error))
        {
            throw InvalidValue(error);
        }
    }

    public static byte[] EncodeReward(ActorRewardDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Frame("progression.reward", Number(1), Id(definition.DefinitionId), Text(definition.Key),
            Number(definition.Experience), EncodeCurrency(definition.Currency), Id(definition.ReserveAccountId),
            Number(definition.CurrencyAmount));
    }

    public static ActorRewardDefinition DecodeReward(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new ProgressionFrameReader(bytes, MaximumRewardByteLength, 8);
            reader.Tag("progression.reward"u8);
            reader.Version();
            var result = new ActorRewardDefinition(reader.Identity(), reader.Text(128), reader.Number(),
                DecodeCurrency(reader.Field(MaximumCurrencyByteLength)), reader.Identity(), reader.Number());
            reader.Complete();
            Canonical(bytes, EncodeReward(result));
            return result;
        }
        catch (Exception error) when (IsInvalidValue(error))
        {
            throw InvalidValue(error);
        }
    }

    public static byte[] EncodeLedger(CurrencyLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        return Frame("progression.currency-ledger", Number(1), Id(ledger.WorldId), Id(ledger.BranchId),
            Number(ledger.Revision), EncodeCurrency(ledger.Currency), Number(ledger.AdmittedSupply),
            Collection("progression.accounts", ledger.Accounts.Select(EncodeAccount)));
    }

    public static CurrencyLedger DecodeLedger(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new ProgressionFrameReader(bytes, MaximumLedgerByteLength, 8);
            reader.Tag("progression.currency-ledger"u8);
            reader.Version();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong revision = reader.Number();
            CurrencyDefinition currency = DecodeCurrency(reader.Field(MaximumCurrencyByteLength));
            ulong supply = reader.Number();
            var accounts = new ProgressionFrameReader(reader.Field(MaximumLedgerByteLength),
                MaximumLedgerByteLength, CurrencyLedger.MaximumAccounts + 1);
            accounts.Tag("progression.accounts"u8);
            List<CurrencyAccount> values = [];
            while (accounts.RemainingFields != 0)
            {
                values.Add(DecodeAccount(accounts.Field(MaximumAccountByteLength)));
            }
            accounts.Complete();
            reader.Complete();
            var result = new CurrencyLedger(world, branch, revision, currency, supply, values);
            Canonical(bytes, EncodeLedger(result));
            return result;
        }
        catch (Exception error) when (IsInvalidValue(error))
        {
            throw InvalidValue(error);
        }
    }

    private static byte[] EncodeActor(ActorProgressionState actor) => Frame("progression.actor", Number(1),
        Id(actor.ActorId), Number(actor.Revision), Id(actor.Profile.DefinitionId), actor.Profile.Version.ToByteArray(),
        Id(actor.Curve.DefinitionId), actor.Curve.Version.ToByteArray(), Number((ulong)actor.Level),
        Number(actor.Experience), Number((ulong)actor.CurrentHp), Number((ulong)actor.CurrentMp));

    private static ActorProgressionState DecodeActor(ReadOnlySpan<byte> bytes,
        IReadOnlyList<ActorStatProfileDefinition> profiles, IReadOnlyList<LevelProgressionDefinition> curves)
    {
        var reader = new ProgressionFrameReader(bytes, MaximumActorByteLength, 12);
        reader.Tag("progression.actor"u8);
        reader.Version();
        EntityId actor = reader.Identity();
        ulong revision = reader.Number();
        EntityId profileId = reader.Identity();
        ContentHash profileVersion = reader.Hash();
        EntityId curveId = reader.Identity();
        ContentHash curveVersion = reader.Hash();
        ActorStatProfileDefinition profile = profiles.FirstOrDefault(value => value.DefinitionId == profileId
            && value.Version == profileVersion) ?? throw new ArgumentException("Unknown actor statistic definition pin.");
        LevelProgressionDefinition curve = curves.FirstOrDefault(value => value.DefinitionId == curveId
            && value.Version == curveVersion) ?? throw new ArgumentException("Unknown actor progression curve pin.");
        var result = new ActorProgressionState(actor, revision, profile, curve, checked((int)reader.Number()),
            reader.Number(), checked((int)reader.Number()), checked((int)reader.Number()));
        reader.Complete();
        return result;
    }

    private static byte[] EncodeAccount(CurrencyAccount account) => Frame("progression.account", Number(1),
        Id(account.AccountId), Number((ulong)account.Kind), account.ActorId is { } actor ? Id(actor) : Array.Empty<byte>(),
        Number(account.Balance), Number(account.Revision));

    private static CurrencyAccount DecodeAccount(ReadOnlySpan<byte> bytes)
    {
        var reader = new ProgressionFrameReader(bytes, MaximumAccountByteLength, 7);
        reader.Tag("progression.account"u8);
        reader.Version();
        EntityId id = reader.Identity();
        var kind = (CurrencyAccountKind)checked((int)reader.Number());
        ReadOnlySpan<byte> owner = reader.Field(EntityId.ByteLength);
        EntityId? actor = owner.IsEmpty ? null : EntityId.FromBytes(owner);
        var result = new CurrencyAccount(id, kind, actor, reader.Number(), reader.Number());
        reader.Complete();
        return result;
    }

    private static byte[] EncodeStats(ActorStatValues value) => Frame("progression.stats", Number(1),
        Number((ulong)value.MaxHp), Number((ulong)value.MaxMp), Number((ulong)value.Attack),
        Number((ulong)value.Magic), Number((ulong)value.Defense), Number((ulong)value.Speed));

    private static ActorStatValues DecodeStats(ReadOnlySpan<byte> bytes)
    {
        var reader = new ProgressionFrameReader(bytes, 256, 8);
        reader.Tag("progression.stats"u8);
        reader.Version();
        var result = new ActorStatValues(checked((int)reader.Number()), checked((int)reader.Number()),
            checked((int)reader.Number()), checked((int)reader.Number()), checked((int)reader.Number()),
            checked((int)reader.Number()));
        reader.Complete();
        return result;
    }

    private static byte[] EncodeModifiers(ActorStatModifiers value) => Frame("progression.modifiers", Number(1),
        Signed(value.Hp), Signed(value.Mp), Signed(value.Attack), Signed(value.Magic), Signed(value.Defense), Signed(value.Speed));

    private static ActorStatModifiers DecodeModifiers(ReadOnlySpan<byte> bytes)
    {
        var reader = new ProgressionFrameReader(bytes, 256, 8);
        reader.Tag("progression.modifiers"u8);
        reader.Version();
        var result = new ActorStatModifiers(reader.Signed(), reader.Signed(), reader.Signed(),
            reader.Signed(), reader.Signed(), reader.Signed());
        reader.Complete();
        return result;
    }

    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
    private static byte[] Collection(string tag, IEnumerable<byte[]> values) => Frame(tag,
        values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Text(string value) => CanonicalEncodingV1.Text(value, nameof(value));

    private static byte[] Signed(int value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static void Canonical(ReadOnlySpan<byte> original, ReadOnlySpan<byte> encoded)
    {
        if (!original.SequenceEqual(encoded))
        {
            throw new ProgressionFormatException("Progression fields are not in their unique canonical representation.");
        }
    }

    private static bool IsInvalidValue(Exception error) => error is ArgumentException or OverflowException or InvalidOperationException;
    private static ProgressionFormatException InvalidValue(Exception error) =>
        new("Progression data violates its finite state or pin invariants: " + error.Message);
}
