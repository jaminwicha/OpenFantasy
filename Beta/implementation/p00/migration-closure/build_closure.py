#!/usr/bin/env python3
"""Retain a typed transitive source closure; never transform gameplay or discard fields."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import sys

PACKAGE = 'openfantasy.aldermere'
ROOT = Path(__file__).resolve().parents[4]
INVENTORY = 'Beta/content-migration/original-content-inventory-2026-09-08.json'
SELECTION = 'Beta/implementation/p00/legacy-first-loop-inputs.json'
OUTPUT = ROOT / 'Beta/Runtime/content/legacy/aldermere-first-loop'
SCHEMA = {
    'npcs': 'id name sprite tileX tileY dialog dialogs shop quests appearance wander',
    'quests': 'id name kind main prerequisite giver offer turnIn targetMonster targetRegion targetItem talkNpc targetCount rewardXp rewardGil rewardItem minLevel',
    'items': 'id name kind price healHp healMp attackBonus magicBonus defenseBonus hpBonus tool toolPower vehicle',
    'classes': 'id name baseStats growth moveSpeed abilities',
    'abilities': 'id name kind power range atbCost mpCost unlockLevel aoeRadius usesMagic',
    'monsters': 'id name level stats moveSpeed xpReward gilMin gilMax aggroRange leashRange attack drops',
    'recipes': 'id output outputCount inputs station',
    'regions': 'id x y w h',
    'nodes': 'id type region count respawnTicks',
    'tileset': 'id name walkable',
}
STAT = 'hp mp attack magic defense speed'
APPEARANCE = 'name race skin hairStyle hairColor outfit outfitA outfitB acc'
SEMANTIC_SUPPORT = (
    'src/Sim/GameData.cs', 'src/Sim/WorldMap.cs', 'src/Sim/Appearance.cs',
    'src/Sim/World.cs', 'src/Sim/World.Systems.cs', 'src/Sim/World.Gathering.cs',
    'src/Sim/World.Vehicles.cs', 'src/Sim/SimConstants.cs',
    'src/Client/SpriteComposer.cs', 'src/Client/EntityView.cs',
)

class ClosureError(ValueError):
    pass


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    # This is the original inventory's exact payload hash convention.
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()


def document(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()


def pointer(value, address):
    for part in address.split('/')[1:]:
        part = part.replace('~1', '/').replace('~0', '~')
        value = value[int(part)] if isinstance(value, list) else value[part]
    return value


def key(family, identity):
    return f'{PACKAGE}:{family}/{identity}'


def fields(value, permitted, location):
    if not isinstance(value, dict):
        raise ClosureError(f'Invalid object at {location}')
    unknown = set(value) - set(permitted.split())
    if unknown:
        raise ClosureError(f'Unresolved field semantics at {location}: {sorted(unknown)}')


def enum(value, permitted, location):
    if value not in permitted.split():
        raise ClosureError(f'Unresolved enum semantics at {location}: {value!r}')


def validate_shape(family, payload):
    if family not in SCHEMA:
        raise ClosureError(f'Unresolved record family: {family}')
    fields(payload, SCHEMA[family], family)
    for field in ('stats', 'baseStats', 'growth'):
        if field in payload:
            fields(payload[field], STAT, f'{family}/{field}')
    for field, shape in (('appearance', APPEARANCE), ('region', 'x y w h'), ('attack', 'power range atbCost')):
        if field in payload:
            fields(payload[field], shape, f'{family}/{field}')
    for field, shape in (('drops', 'item chancePercent'), ('inputs', 'item count')):
        if field in payload:
            for index, item in enumerate(payload[field]):
                fields(item, shape, f'{family}/{field}/{index}')
    enums = {
        'items': {'kind': 'Consumable Weapon Armor Material Tool Vehicle', 'tool': 'None Axe Pickaxe Sickle', 'vehicle': 'None Canoe Boat Ship Tank Airship'},
        'abilities': {'kind': 'Melee Projectile Heal'},
        'quests': {'kind': 'Kill Explore Talk Fetch'},
        'recipes': {'station': 'None Forge'},
    }
    for field, permitted in enums.get(family, {}).items():
        if field in payload:
            enum(payload[field], permitted, f'{family}/{field}')


def definition_references(family, payload):
    """Only inspected data-model fields become definition references; prose stays prose."""
    result = []
    def add(field, target_family, value):
        if value != '':
            result.append((field, target_family, str(value)))
    if family == 'classes':
        for i, value in enumerate(payload['abilities']):
            add(f'/abilities/{i}', 'abilities', value)
    elif family == 'npcs':
        for field, target in (('shop', 'items'), ('quests', 'quests')):
            for i, value in enumerate(payload.get(field, [])):
                add(f'/{field}/{i}', target, value)
    elif family == 'quests':
        for field, target in (
            ('giver', 'npcs'), ('prerequisite', 'quests'), ('targetMonster', 'monsters'),
            ('targetRegion', 'regions'), ('targetItem', 'items'), ('talkNpc', 'npcs'), ('rewardItem', 'items'),
        ):
            if field in payload:
                add('/' + field, target, payload[field])
        required = {'Kill': 'targetMonster', 'Explore': 'targetRegion', 'Talk': 'talkNpc', 'Fetch': 'targetItem'}[payload.get('kind', 'Kill')]
        if not payload.get('giver') or not payload.get(required):
            raise ClosureError(f'Quest {payload.get("id")} has no giver or {required}')
    elif family == 'monsters':
        for i, value in enumerate(payload.get('drops', [])):
            add(f'/drops/{i}/item', 'items', value['item'])
    elif family == 'recipes':
        add('/output', 'items', payload['output'])
        for i, value in enumerate(payload['inputs']):
            add(f'/inputs/{i}/item', 'items', value['item'])
    return result


def resolve_records(index, roots):
    pending = list(roots)
    retained = {}
    references = []
    while pending:
        identity = pending.pop()
        if identity in retained:
            continue
        if identity not in index:
            _, legacy_id = identity
            wrong = sorted(family for family, candidate in index if candidate == legacy_id)
            detail = f'; found only in {wrong}' if wrong else ''
            raise ClosureError(f'Missing/wrong-kind definition {identity}{detail}')
        row = index[identity]
        validate_shape(row['family'], row['sourcePayload'])
        retained[identity] = row
        for field, family, legacy_id in definition_references(row['family'], row['sourcePayload']):
            destination = (family, legacy_id)
            references.append({'from': row['targetKey'], 'field': field, 'kind': 'definition', 'to': key(*destination)})
            pending.append(destination)
    return retained, references


def merge_after_semantic_expansion(retained, extra):
    """Reject newly found code-dependent kinds until the expander supports another pass."""
    for identity, row in extra.items():
        if identity in retained:
            continue
        if row['family'] == 'nodes' or (row['family'] == 'items' and row['sourcePayload'].get('kind') == 'Vehicle'):
            raise ClosureError(f'Late code-dependent definition needs semantic expansion: {identity}')
    retained.update(extra)


def index_records(rows):
    index = {}
    for row in rows:
        identity = (row['family'], str(row['legacyId']))
        if identity in index:
            raise ClosureError(f'Duplicate definition identity: {identity}')
        index[identity] = row
    return index


def build(root=ROOT):
    inventory_bytes = (root / INVENTORY).read_bytes()
    selection_bytes = (root / SELECTION).read_bytes()
    inventory = json.loads(inventory_bytes)
    selection = json.loads(selection_bytes)
    if digest(inventory_bytes) != selection['source_inventory_sha256']:
        raise ClosureError('Source inventory drift from reviewed selection')
    files = {entry['path']: entry for entry in inventory['files']}
    source_bytes = {}
    def source(path):
        if path not in files:
            raise ClosureError(f'Uninventoried source dependency: {path}')
        if path not in source_bytes:
            data = (root / path).read_bytes()
            expected = files[path]
            if len(data) != expected['bytes'] or digest(data) != expected['sha256']:
                raise ClosureError(f'Source drift: {path}')
            source_bytes[path] = data
        return source_bytes[path]

    parsed = {}
    all_rows = []
    for entry in inventory['data_records']:
        path = entry['source']
        if path not in parsed:
            parsed[path] = json.loads(source(path))
        payload = pointer(parsed[path], entry['json_pointer'])
        if digest(canonical(payload)) != entry['payload_sha256']:
            raise ClosureError(f'Payload source drift: {entry["inventory_id"]}')
        all_rows.append({
            'targetKey': key(entry['kind'], entry['legacy_id'] if entry['legacy_id'] is not None else entry['json_pointer'].strip('/').replace('/', '.')),
            'family': entry['kind'], 'legacyId': entry['legacy_id'],
            'sourceIdentity': {name: entry[old] for name, old in (
                ('path', 'source'), ('jsonPointer', 'json_pointer'), ('inventoryId', 'inventory_id'), ('payloadSha256', 'payload_sha256'))},
            'sourcePayload': payload,
        })
    # Anonymous inventory entries are not global definitions; index only inspected families.
    index = index_records([row for row in all_rows if row['family'] in SCHEMA])
    roots = []
    for chosen in selection['records']:
        identity = (chosen['family'], str(chosen['legacy_id']))
        if identity not in index:
            raise ClosureError(f'Selected root disappeared: {identity}')
        actual = index[identity]
        if actual['sourcePayload'] != chosen['source_payload'] or actual['sourceIdentity']['payloadSha256'] != chosen['payload_sha256'] or actual['sourceIdentity']['inventoryId'] != chosen['inventory_id']:
            raise ClosureError(f'Selected root source drift: {identity}')
        roots.append(identity)
    retained, references = resolve_records(index, roots)

    support = {}
    asset_paths = set()
    def support_ref(owner, field, path, kind='source_semantics'):
        source(path)
        target = key('assets' if kind == 'asset' else 'source_files', path)
        references.append({'from': owner, 'field': field, 'kind': kind, 'to': target})
        support[target] = path
        if kind == 'asset':
            asset_paths.add(path)
    for path in SEMANTIC_SUPPORT:
        support_ref(key('packet', 'first-loop'), '$reference_rules', path)
    for entry in selection['source_files']:
        path = entry['source']
        if digest(source(path)) != entry['sha256']:
            raise ClosureError(f'Selected file drift: {path}')
        if path.startswith('assets/'):
            support_ref(key('packet', 'first-loop'), '$retained_calibration_input', path, 'asset')

    gathering = source('src/Sim/World.Gathering.cs').decode()
    pattern = r'^        \["([^"\n]+)"\] = new NodeArchetype\("([^"\n]+)", "([^"\n]+)", ToolKind\.(\w+), (\d+), "([^"\n]+)"\),$'
    archetypes = {}
    for match in re.finditer(pattern, gathering, re.MULTILINE):
        identity, name, sprite, tool, hp, yielded = match.groups()
        if identity in archetypes:
            raise ClosureError(f'Duplicate code archetype {identity}')
        archetypes[identity] = (match.group(), {'name': name, 'sprite': sprite, 'tool': tool, 'hp': int(hp), 'yieldItem': yielded})
    declared_nodes = {entry['legacy_id'] for entry in inventory['code_defined_content'] if entry['kind'] == 'node_archetype'}
    if set(archetypes) != declared_nodes:
        raise ClosureError('Code archetype extraction unresolved; inspect changed syntax instead of guessing')
    code_rows = {}
    extra_roots = []
    for row in list(retained.values()):
        payload = row['sourcePayload']
        owner = row['targetKey']
        if row['family'] == 'nodes':
            identity = payload['type']
            if identity not in archetypes:
                raise ClosureError(f'Unknown code-defined node type: {identity}')
            raw, values = archetypes[identity]
            target = key('node_archetypes', identity)
            references.append({'from': owner, 'field': '/type', 'kind': 'code_definition', 'to': target})
            if target not in code_rows:
                code_rows[target] = {
                    'targetKey': target, 'family': 'node_archetypes', 'legacyId': identity,
                    'sourceIdentity': {'path': 'src/Sim/World.Gathering.cs', 'inventoryId': f'src/Sim/World.Gathering.cs#node_archetype/{identity}', 'sourceSha256': digest(source('src/Sim/World.Gathering.cs')), 'exactTextSha256': digest(raw.encode())},
                    'sourceExactText': raw, 'extractedFields': values,
                    'extractionRule': 'Exact NodeArchetype constructor expression; no gameplay-unit transformation',
                }
                support_ref(target, '$source', 'src/Sim/World.Gathering.cs')
                support_ref(target, '/sprite', f'assets/units/{values["sprite"]}.png', 'asset')
                references.append({'from': target, 'field': '/yieldItem', 'kind': 'definition', 'to': key('items', values['yieldItem'])})
                extra_roots.append(('items', values['yieldItem']))
        if row['family'] == 'items' and payload.get('kind') == 'Vehicle':
            vehicle = payload.get('vehicle', 'None')
            if vehicle == 'None':
                raise ClosureError(f'Vehicle item has no vehicle: {owner}')
            support_ref(owner, '/vehicle', 'src/Sim/World.Vehicles.cs')
            support_ref(owner, '/vehicle', f'assets/units/veh_{vehicle.lower()}.png', 'asset')
            terrain_names = {'Canoe': ['shallow'], 'Boat': ['shallow', 'ocean'], 'Ship': ['shallow', 'ocean'], 'Tank': ['hills', 'tree'], 'Airship': []}[vehicle]
            for name in terrain_names:
                candidates = [r for r in index.values() if r['family'] == 'tileset' and r['sourcePayload']['name'] == name]
                if len(candidates) != 1:
                    raise ClosureError(f'Unresolved vehicle terrain: {name}')
                candidate = candidates[0]
                extra_roots.append(('tileset', str(candidate['legacyId'])))
                references.append({'from': owner, 'field': '/vehicle', 'kind': 'definition_via_code', 'to': candidate['targetKey']})

    extra, extra_references = resolve_records(index, extra_roots)
    merge_after_semantic_expansion(retained, extra)
    references.extend(extra_references)
    appearance = json.loads(source('data/appearance.json'))
    fields(appearance, 'gray skins hairStyles hairColors outfits outfitColors accessories races presets', 'appearance manifest')
    for preset in appearance['presets']:
        fields(preset, APPEARANCE, 'appearance preset')
    for row in retained.values():
        owner = row['targetKey']
        payload = row['sourcePayload']
        support_ref(owner, '$source', row['sourceIdentity']['path'], 'source_payload')
        if row['family'] == 'monsters':
            support_ref(owner, '/id', f'assets/units/{payload["id"]}.png', 'asset')
        if row['family'] == 'recipes' and payload.get('station', 'None') == 'Forge':
            raise ClosureError('Forge station closure not yet implemented; add typed terrain witness before claiming closure')
        if row['family'] == 'npcs':
            if 'appearance' in payload:
                app = payload['appearance']
                support_ref(owner, '/appearance', 'data/appearance.json', 'appearance_manifest')
                bounds = {'race': len(appearance['races']), 'skin': len(appearance['skins']), 'hairStyle': appearance['hairStyles'], 'hairColor': len(appearance['hairColors']), 'outfit': appearance['outfits'], 'outfitA': len(appearance['outfitColors']), 'outfitB': len(appearance['outfitColors']), 'acc': appearance['accessories']}
                for field, maximum in bounds.items():
                    value = app.get(field, 0)  # Source AppearancePreset's inspected default, never written into preserved payload.
                    if not isinstance(value, int) or isinstance(value, bool) or not 0 <= value < maximum:
                        raise ClosureError(f'Appearance index outside source range: {owner}/{field}')
                layers = ['body', f'outfit_{app.get("outfit",0):02}_a', f'outfit_{app.get("outfit",0):02}_b', f'hair_{app.get("hairStyle",0):02}']
                if app.get('race', 0) > 0:
                    layers.append(f'race_{app["race"]:02}')
                if app.get('acc', 0) > 0:
                    layers.append(f'acc_{app["acc"]:02}')
                for layer in layers:
                    support_ref(owner, '/appearance', f'assets/layers/{layer}.png', 'asset')
            if payload.get('sprite'):
                # Even inactive alternate sprites remain accountable source dependencies.
                support_ref(owner, '/sprite', f'assets/units/{payload["sprite"]}.png', 'asset')
            if not payload.get('sprite') and 'appearance' not in payload:
                raise ClosureError(f'NPC has no source presentation: {owner}')

    rows = sorted(retained.values(), key=lambda row: row['targetKey'])
    code = sorted(code_rows.values(), key=lambda row: row['targetKey'])
    mappings = [{'inventoryId': row['sourceIdentity']['inventoryId'], 'sourcePath': row['sourceIdentity']['path'], 'targetKey': row['targetKey'], 'mapping': 'one_to_one_retained_source_identity', 'transforms': []} for row in rows + code]
    edges = sorted({canonical(edge): edge for edge in references}.values(), key=lambda edge: (edge['from'], edge['field'], edge['kind'], edge['to']))
    available = {row['targetKey'] for row in rows + code} | set(support) | {key('packet', 'first-loop')}
    for edge in edges:
        if edge['from'] not in available or edge['to'] not in available:
            raise ClosureError(f'Unresolved emitted edge: {edge}')
    outputs = {
        'records.json': document({'schemaVersion': 1, 'packageId': PACKAGE, 'records': rows, 'codeDefinedRecords': code}),
        'references.json': document({'schemaVersion': 1, 'packageId': PACKAGE, 'edges': edges}),
        'source-key-map.json': document({'schemaVersion': 1, 'packageId': PACKAGE, 'mappings': sorted(mappings, key=lambda row: row['inventoryId'])}),
    }
    support_rows = []
    for target, path in sorted(support.items()):
        destination = path if path.startswith('assets/') else 'source-support/' + path + ('.txt' if path.endswith('.cs') else '')
        outputs[destination] = source(path)
        support_rows.append({'targetKey': target, 'sourcePath': path, 'retainedPath': destination, 'bytes': len(source(path)), 'sha256': digest(source(path)), 'role': 'retained_original_asset_not_native_approval' if path in asset_paths else 'source_payload_or_semantics_not_executable_import'})
    manifest = {
        'schemaVersion': 1, 'packageId': PACKAGE, 'packetId': 'aldermere-first-loop',
        'status': 'typed_source_dependency_closure_verified_not_playable_import',
        'inputHashes': {'sourceInventorySha256': digest(inventory_bytes), 'selectedRootsSha256': digest(selection_bytes)},
        'rootKeys': sorted(key(*identity) for identity in roots),
        'counts': {'selectedRoots': len(roots), 'retainedStructuredRecords': len(rows), 'retainedCodeDefinitions': len(code), 'typedEdges': len(edges), 'retainedAssets': len(asset_paths), 'retainedSourceFiles': len(support_rows), 'totalInventoriedStructuredRecords': len(inventory['data_records']), 'totalInventoriedCodeDefinitions': len(inventory['code_defined_content']), 'totalInventoriedFiles': len(inventory['files']), 'totalInventoriedDocumentHeadings': len(inventory['document_sections'])},
        'recordFamilies': dict(sorted(Counter(row['family'] for row in rows).items())),
        'referenceRules': {
            'definitionFields': 'Class abilities; NPC shops/quests; quest giver/prerequisite/target/reward; monster drops; recipe inputs/output.',
            'codeDefinedFields': 'Node type resolves exact NodeArchetype constructor, yield item and sprite; vehicle enum resolves inspected terrain identities and visual sheet.',
            'presentationFields': 'NPC appearance indexes resolve complete original palette manifest and exact used layers; nonempty alternate sprites retained; monster ID resolves original sheet.',
            'nonReferences': 'Literal narrative/dialogue/name text, coordinates/rectangles, enum labels, numeric balance/unit fields and statistical blocks remain unchanged; no speculative text entity linking.',
            'sourceFiles': 'Whole source files preserve exact context and may contain unselected records. Their presence does not promote those additional records to migrated definitions.',
            'failClosed': 'Unknown fields, enum meanings, unresolved known references, duplicate identities, missing assets or any input source drift reject generation; no trimming or silent defaults.',
        },
        'unresolvedReferences': [],
        'remainingTransformations': [
            'No source attribute, damage/range, ATB/tick, currency, stack/mass/volume or clock values have been transformed into current authority rules.',
            'Resource periodic respawn, fixed prices, vehicle inventory/embark rules and source appearance clamping require explicit compatibility decisions.',
            'Coordinates and source map bytes are retained; geography, station/accessibility, quest availability and gameplay reachability are not established by reference closure.',
            'Original assets are byte-preserved calibration material, without native import, accepted style, motion or sound approval.',
            'Narrative semantics, all remaining inventory records, generated content, documentation concepts and full migration gates remain pending.',
        ],
        'sources': support_rows,
        'files': [{'path': path, 'bytes': len(data), 'sha256': digest(data)} for path, data in sorted(outputs.items())],
    }
    outputs['closure-manifest.json'] = document(manifest)
    return outputs, manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Rebuild in memory and require byte-identical retained output.')
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    outputs, manifest = build()
    if args.check:
        expected = set(outputs)
        actual = {str(path.relative_to(args.output)) for path in args.output.rglob('*') if path.is_file()}
        # Godot import sidecars/caches are not part of the retained packet and must not be committed here.
        if actual != expected:
            raise ClosureError(f'Output file set differs: missing={sorted(expected-actual)}, extra={sorted(actual-expected)}')
        for name, data in outputs.items():
            if (args.output / name).read_bytes() != data:
                raise ClosureError(f'Output drift: {name}')
    else:
        for name, data in outputs.items():
            destination = args.output / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    print(json.dumps({'status': 'verified' if args.check else 'written', **manifest['counts']}, indent=2))


if __name__ == '__main__':
    try:
        main()
    except (ClosureError, KeyError, FileNotFoundError, json.JSONDecodeError) as error:
        print(f'Closure rejected: {error}', file=sys.stderr)
        sys.exit(1)
