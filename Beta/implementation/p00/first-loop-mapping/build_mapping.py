#!/usr/bin/env python3
"""Build a field-accountable source mapping; no runtime/content mutation or tuning conversion."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
PACKET = ROOT / 'Beta/Runtime/content/legacy/aldermere-first-loop'
PRIMARY = {'npcs/farmer_wren', 'quests/wren_orchard', 'items/wood', 'items/berries', 'items/rations', 'recipes/craft_rations'}
CONTEXT = {'nodes/101', 'nodes/103'}
PREFIX = 'openfantasy.aldermere:'
DISPOSITIONS = {'mapped_to_existing_contract', 'pending_explicit_transform_or_gameplay', 'retained_source_only'}


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()


def digest(value):
    return hashlib.sha256(value).hexdigest()


def json_bytes(value):
    return (json.dumps(value, indent=2, ensure_ascii=False) + '\n').encode()


def fields(value, pointer=''):
    if isinstance(value, dict):
        for key, child in value.items():
            child_pointer = pointer + '/' + key.replace('~', '~0').replace('/', '~1')
            yield child_pointer, child
            yield from fields(child, child_pointer)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            child_pointer = pointer + '/' + str(index)
            yield child_pointer, child
            yield from fields(child, child_pointer)


def target(path, marker, meaning):
    actual = ROOT / ('Beta/Runtime/' + path)
    return {'path': str(actual.relative_to(ROOT)), 'sha256': digest(actual.read_bytes()), 'marker': marker, 'meaning': meaning}


def target_registry():
    base = 'src/OpenFantasy.Core/'
    return {
        'package-namespace': target(base+'Content/PackageId.cs', 'public sealed record PackageId', 'Existing package namespace value; not a gameplay definition compiler.'),
        'content-hash': target(base+'Content/ContentHash.cs', 'public sealed record ContentHash', 'Exact content digest; not an imported runtime version or source interval.'),
        'entity-id': target(base+'EntityId.cs', 'public readonly record struct EntityId', 'Canonical identity representation; allocation/collision registry for imported definitions is pending.'),
        'material-key': target(base+'Production/MaterialDefinition.cs', 'public string Key { get; }', 'Existing local material key field; physical calibration and accepted identity/version still required.'),
        'recipe-key': target(base+'Production/MaterialRecipeDefinition.cs', 'public string Key { get; }', 'Existing local recipe key field; key preservation alone does not admit the recipe.'),
        'recipe-count': target(base+'Production/RecipeTerm.cs', 'public ulong Count { get; }', 'Positive counted recipe amount; exact positive source integers fit without tuning.'),
        'recipe-role': target(base+'Production/RecipeTerm.cs', 'public enum RecipeMaterialRole', 'Input/Output role vocabulary. No original waste role is fabricated.'),
        'material-pin': target(base+'Custody/CountedItemDefinition.cs', 'public sealed record CountedItemDefinition', 'Needs exact accepted definition ID/version; no source ID minting by a string rename.'),
        'body-definition': target(base+'Actors/BodyDefinition.cs', 'public sealed class BodyDefinition', 'Finite anatomy schema exists; Wren is not assigned authored qualification dimensions or anatomy by this map.'),
        'form-binding': target(base+'Actors/ActorFormBinding.cs', 'public sealed record ActorFormBinding', 'Reference shape exists; this map grants no live actor/form identity or control.'),
        'calendar-coordinate': target(base+'Scheduling/CalendarClockCoordinate.cs', 'public sealed record CalendarClockCoordinate', 'Exact scoped calendar only; original respawn ticks require an explicit exposure/calendar policy.'),
        'world-address': target(base+'History/HistoryAddress.cs', 'public readonly record struct HistoryAddress', 'Exact realm/branch/X/Y/Z address representation; old tile geometry still needs a reviewed embedding.'),
    }


def semantics(family, pointer, value):
    name = pointer.rsplit('/', 1)[-1]
    pending = 'pending_explicit_transform_or_gameplay'
    retained = 'retained_source_only'
    mapped = 'mapped_to_existing_contract'
    if isinstance(value, (dict, list)):
        return retained, 'Ordered source grouping retained; child rows account for every contained field/value.', 'source JSON grouping', [], None
    if name == 'id':
        if family == 'items':
            return mapped, 'Faithful local key spelling; separate stable source key stays in namespace openfantasy.aldermere.', 'opaque stable key', ['material-key'], {'operation': 'copy_key', 'value': value}
        if family == 'recipes':
            return mapped, 'Faithful local recipe-key spelling; acceptance still requires material pins and actual process rules.', 'opaque stable key', ['recipe-key'], {'operation': 'copy_key', 'value': value}
        return pending, 'Retain source definition key; allocate canonical definition/live identities through reviewed compiler/registry policy.', 'opaque source identity', ['entity-id'], None
    if family == 'recipes' and (name == 'outputCount' or name == 'count'):
        role = 'Output' if name == 'outputCount' else 'Input'
        return mapped, 'Positive source count maps exactly to UInt64 RecipeTerm.Count; enclosing operation remains unadmitted.', 'counted source item units', ['recipe-count', 'recipe-role'], {'operation': 'positive_count', 'role': role, 'value': value}
    if family == 'recipes' and (name == 'output' or name == 'item'):
        return pending, 'Resolve preserved item key to accepted material identity/version; calibration pins cannot be substituted for source admission.', 'typed item-definition reference', ['material-pin'], None
    if family == 'npcs' and name in ('tileX', 'tileY'):
        return pending, 'Source tile-center placement is tile*32+16 old units. Target needs realm/Z/origin and reviewed source-tile-to-cell embedding; no new coordinates admitted.', 'source tiles', ['world-address'], None
    if family == 'npcs' and name == 'wander':
        return pending, 'Source home-leash radius in tiles, not a personality score or teleport range. New cognition, schedule and legal movement need explicit behavior.', 'source tiles of home leash', ['calendar-coordinate', 'body-definition'], None
    if family == 'npcs' and pointer.startswith('/appearance/'):
        meaning = 'Exact original palette/layer index; retain appearance manifest and used native source layers. It is not a current anatomy/stat/affinity decision.'
        if name == 'race':
            meaning += ' Source races[0] is Human; this does not assign the authored humanoid-candidate body profile.'
        return pending, meaning, 'source appearance index', ['body-definition'], None
    if family == 'npcs' and pointer.startswith('/quests/'):
        return pending, 'Explicit quest-definition reference; NPC and quest point to each other without duplicating either identity.', 'quest key', [], None
    if family == 'npcs' and name == 'sprite':
        return retained, 'Empty fallback sprite is intentional: appearance is present and preferred. Retain the empty value; do not invent a sheet or erase composition.', 'source fallback sprite key', [], None
    if name in ('name', 'dialog', 'offer', 'turnIn') or family == 'npcs' and pointer.startswith('/dialogs/'):
        return retained, 'Verbatim display/narrative source text. Future knowledge and event grounding must not treat flavor as an already simulated world fact.', 'verbatim source text', [], None
    if family == 'quests':
        meanings = {
            'kind': ('Fetch consumes the target item count on successful turn-in; target quest adjudication and atomic reward/custody are new gameplay.', 'source quest-kind enum'),
            'giver': ('Resolve farmer_wren as giver and Fetch turn-in NPC; current death/departure and alternative completion need explicit continuation.', 'NPC definition key'),
            'targetItem': ('Wood definition reference; actual protected/reserved owned lots must be selected and consumed by later quest authority.', 'item definition key'),
            'targetCount': ('Exactly six wood are required and consumed in the source; preserve amount while defining actual quest requirement/reservation contract.', 'counted wood units'),
            'rewardXp': ('420 source XP; new skills/classes and level progression require explicit mapping. Do not convert to work, stats or learning implicitly.', 'source XP points'),
            'rewardGil': ('200 source gil; currency denomination, issuer/treasury and real payment funding require a target economic contract.', 'source gil units'),
            'minLevel': ('Source minimum level one; target capability/DC and onboarding eligibility must preserve a feasible early route without assuming identical progression.', 'source integer level'),
        }
        if name in meanings:
            meaning, units = meanings[name]
            return pending, meaning, units, ['material-pin'] if name == 'targetItem' else [], None
    if family == 'items':
        meanings = {'kind': ('Material/Consumable source category; target use/effect rules remain separate from a physical MaterialDefinition.', 'source item-kind enum'),
                    'price': ('Static source shop value; retain historical number without claiming a live quote, tax, bid/ask or market equilibrium.', 'source gil units'),
                    'healHp': ('Rations restore up to 30 source HP, capped by deficit; target resource pools and legitimate consumption/effect transaction remain explicit.', 'source HP points'),
                    'healMp': ('Rations restore up to 5 source MP, capped by deficit; do not map to work units or an unconditional rest reset.', 'source MP points')}
        if name in meanings:
            return pending, *meanings[name], [], None
    if family == 'nodes':
        if name == 'type':
            return pending, 'Resolve exact preserved oak/berry code archetype, its required tool, source HP and per-swing yield; not a free new resource source ID.', 'code archetype key', ['material-pin'], None
        if pointer.startswith('/region/'):
            return pending, 'Source spawn-region rectangle is in tiles; requires real target geography/occupancy, stable generation address and scope before placement.', 'source tiles', ['world-address'], None
        if name == 'count':
            return pending, 'Source attempts to spawn this many nodes; each spawn tries at most32 candidates and can fail. It is not guaranteed source stock or counted export allowance.', 'initial spawn attempts', [], None
        if name == 'respawnTicks':
            return pending, 'Source ticks run at20/s:900=45s or700=35s after depletion. Target ecology/growth, exposure/Wait and renewable production-cycle claims require explicit semantics.', 'source scheduler ticks', ['calendar-coordinate'], None
    raise ValueError(f'Unreviewed source field semantics: {family}{pointer}={value!r}')


def build():
    records = json.loads((PACKET/'records.json').read_text())
    references = json.loads((PACKET/'references.json').read_text())['edges']
    manifest = json.loads((PACKET/'closure-manifest.json').read_text())
    registry = target_registry()
    chosen = [row for row in records['records'] if row['targetKey'][len(PREFIX):] in PRIMARY | CONTEXT]
    code = [row for row in records['codeDefinedRecords'] if row['legacyId'] in ('oak', 'berry')]
    selected_keys = {row['targetKey'] for row in chosen+code}
    dependencies = [edge for edge in references if edge['from'] in selected_keys]
    result = []
    for row in sorted(chosen, key=lambda value:value['targetKey']):
        identity = row['sourceIdentity']
        coverage = []
        for pointer, value in fields(row['sourcePayload']):
            disposition, meaning, units, targets, transform = semantics(row['family'], pointer, value)
            links = [edge for edge in dependencies if edge['from']==row['targetKey'] and
                     (edge['field']==pointer or pointer.startswith(edge['field']+'/'))]
            coverage.append({'relativePointer':pointer, 'sourcePointer':identity['jsonPointer']+pointer,
                'sourceValue':value, 'valueSha256':digest(canonical(value)), 'disposition':disposition,
                'units':units, 'meaning':meaning, 'targetReferences':targets, 'faithfulFieldTransform':transform,
                'sourceDependencies':links})
        result.append({'sourceKey':row['targetKey'], 'scope':'primary' if row['targetKey'][len(PREFIX):] in PRIMARY else 'gathering_context',
                       'sourceIdentity':identity, 'sourcePayload':row['sourcePayload'],
                       'target':{'namespace':'openfantasy.aldermere', 'preservedKey':row['targetKey'],
                            'definitionId':None, 'acceptedVersion':None,
                            'versionPolicy':'Allocate collision-checked definition identity from reviewed compiler/source alias policy; hash canonical accepted definition after explicit transforms. Source payload hash remains lineage, not accepted runtime version.',
                            'runtimeAdmission':'not_admitted'}, 'fields':coverage})
    evidence_specs = {
      'constants':('src/Sim/SimConstants.cs',['public const int TicksPerSecond = 20;', 'public const int UnitsPerTile = 32;']),
      'npc-defaults':('src/Sim/GameData.cs',['public List<string> Shop { get; set; } = new();','public string Prerequisite { get; set; } = "";','public string RewardItem { get; set; } = "";','public CraftStation Station { get; set; } = CraftStation.None;']),
      'npc-spawn':('src/Sim/World.Systems.cs',['X = def.TileX * u + u / 2,','Hp = 1,','MoveSpeed = 2,']),
      'quest-fetch':('src/Sim/World.Systems.cs',['QuestKind.Fetch => CountItem(player, quest.TargetItem) >= quest.TargetCount,','RemoveItem(player, quest.TargetItem, quest.TargetCount);','player.Gil += quest.RewardGil;','GrantQuestXp(player, quest.RewardXp);']),
      'gathering':('src/Sim/World.Gathering.cs',['private const int GatherAtbCost = 400;','private const int GatherRange = 56;','node.Hp--;','int amount = tool.ToolPower;']),
      'crafting':('src/Sim/World.Gathering.cs',['RemoveItem(player, input.Item, input.Count);','if (!AddItem(player, recipe.Output, recipe.OutputCount))']),
      'consuming':('src/Sim/World.Systems.cs',['int hp = Math.Min(item.HealHp, player.MaxHp - player.Hp);','int mp = Math.Min(item.HealMp, player.MaxMp - player.Mp);']),
    }
    evidence = {}
    for key,(path,markers) in evidence_specs.items():
        retained='source-support/'+path+'.txt'
        data=(PACKET/retained).read_bytes()
        evidence[key]={'retainedPath':retained,'originalPath':path,'sha256':digest(data),'requiredMarkers':markers}
    defaults = [
       {'sourceKey':PREFIX+'recipes/craft_rations','missingField':'/station','sourceDefault':'None','meaning':'No implicit Forge gate in source; target process station policy still pending.','evidence':'npc-defaults'},
       {'sourceKey':PREFIX+'npcs/farmer_wren','missingField':'/shop','sourceDefault':[],'meaning':'Wren has no source shop, regardless of flavor about paying gil.','evidence':'npc-defaults'},
       {'sourceKey':PREFIX+'quests/wren_orchard','missingField':'/prerequisite','sourceDefault':'','meaning':'No source prerequisite quest; do not invent a chain blocker.','evidence':'npc-defaults'},
       {'sourceKey':PREFIX+'quests/wren_orchard','missingField':'/rewardItem','sourceDefault':'','meaning':'No item reward in this quest; only source XP/gil.','evidence':'npc-defaults'},
    ]
    relevant_sources = {edge['to'] for edge in dependencies} - selected_keys
    support = [row for row in manifest['sources'] if row['targetKey'] in relevant_sources]
    return {'schemaVersion':1,'status':'field_mapping_review_not_playable_migration',
       'scope':{'primaryRecords':sorted(PREFIX+key for key in PRIMARY),'gatheringContextRecords':sorted(PREFIX+key for key in CONTEXT),
                'codeContextKeys':sorted(row['targetKey'] for row in code),'notFullMigration':True},
       'sourcePacket':{name:digest((PACKET/name).read_bytes()) for name in ['records.json','references.json','closure-manifest.json','source-key-map.json']},
       'knownTargets':registry,'targetPolicy':{'namespace':'openfantasy.aldermere','namespaceContract':'package-namespace',
           'sourceHashContract':'content-hash','automaticRuntimeAdmission':False,
           'calibrationPolicy':'openfantasy.calibration and authored body-family examples are qualification data, never substituted automatically for source definitions, HP, item effects, sizes or balance.'},
       'records':result,'codeContext':code,'dependencyEdges':dependencies,'dependencySupport':support,
       'evidence':evidence,'faithfulMissingDefaults':defaults,
       'counts':{'primaryRecords':len(PRIMARY),'gatheringContextRecords':len(CONTEXT),'codeContextRecords':len(code),
          'fieldRows':sum(len(row['fields']) for row in result),'dependencyEdges':len(dependencies),
          'dispositions':dict(sorted(Counter(field['disposition'] for row in result for field in row['fields']).items()))}}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    parser.add_argument('--output',type=Path,help='Write to an explicitly selected artifact path; default emits JSON to stdout.')
    args=parser.parse_args()
    if args.check and args.output: parser.error('--check and --output are mutually exclusive')
    expected=json_bytes(build())
    path=HERE/'mapping-v1.json'
    if args.check:
        if path.read_bytes()!=expected: raise ValueError('Mapping differs from current explicit rules/source fingerprints')
        print('Mapping rebuild matches retained bytes.')
    elif args.output:
        args.output.write_bytes(expected)
    else:
        print(expected.decode(),end='')


if __name__=='__main__': main()
