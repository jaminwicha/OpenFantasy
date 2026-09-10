using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Joins the existing finite-domain contracts before a local root is admitted. This validates supplied
/// identities and references, not authentication, content acquisition, history or extraction rights.
/// </summary>
public static class WorldAdmissionValidator
{
    public static WorldIdentityIndex Validate(WorldSnapshot world, WorldIdentityIndex? previous = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.AdvancingSteps > world.Revision)
        {
            throw new ArgumentException("Advancing steps cannot exceed committed world revisions.", nameof(world));
        }
        TerrainIdentityLayer terrain = previous is not null && previous.Terrain.Matches(world.Space.Regions)
            ? previous.Terrain : ReadTerrain(world.Space.Regions);
        var admission = new Admission(world, terrain);
        admission.ReadInventory();
        admission.ReadPresence();
        admission.ReadResidents();
        admission.ReadDialogues();
        return admission.Complete();
    }

    private static TerrainIdentityLayer ReadTerrain(IReadOnlyList<TerrainRegionSnapshot> regions)
    {
        Dictionary<EntityId, WorldIdentityRole> roles = [];
        Dictionary<EntityId, ContentHash> materials = [];
        Dictionary<EntityId, (long X, long Y, TerrainStratum Stratum)> formations = [];
        foreach (TerrainRegionSnapshot region in regions)
        {
            TerrainMaterialPalette palette = region.Rules.Materials;
            foreach (CountedItemDefinition material in new[] { palette.Surface, palette.Soil, palette.Stone })
            {
                Register(roles, material.DefinitionId, WorldIdentityRole.ItemDefinition);
                Pin(materials, material.DefinitionId, material.Version);
            }
            foreach (TerrainCell cell in region.Cells)
            {
                if (cell.Stratum == TerrainStratum.Void)
                {
                    if (cell.SourceId.HasValue || cell.FormationId.HasValue || cell.Material is not null)
                    {
                        throw new ArgumentException("Void terrain cannot claim substrate lineage or material.");
                    }
                    continue;
                }
                if (cell.SourceId is not { } source || cell.FormationId is not { } formation
                    || cell.Material is not { } material)
                {
                    throw new ArgumentException("Substrate requires its source, formation and exact material pin.");
                }
                if (material != palette.For(cell.Stratum))
                {
                    throw new ArgumentException("A substrate cell must use its stratum's admitted material pin.");
                }
                Register(roles, source, WorldIdentityRole.TerrainSource, unique: true);
                Register(roles, formation, WorldIdentityRole.TerrainFormation);
                Register(roles, material.DefinitionId, WorldIdentityRole.ItemDefinition);
                Pin(materials, material.DefinitionId, material.Version);
                var lineage = (cell.Coordinate.X, cell.Coordinate.Y, cell.Stratum);
                if (formations.TryGetValue(formation, out var existing) && existing != lineage)
                {
                    throw new ArgumentException("One terrain formation cannot name different columns or strata.");
                }
                formations[formation] = lineage;
            }
        }
        return new TerrainIdentityLayer(regions, roles, materials);
    }

    private static void Register(Dictionary<EntityId, WorldIdentityRole> roles, EntityId identity,
        WorldIdentityRole role, bool unique = false)
    {
        if (roles.TryGetValue(identity, out WorldIdentityRole existing) && (existing != role || unique))
        {
            throw new ArgumentException("A local identity aliases incompatible roles or repeats a unique instance.");
        }
        roles[identity] = role;
    }

    private static void Pin(Dictionary<EntityId, ContentHash> pins, EntityId identity, ContentHash version)
    {
        if (pins.TryGetValue(identity, out ContentHash? existing) && existing != version)
        {
            throw new ArgumentException("A shared definition identity requires one exact content pin.");
        }
        pins[identity] = version;
    }

    private sealed class Admission(WorldSnapshot world, TerrainIdentityLayer terrain)
    {
        private readonly Dictionary<EntityId, WorldIdentityRole> _roles = [];
        private readonly Dictionary<EntityId, ContentHash> _definitionPins = [];
        private readonly Dictionary<LanguageChannelKey, ContentHash> _channelPins = [];

        internal WorldIdentityIndex Complete() => new(_roles, terrain);

        internal void ReadInventory()
        {
            CountedAuthorityState inventory = world.Inventory;
            Add(inventory.WorldId, WorldIdentityRole.World);
            Add(inventory.BranchId, WorldIdentityRole.Branch);
            Add(world.Space.Scope.RealmId, WorldIdentityRole.Realm);
            foreach (EntityId actor in inventory.Actors)
            {
                Add(actor, WorldIdentityRole.Actor);
            }
            foreach (ActorControlBinding control in inventory.Controls)
            {
                Require(control.ActorId, WorldIdentityRole.Actor);
                Add(control.PrincipalId, WorldIdentityRole.Principal);
                Add(control.SessionId, WorldIdentityRole.Session);
            }
            foreach (CustodianAccessPolicy policy in inventory.Policies)
            {
                Add(policy.CustodianId, WorldIdentityRole.Custodian);
                Require(policy.OwnerActorId, WorldIdentityRole.Actor);
            }
            foreach (EntityId reservation in inventory.ReservationIds)
            {
                Add(reservation, WorldIdentityRole.Reservation);
            }
            foreach (AdmittedCountedSource source in inventory.Sources)
            {
                // Terrain-source identities have a separate role until a coupled extraction contract exists.
                Add(source.SourceId, WorldIdentityRole.CountedSource);
                Definition(source.Definition.DefinitionId, WorldIdentityRole.ItemDefinition, source.Definition.Version);
            }
            foreach (CountedSourceLot lot in inventory.Custody.Lots)
            {
                Add(lot.LotId, WorldIdentityRole.Lot);
                Require(lot.CustodianId, WorldIdentityRole.Custodian);
                Require(lot.Source.SourceId, WorldIdentityRole.CountedSource);
                Definition(lot.Definition.DefinitionId, WorldIdentityRole.ItemDefinition, lot.Definition.Version);
            }
        }

        internal void ReadPresence()
        {
            foreach (ActorPresence actor in world.Space.Actors)
            {
                Require(actor.ActorId, WorldIdentityRole.Actor);
                Add(actor.Binding.FormInstanceId, WorldIdentityRole.Form, unique: true);
                Add(actor.Binding.PhysicalBodyId!.Value, WorldIdentityRole.Body, unique: true);
                Add(actor.HomePlaceId, WorldIdentityRole.Place);
                Definition(actor.Condition.Definition.DefinitionId, WorldIdentityRole.BodyDefinition,
                    actor.Condition.Definition.Version);
                foreach (BodyInjury injury in actor.Condition.Injuries)
                {
                    Add(injury.InjuryId, WorldIdentityRole.Injury, unique: true);
                    Add(injury.CauseId, WorldIdentityRole.CauseReference);
                }
            }
        }

        internal void ReadResidents()
        {
            HashSet<EntityId> controlled = world.Inventory.Controls.Select(control => control.ActorId).ToHashSet();
            ContentHash rulesVersion = world.Residents[0].Comprehension.RulesVersion;
            foreach (WorldResident resident in world.Residents)
            {
                Require(resident.ActorId, WorldIdentityRole.Actor);
                Require(resident.InventoryCustodianId, WorldIdentityRole.Custodian);
                if (!controlled.Contains(resident.ActorId) || resident.Comprehension.RulesVersion != rulesVersion)
                {
                    throw new ArgumentException("Every local resident needs explicit control and the admitted comprehension rules pin.");
                }
                foreach (LanguageComprehensionEntry entry in resident.Comprehension.Entries)
                {
                    Channel(entry.Channel, entry.DefinitionVersion);
                }
                if (resident.OpeningUtterance is { } utterance)
                {
                    Channel(utterance.Channel, utterance.ChannelVersion);
                    Definition(utterance.DefinitionId, WorldIdentityRole.Utterance,
                        ContentHash.Compute(WorldResidentCodecV1.EncodeUtterance(utterance)));
                }
            }
        }

        internal void ReadDialogues()
        {
            foreach (DialogueSession dialogue in world.Dialogues)
            {
                Add(dialogue.DialogueId, WorldIdentityRole.Dialogue, unique: true);
                Add(dialogue.CauseId, WorldIdentityRole.CauseReference);
                Require(dialogue.ListenerActorId, WorldIdentityRole.Actor);
                Require(dialogue.SpeakerActorId, WorldIdentityRole.Actor);
                Require(dialogue.UtteranceDefinitionId, WorldIdentityRole.Utterance);
                WorldResident speaker = world.GetResident(dialogue.SpeakerActorId);
                WorldResident listener = world.GetResident(dialogue.ListenerActorId);
                ResidentUtterance utterance = speaker.OpeningUtterance!;
                BodyConditionSnapshot speakerBody = world.Space.GetActor(speaker.ActorId).Condition;
                BodyConditionSnapshot listenerBody = world.Space.GetActor(listener.ActorId).Condition;
                var requirement = new ComprehensionRequirement(utterance.Channel, utterance.ChannelVersion,
                    ComprehensionBand.Conversational);
                if (utterance.Channel.Mode != CommunicationMode.Spoken
                    || !speakerBody.SupportsFunction(BodyFunction.Vocalization)
                    || !listenerBody.SupportsFunction(BodyFunction.Hearing)
                    || world.Space.QueryInteraction(listener.ActorId, speaker.ActorId) != SpatialInteraction.Reachable
                    || !speaker.Comprehension.Query(speaker.ActorId, speaker.Comprehension.RulesVersion,
                        requirement).MeetsBandRequirement
                    || !listener.Comprehension.Query(listener.ActorId, listener.Comprehension.RulesVersion,
                        requirement).MeetsBandRequirement)
                {
                    throw new ArgumentException("An active addressed dialogue requires reachable speech, hearing and explicit comprehension.");
                }
            }
        }

        private void Add(EntityId identity, WorldIdentityRole role, bool unique = false)
        {
            if (terrain.TryGetRole(identity, out WorldIdentityRole existing) && existing != role)
            {
                throw new ArgumentException("A dynamic identity aliases an incompatible admitted terrain role.");
            }
            Register(_roles, identity, role, unique);
        }

        private void Require(EntityId identity, WorldIdentityRole role)
        {
            if (!_roles.TryGetValue(identity, out WorldIdentityRole existing) || existing != role)
            {
                throw new ArgumentException("A local reference does not resolve to its admitted entity role.");
            }
        }

        private void Definition(EntityId identity, WorldIdentityRole role, ContentHash version)
        {
            Add(identity, role);
            if (terrain.TryGetMaterialPin(identity, out ContentHash? terrainVersion) && terrainVersion != version)
            {
                throw new ArgumentException("Inventory and terrain must share an exact counted-material pin.");
            }
            Pin(_definitionPins, identity, version);
        }

        private void Channel(LanguageChannelKey channel, ContentHash version)
        {
            Add(channel.LanguageId, WorldIdentityRole.Language);
            Add(channel.RegisterId, WorldIdentityRole.LanguageRegister);
            if (_channelPins.TryGetValue(channel, out ContentHash? existing) && existing != version)
            {
                throw new ArgumentException("A shared language channel requires one exact definition pin.");
            }
            _channelPins[channel] = version;
        }
    }
}
