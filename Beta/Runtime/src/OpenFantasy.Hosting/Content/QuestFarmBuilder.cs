using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Builds fresh explicit stats and funded quest metadata while retaining every existing material right.</summary>
internal sealed class QuestFarmBuilder
{
    private readonly WorldSnapshot _world;
    private readonly QuestFarmDefinition _definition;
    private readonly QuestFarmSources _source;
    private readonly QuestFarmIdentityScope _identities;
    private readonly WorldResident _player;
    private readonly WorldResident _wren;

    internal QuestFarmBuilder(WorldSnapshot world, QuestFarmDefinition definition, QuestFarmSources source)
    {
        _world = world;
        _definition = definition;
        _source = source;
        _identities = new QuestFarmIdentityScope(world);
        _player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        _wren = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host);
    }

    internal QuestFarmComponents Build()
    {
        JsonElement project = _definition.Root.GetProperty("project");
        EntityId custodian = _identities.Instance(project.GetProperty("custodianRole").GetString()!);
        EntityId reservation = _identities.Instance(project.GetProperty("reservationRole").GetString()!);
        CountedAuthorityState inventory = AddEmptyProject(custodian, reservation, project);
        ProgressionSnapshot progression = CreateProgression();
        JsonElement metadata = _definition.Root.GetProperty("quest");
        CurrencyAccount reserve = progression.Currency.Accounts.Single(value => value.Kind == CurrencyAccountKind.Reserve);
        var reward = new ActorRewardDefinition(_identities.Instance(metadata.GetProperty("rewardRole").GetString()!),
            "wren_orchard_reward", _source.Quest.GetProperty("rewardXp").GetUInt64(), progression.Currency.Currency,
            reserve.AccountId, _source.Quest.GetProperty("rewardGil").GetUInt64());
        var item = new CountedItemDefinition(EntityId.Parse(metadata.GetProperty("itemDefinitionId").GetString()),
            new ContentHash(metadata.GetProperty("itemVersion").GetString()!));
        var quest = new DeliveryQuestDefinition(_identities.Instance(metadata.GetProperty("definitionRole").GetString()!),
            _source.Quest.GetProperty("id").GetString()!, _source.Quest.GetProperty("name").GetString()!,
            _source.Quest.GetProperty("offer").GetString()!, _wren.ActorId, _source.Quest.GetProperty("minLevel").GetUInt32(),
            item, _source.Quest.GetProperty("targetCount").GetUInt64(), custodian, reservation, reward);
        var quests = new QuestSnapshot(_world.WorldId, _world.BranchId, 0, [quest], []);
        _world.Production!.ValidateForAuthority(inventory);
        var projectAdmission = new QuestProjectCustody(custodian, _wren.ActorId,
            project.GetProperty("unitCapacity").GetUInt64(), reservation,
            (CustodyDepositRule)project.GetProperty("depositRule").GetInt32());
        var admission = new WorldQuestAdmission(QuestFarmAdmission.AdmissionVersion, progression, quests, [projectAdmission]);
        return new QuestFarmComponents(inventory, admission);
    }

    private CountedAuthorityState AddEmptyProject(EntityId custodian, EntityId reservation, JsonElement project)
    {
        CountedAuthorityState current = _world.Inventory;
        var storage = new CustodianStorage(custodian, project.GetProperty("unitCapacity").GetUInt64(), 0);
        var policy = new CustodianAccessPolicy(custodian, _wren.ActorId,
            (CustodyDepositRule)project.GetProperty("depositRule").GetInt32());
        var custody = new CustodySnapshot(_world.WorldId, _world.BranchId, current.Custody.Revision,
            current.Custody.Storages.Append(storage), current.Custody.Lots);
        return new CountedAuthorityState(custody, current.AdmittedUnitCapacity, current.Actors, current.Controls,
            current.Policies.Append(policy), current.Sources, current.ReservationIds.Append(reservation));
    }

    private ProgressionSnapshot CreateProgression()
    {
        JsonElement player = _definition.Root.GetProperty("playerProfile");
        JsonElement wren = _definition.Root.GetProperty("wrenProfile");
        JsonElement curveMetadata = _definition.Root.GetProperty("levelCurve");
        ActorStatValues values = ReadSourceStats(_source.Squire.GetProperty("baseStats"));
        var playerProfile = new ActorStatProfileDefinition(_identities.Definition(player.GetProperty("definitionKey").GetString()!),
            player.GetProperty("key").GetString()!, player.GetProperty("displayName").GetString()!, ActorStatProfileKind.Class,
            values, ReadSourceModifiers(_source.Squire.GetProperty("growth")), ReadModifiers(player.GetProperty("heritageModifiers")));
        int[] fixedValues = wren.GetProperty("baseStats").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        var wrenProfile = new ActorStatProfileDefinition(_identities.Definition(wren.GetProperty("definitionKey").GetString()!),
            wren.GetProperty("key").GetString()!, wren.GetProperty("displayName").GetString()!, ActorStatProfileKind.Unassigned,
            new ActorStatValues(fixedValues[0], fixedValues[1], fixedValues[2], fixedValues[3], fixedValues[4], fixedValues[5]),
            ReadModifiers(wren.GetProperty("growth")), ReadModifiers(wren.GetProperty("heritageModifiers")));
        var curve = new LevelProgressionDefinition(_identities.Definition(curveMetadata.GetProperty("definitionKey").GetString()!),
            curveMetadata.GetProperty("key").GetString()!, curveMetadata.GetProperty("thresholds").EnumerateArray().Select(value => value.GetUInt64()));
        ActorProgressionState[] actors = [CreateActor(_player, playerProfile, curve, player), CreateActor(_wren, wrenProfile, curve, wren)];
        return new ProgressionSnapshot(_world.WorldId, _world.BranchId, 0, [playerProfile, wrenProfile], [curve], actors, CreateCurrency(player, wren));
    }

    private CurrencyLedger CreateCurrency(JsonElement player, JsonElement wren)
    {
        JsonElement metadata = _definition.Root.GetProperty("currency");
        var currency = new CurrencyDefinition(_identities.Definition(metadata.GetProperty("definitionKey").GetString()!),
            metadata.GetProperty("key").GetString()!, metadata.GetProperty("displayName").GetString()!, metadata.GetProperty("unitName").GetString()!);
        CurrencyAccount[] accounts =
        [
            new(_identities.Instance("quest.player-wallet"), CurrencyAccountKind.Actor, _player.ActorId, player.GetProperty("initialGil").GetUInt64(), 0),
            new(_identities.Instance("quest.wren-wallet"), CurrencyAccountKind.Actor, _wren.ActorId, wren.GetProperty("initialGil").GetUInt64(), 0),
            new(_identities.Instance(metadata.GetProperty("reserveRole").GetString()!), CurrencyAccountKind.Reserve, null,
                metadata.GetProperty("reserveBalance").GetUInt64(), 0)
        ];
        return new CurrencyLedger(_world.WorldId, _world.BranchId, 0, currency, metadata.GetProperty("admittedSupply").GetUInt64(), accounts);
    }

    private static ActorProgressionState CreateActor(WorldResident actor, ActorStatProfileDefinition profile,
        LevelProgressionDefinition curve, JsonElement metadata)
    {
        int level = metadata.GetProperty("initialLevel").GetInt32();
        ActorStatValues stats = profile.Calculate(level);
        return new ActorProgressionState(actor.ActorId, 0, profile, curve, level,
            metadata.GetProperty("initialExperience").GetUInt64(), stats.MaxHp, stats.MaxMp);
    }

    private static ActorStatValues ReadSourceStats(JsonElement value) => new(value.GetProperty("hp").GetInt32(),
        value.GetProperty("mp").GetInt32(), value.GetProperty("attack").GetInt32(), value.GetProperty("magic").GetInt32(),
        value.GetProperty("defense").GetInt32(), value.GetProperty("speed").GetInt32());

    private static ActorStatModifiers ReadSourceModifiers(JsonElement value) => new(value.GetProperty("hp").GetInt32(),
        value.GetProperty("mp").GetInt32(), value.GetProperty("attack").GetInt32(), value.GetProperty("magic").GetInt32(),
        value.GetProperty("defense").GetInt32(), value.GetProperty("speed").GetInt32());

    private static ActorStatModifiers ReadModifiers(JsonElement value)
    {
        int[] modifiers = value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
        if (modifiers.Length != 6) throw new InvalidDataException("A quest stat modifier vector must contain exactly six values.");
        return new ActorStatModifiers(modifiers[0], modifiers[1], modifiers[2], modifiers[3], modifiers[4], modifiers[5]);
    }
}
