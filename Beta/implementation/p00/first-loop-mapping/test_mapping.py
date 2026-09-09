#!/usr/bin/env python3
"""Read-only positive and adversarial checks of the retained semantic mapping."""
from copy import deepcopy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import validate_mapping as validator

HERE = Path(__file__).resolve().parent
ARTIFACT = HERE / 'mapping-v1.json'
PREFIX = 'openfantasy.aldermere:'


def record(document, key):
    return next(row for row in document['records'] if row['sourceKey'] == PREFIX + key)


def field(document, key, address):
    return next(row for row in record(document, key)['fields'] if row['relativePointer'] == address)


def change_field_and_rehash(document):
    row = field(document, 'items/rations', '/healHp')
    row['sourceValue'] = 300
    row['valueSha256'] = validator.digest(validator.canonical(300))


def relabel_real_target(document):
    document['knownTargets']['material-key'] = deepcopy(document['knownTargets']['recipe-key'])


def forge_gameplay_mapping(document):
    row = field(document, 'items/rations', '/healHp')
    row.update(disposition='mapped_to_existing_contract',
               faithfulFieldTransform={'operation': 'positive_count', 'value': 30},
               targetReferences=['recipe-count'])


def mutate_code_and_rehash(document):
    row = document['codeContext'][0]
    row['sourceExactText'] += ' // changed'
    row['sourceIdentity']['exactTextSha256'] = validator.digest(row['sourceExactText'].encode())


MUTATIONS = {
    'schema_upgrade': lambda d: d.update(schemaVersion=2),
    'claims_playable_migration': lambda d: d.update(status='playable_migration_complete'),
    'claims_full_scope': lambda d: d['scope'].update(notFullMigration=False),
    'missing_primary_scope': lambda d: d['scope']['primaryRecords'].pop(),
    'missing_source_fingerprint': lambda d: d['sourcePacket'].pop('source-key-map.json'),
    'wrong_source_fingerprint': lambda d: d['sourcePacket'].update({'records.json': '0' * 64}),
    'missing_record': lambda d: d['records'].pop(),
    'duplicate_record': lambda d: d['records'].append(deepcopy(d['records'][0])),
    'changed_original_identity': lambda d: d['records'][0]['sourceIdentity'].update(jsonPointer='/items/0'),
    'changed_original_payload': lambda d: record(d, 'items/wood')['sourcePayload'].update(price=7),
    'changed_field_and_forged_hash': change_field_and_rehash,
    'changed_field_hash': lambda d: field(d, 'items/wood', '/price').update(valueSha256='0' * 64),
    'wrong_field_pointer': lambda d: field(d, 'items/wood', '/price').update(sourcePointer='/items/12/id'),
    'missing_nested_leaf': lambda d: record(d, 'npcs/farmer_wren')['fields'].remove(field(d, 'npcs/farmer_wren', '/appearance/skin')),
    'missing_container': lambda d: record(d, 'npcs/farmer_wren')['fields'].remove(field(d, 'npcs/farmer_wren', '/appearance')),
    'duplicate_field': lambda d: record(d, 'items/wood')['fields'].append(deepcopy(field(d, 'items/wood', '/id'))),
    'wrong_units_omission': lambda d: field(d, 'items/rations', '/healHp').update(units=''),
    'missing_meaning': lambda d: field(d, 'items/rations', '/healHp').update(meaning=''),
    'unapproved_identity': lambda d: d['records'][0]['target'].update(definitionId='00000000-0000-0000-0000-000000000001'),
    'source_hash_laundered_as_version': lambda d: d['records'][0]['target'].update(acceptedVersion=d['records'][0]['sourceIdentity']['payloadSha256']),
    'claims_admitted_record': lambda d: d['records'][0]['target'].update(runtimeAdmission='admitted'),
    'claims_auto_admission': lambda d: d['targetPolicy'].update(automaticRuntimeAdmission=True),
    'calibration_namespace_substitution': lambda d: d['records'][0]['target'].update(namespace='openfantasy.calibration'),
    'wrong_policy_contract': lambda d: d['targetPolicy'].update(namespaceContract='entity-id'),
    'unknown_target_reference': lambda d: field(d, 'items/wood', '/id').update(targetReferences=['unimplemented-item-compiler']),
    'duplicate_target_reference': lambda d: field(d, 'items/wood', '/id').update(targetReferences=['material-key', 'material-key']),
    'real_but_wrong_target_declaration': relabel_real_target,
    'wrong_target_fingerprint': lambda d: d['knownTargets']['material-key'].update(sha256='0' * 64),
    'unknown_registry_target': lambda d: d['knownTargets'].update({'admitted-world': deepcopy(d['knownTargets']['entity-id'])}),
    'fabricated_gameplay_conversion': forge_gameplay_mapping,
    'pending_field_with_conversion': lambda d: field(d, 'items/wood', '/price').update(faithfulFieldTransform={'operation': 'copy_key', 'value': 6}),
    'wrong_faithful_key': lambda d: field(d, 'items/wood', '/id')['faithfulFieldTransform'].update(value='timber'),
    'wrong_recipe_role': lambda d: field(d, 'recipes/craft_rations', '/outputCount')['faithfulFieldTransform'].update(role='Input'),
    'wrong_recipe_count': lambda d: field(d, 'recipes/craft_rations', '/inputs/0/count')['faithfulFieldTransform'].update(value=20),
    'missing_dependency_edge': lambda d: d['dependencyEdges'].pop(),
    'duplicate_dependency_edge': lambda d: d['dependencyEdges'].append(deepcopy(d['dependencyEdges'][0])),
    'changed_dependency_endpoint': lambda d: d['dependencyEdges'][0].update(to='invented:dependency'),
    'missing_field_dependency': lambda d: field(d, 'quests/wren_orchard', '/giver').update(sourceDependencies=[]),
    'missing_dependency_support': lambda d: d['dependencySupport'].pop(),
    'changed_dependency_support_hash': lambda d: d['dependencySupport'][0].update(sha256='0' * 64),
    'missing_code_archetype': lambda d: d['codeContext'].pop(),
    'changed_code_with_forged_hash': mutate_code_and_rehash,
    'missing_semantic_evidence': lambda d: d['evidence'].pop('consuming'),
    'unrelated_real_evidence': lambda d: d['evidence'].update({'gathering': deepcopy(d['evidence']['crafting'])}),
    'empty_evidence_marker': lambda d: d['evidence']['gathering'].update(requiredMarkers=['']),
    'missing_evidence_markers': lambda d: d['evidence']['gathering'].update(requiredMarkers=[]),
    'missing_source_default': lambda d: d['faithfulMissingDefaults'].pop(),
    'invented_default_station': lambda d: d['faithfulMissingDefaults'][0].update(sourceDefault='Forge'),
    'wrong_default_witness': lambda d: d['faithfulMissingDefaults'][0].update(evidence='crafting'),
    'default_overrides_source_field': lambda d: d['faithfulMissingDefaults'][0].update(missingField='/output'),
    'forged_proof_count': lambda d: d['counts'].update(fieldRows=1000),
}


