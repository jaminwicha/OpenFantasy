#!/usr/bin/env python3
"""Exercise real closure integrity and deliberate adversarial reference failures."""
import copy
import json
from pathlib import Path
import random
import shutil
import tempfile
import unittest

import build_closure as closure


def row(family, identity, payload):
    return {'targetKey': closure.key(family, identity), 'family': family, 'legacyId': identity,
            'sourcePayload': {'id': identity, **payload}}


class SourceClosureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.outputs, cls.manifest = closure.build()
        cls.packet = json.loads(cls.outputs['records.json'])

    def test_real_transitive_closure_preserves_every_selected_root_and_payload(self):
        selection = json.loads((closure.ROOT / closure.SELECTION).read_text())
        records = {record['sourceIdentity']['inventoryId']: record for record in self.packet['records']}
        for selected in selection['records']:
            retained = records[selected['inventory_id']]
            self.assertEqual(selected['source_payload'], retained['sourcePayload'])
            self.assertEqual(selected['payload_sha256'], closure.digest(closure.canonical(retained['sourcePayload'])))
        identities = {record['targetKey'] for record in self.packet['records']}
        # Non-root shop/quest dependencies prove this is more than copying the initial 31.
        for identity in ('quests/rumor_bats', 'quests/hilda_frost_hunt', 'items/ether', 'items/ship'):
            self.assertIn(f'{closure.PACKAGE}:{identity}', identities)
        self.assertEqual(49, len(records))
        self.assertEqual(532, self.manifest['counts']['totalInventoriedStructuredRecords'])

    def test_every_emitted_edge_has_real_retained_endpoints(self):
        available = {record['targetKey'] for record in self.packet['records'] + self.packet['codeDefinedRecords']}
        available.update(source['targetKey'] for source in self.manifest['sources'])
        available.add(closure.key('packet', 'first-loop'))
        edges = json.loads(self.outputs['references.json'])['edges']
        for edge in edges:
            self.assertIn(edge['from'], available)
            self.assertIn(edge['to'], available)
        self.assertEqual([], self.manifest['unresolvedReferences'])

    def test_missing_reference_rejects_without_deleting_the_original_field(self):
        npc = row('npcs', 'keeper', {'name': 'Keeper', 'shop': ['lost_item']})
        snapshot = copy.deepcopy(npc)
        with self.assertRaisesRegex(closure.ClosureError, 'Missing/wrong-kind'):
            closure.resolve_records(closure.index_records([npc]), [('npcs', 'keeper')])
        self.assertEqual(snapshot, npc)

    def test_same_name_in_wrong_family_does_not_resolve(self):
        npc = row('npcs', 'keeper', {'name': 'Keeper', 'shop': ['strike']})
        ability = row('abilities', 'strike', {'kind': 'Melee'})
        with self.assertRaisesRegex(closure.ClosureError, "found only in.*abilities"):
            closure.resolve_records(closure.index_records([npc, ability]), [('npcs', 'keeper')])

    def test_duplicate_definition_identity_rejects(self):
        item = row('items', 'wood', {'kind': 'Material'})
        with self.assertRaisesRegex(closure.ClosureError, 'Duplicate definition identity'):
            closure.index_records([item, copy.deepcopy(item)])

    def test_unknown_top_level_reference_semantics_reject(self):
        item = row('items', 'wood', {'kind': 'Material', 'hiddenOwner': 'someone'})
        with self.assertRaisesRegex(closure.ClosureError, 'Unresolved field semantics'):
            closure.resolve_records(closure.index_records([item]), [('items', 'wood')])

    def test_unknown_nested_reference_semantics_reject(self):
        recipe = row('recipes', 'soup', {'output': 'soup', 'inputs': [{'item': 'wood', 'count': 1, 'substitute': 'bark'}]})
        with self.assertRaisesRegex(closure.ClosureError, 'Unresolved field semantics'):
            closure.resolve_records(closure.index_records([recipe]), [('recipes', 'soup')])

    def test_unknown_enum_semantics_reject(self):
        item = row('items', 'vehicle', {'kind': 'Vehicle', 'vehicle': 'Sandship'})
        with self.assertRaisesRegex(closure.ClosureError, 'Unresolved enum semantics'):
            closure.resolve_records(closure.index_records([item]), [('items', 'vehicle')])

    def test_cyclic_quest_and_npc_references_terminate_and_keep_both(self):
        npc = row('npcs', 'keeper', {'name': 'Keeper', 'quests': ['favor']})
        quest = row('quests', 'favor', {'kind': 'Fetch', 'giver': 'keeper', 'targetItem': 'wood'})
        item = row('items', 'wood', {'kind': 'Material'})
        retained, edges = closure.resolve_records(closure.index_records([npc, quest, item]), [('npcs', 'keeper')])
        self.assertEqual(3, len(retained))
        self.assertEqual(3, len(edges))

    def test_enumeration_and_root_order_do_not_change_canonical_closure(self):
        records = self.packet['records']
        roots = [(record['family'], str(record['legacyId'])) for record in records]
        expected_records, expected_edges = closure.resolve_records(closure.index_records(records), roots)
        randomizer = random.Random(574)
        for _ in range(8):
            shuffled = list(records)
            shuffled_roots = list(roots)
            randomizer.shuffle(shuffled)
            randomizer.shuffle(shuffled_roots)
            actual_records, actual_edges = closure.resolve_records(closure.index_records(shuffled), shuffled_roots)
            self.assertEqual(sorted(expected_records), sorted(actual_records))
            self.assertEqual(sorted(map(closure.canonical, expected_edges)), sorted(map(closure.canonical, actual_edges)))

    def test_late_code_dependent_yield_rejects_before_claiming_closed(self):
        existing = row('items', 'wood', {'kind': 'Material'})
        retained = {('items', 'wood'): existing}
        for added in (row('items', 'boat', {'kind': 'Vehicle', 'vehicle': 'Boat'}),
                      row('nodes', 'new_node', {'type': 'oak'})):
            with self.assertRaisesRegex(closure.ClosureError, 'Late code-dependent definition'):
                closure.merge_after_semantic_expansion(retained, {(added['family'], added['legacyId']): added})
            self.assertEqual({('items', 'wood'): existing}, retained)
        # The same item already visited by the main semantic pass is safe to rediscover.
        visited = row('items', 'boat', {'kind': 'Vehicle', 'vehicle': 'Boat'})
        retained[('items', 'boat')] = visited
        closure.merge_after_semantic_expansion(retained, {('items', 'boat'): visited})
        self.assertEqual(visited, retained[('items', 'boat')])

    def test_builder_is_byte_reproducible(self):
        repeated, _ = closure.build()
        self.assertEqual(self.outputs, repeated)
        for name, expected in self.outputs.items():
            self.assertEqual(expected, (closure.OUTPUT / name).read_bytes(), name)

    def test_retained_files_match_manifest_hashes_and_exact_source_bytes(self):
        for entry in self.manifest['files']:
            actual = self.outputs[entry['path']]
            self.assertEqual(entry['bytes'], len(actual))
            self.assertEqual(entry['sha256'], closure.digest(actual))
        for entry in self.manifest['sources']:
            self.assertEqual((closure.ROOT / entry['sourcePath']).read_bytes(), self.outputs[entry['retainedPath']])

    def test_source_drift_rejects_generation_before_any_output(self):
        with tempfile.TemporaryDirectory(prefix='openfantasy-closure-') as directory:
            root = Path(directory)
            inventory = json.loads((closure.ROOT / closure.INVENTORY).read_text())
            paths = {entry['source'] for entry in inventory['data_records']}
            paths.update(entry['sourcePath'] for entry in self.manifest['sources'])
            paths.update((closure.INVENTORY, closure.SELECTION))
            for path in paths:
                destination = root / path
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(closure.ROOT / path, destination)
            original = root / 'data/items.json'
            original.write_bytes(original.read_bytes() + b'\n')
            with self.assertRaisesRegex(closure.ClosureError, 'Source drift: data/items.json'):
                closure.build(root)

    def test_original_code_archetypes_are_exact_and_not_rebalanced(self):
        original = (closure.ROOT / 'src/Sim/World.Gathering.cs').read_text()
        by_id = {record['legacyId']: record for record in self.packet['codeDefinedRecords']}
        self.assertEqual({'oak', 'berry'}, set(by_id))
        for record in by_id.values():
            self.assertIn(record['sourceExactText'], original)
            self.assertEqual(record['sourceIdentity']['exactTextSha256'], closure.digest(record['sourceExactText'].encode()))
        self.assertEqual({'name': 'Oak', 'sprite': 'node_oak', 'tool': 'Axe', 'hp': 5, 'yieldItem': 'wood'}, by_id['oak']['extractedFields'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
