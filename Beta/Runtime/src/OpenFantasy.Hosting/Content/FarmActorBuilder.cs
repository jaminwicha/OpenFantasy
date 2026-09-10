using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

internal sealed record AdmittedFarmActor(ActorPresence Presence, WorldResident Resident,
    ActorControlBinding Control, CustodianStorage Storage, CustodianAccessPolicy Policy);

/// <summary>Composes one shared actor model with explicit individual control and language admission.</summary>
internal sealed class FarmActorBuilder
{
    private readonly FarmAdmissionDefinition _definition;
    private readonly FarmIdentityScope _ids;
    private readonly TerrainScope _scope;
    private readonly BodyDefinition _body;
    private readonly EntityId _place;
    private readonly LanguageChannelKey _channel;

    internal FarmActorBuilder(FarmAdmissionDefinition definition, FarmIdentityScope ids,
        TerrainScope scope, BodyDefinition body, EntityId place)
    {
        _definition = definition;
        _ids = ids;
        _scope = scope;
        _body = body;
        _place = place;
        _channel = definition.LanguageChannel(ids);
    }

    internal AdmittedFarmActor Player(string name)
    {
        JsonElement player = _definition.Player;
        SubunitPosition home = FarmAdmissionDefinition.Feet(player.GetProperty("tileX").GetInt32(),
            player.GetProperty("tileY").GetInt32(), player.GetProperty("feetZ").GetInt64());
        return Build("player", name, home, player.GetProperty("wanderRadiusCells").GetInt32(), player, null);
    }

    internal AdmittedFarmActor Wren(FarmSourcePacket source)
    {
        JsonElement wren = _definition.Wren;
        SubunitPosition home = FarmAdmissionDefinition.Feet(source.Wren.GetProperty("tileX").GetInt32(),
            source.Wren.GetProperty("tileY").GetInt32(), wren.GetProperty("feetZ").GetInt64());
        var opening = new ResidentUtterance(_ids.Definition(wren.GetProperty("openingUtteranceKey").GetString()!),
            _channel, _definition.LanguageVersion, source.Wren.GetProperty("dialog").GetString()!);
        return Build("wren", source.Wren.GetProperty("name").GetString()!, home,
            source.Wren.GetProperty("wander").GetInt32(), wren, opening);
    }

    private AdmittedFarmActor Build(string role, string name, SubunitPosition home, int wanderRadius,
        JsonElement metadata, ResidentUtterance? opening)
    {
        EntityId actor = _ids.Instance(role + ".actor");
        EntityId storage = _ids.Instance(role + ".storage");
        ActorFormBinding binding = ActorFormBinding.Corporeal(actor, _ids.Instance(role + ".form"),
            _ids.Instance(role + ".physical-body"), _body);
        var condition = new BodyConditionSnapshot(binding.FormInstanceId, _body, []);
        var presence = new ActorPresence(binding, condition, _scope, home, 0, _place);
        var comprehension = new ActorComprehensionProfile(actor, 0, _definition.ComprehensionRulesVersion,
            [new LanguageComprehensionEntry(_channel, _definition.LanguageVersion,
                (ComprehensionBand)metadata.GetProperty("comprehensionBand").GetInt32())]);
        var resident = new WorldResident(actor, storage, name,
            (ResidentControllerKind)metadata.GetProperty("controllerKind").GetInt32(), home, wanderRadius, comprehension,
            LocalExposureDuration.FromMilliseconds(_definition.Root.GetProperty("initialExperiencedMilliseconds").GetUInt64()), opening);
        var control = new ActorControlBinding(_ids.Instance(role + ".principal"), _ids.Instance(role + ".session"), actor);
        return new AdmittedFarmActor(presence, resident, control,
            new CustodianStorage(storage, metadata.GetProperty("storageUnitCapacity").GetUInt64(), 0),
            new CustodianAccessPolicy(storage, actor, (CustodyDepositRule)metadata.GetProperty("depositRule").GetInt32()));
    }
}