class MappingChecks(unittest.TestCase):
    def setUp(self):
        self.document = json.loads(ARTIFACT.read_bytes())

    def test_retained_mapping_resolves_real_source_and_targets(self):
        result = validator.validate(self.document)
        self.assertEqual('verified_field_mapping_not_gameplay', result['status'])
        self.assertEqual(74, result['counts']['fieldRows'])
        self.assertEqual(28, result['counts']['dependencyEdges'])
        self.assertEqual(7, result['counts']['dispositions']['mapped_to_existing_contract'])
        self.assertEqual(12, result['targetReferences'])
        self.assertEqual(7, result['sourceEvidenceRecords'])

    def test_builder_stdout_exactly_reproduces_frozen_artifact(self):
        before = ARTIFACT.read_bytes()
        result = subprocess.run([sys.executable, str(HERE / 'build_mapping.py')], check=True, capture_output=True)
        self.assertEqual(before, result.stdout)
        self.assertEqual(before, ARTIFACT.read_bytes())

    def test_builder_check_is_read_only(self):
        before = ARTIFACT.read_bytes()
        result = subprocess.run([sys.executable, str(HERE / 'build_mapping.py'), '--check'], check=True, capture_output=True)
        self.assertIn(b'matches retained bytes', result.stdout)
        self.assertEqual(before, ARTIFACT.read_bytes())

    def test_explicit_output_reproduces_only_requested_temp_file(self):
        before = ARTIFACT.read_bytes()
        with tempfile.TemporaryDirectory(prefix='openfantasy-mapping-check-') as folder:
            output = Path(folder) / 'mapping.json'
            subprocess.run([sys.executable, str(HERE / 'build_mapping.py'), '--output', str(output)], check=True, capture_output=True)
            self.assertEqual(before, output.read_bytes())
        self.assertEqual(before, ARTIFACT.read_bytes())

    def test_validation_does_not_mutate_artifact(self):
        before = validator.canonical(self.document)
        validator.validate(self.document)
        self.assertEqual(before, validator.canonical(self.document))


def mutation_test(change):
    def test(self):
        change(self.document)
        with self.assertRaises(validator.MappingError):
            validator.validate(self.document)
    return test


for name, change in MUTATIONS.items():
    setattr(MappingChecks, 'test_reject_' + name, mutation_test(change))


if __name__ == '__main__':
    unittest.main(verbosity=2)
