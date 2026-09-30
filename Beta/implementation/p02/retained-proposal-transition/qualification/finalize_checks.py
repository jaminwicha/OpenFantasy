"""Combine completed, source-identical runs; no failed/unfinished report supplies acceptance."""
import collections
import json
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

import run_checks as checks


def pin(path):
    return {'path': str(path.relative_to(checks.ROOT)), 'sha256': checks.digest(path)}


def main():
    directory = Path(__file__).resolve().parent
    reports = {
        'build': directory / 'build-20260930T011017Z-b922a/report.json',
        'focused': directory / 'focused-20260930T011055Z-198cf/report.json',
        'core': directory / 'core-20260930T011623Z-f8082/report.json',
        'hosting': directory / 'hosting-20260930T012833Z-94334/report.json',
    }
    loaded = {name: json.loads(path.read_text()) for name, path in reports.items()}
    build = loaded['build']
    cases = {}
    executions = 0
    for name, report in loaded.items():
        assert report['accepted'] and report['sourceUnchanged'], name
        assert report['sourceSha256'] == build['sourceSha256'], name
        assert report['assemblySha256'] == build['assemblySha256'], name
        assert report['elapsedSeconds'] <= report['limitSeconds'], name
        assert all(command['exitCode'] == 0 for command in report['commands']), name
        if name == 'build':
            continue
        assert report['buildReportSha256'] == checks.digest(reports['build'])
        count = 0
        for trx in reports[name].parent.glob('*.trx'):
            rows = ET.parse(trx).findall('.//' + checks.NS + 'UnitTestResult')
            assert rows, trx
            for row in rows:
                assert row.attrib['outcome'] == 'Passed', row.attrib
                key = (trx.stem, row.attrib['testId'])
                test_name = row.attrib['testName']
                assert key not in cases or cases[key] == test_name
                cases[key] = test_name
                count += 1
        assert report['outcomes'] == {'Passed': count} and count > 0
        executions += count
    assert checks.sources() == build['sourceSha256']
    assert checks.assemblies() == build['assemblySha256']
    preservation = checks.preservation()
    native_inputs = directory / 'retained-native-inputs-01'
    for path, digest in json.loads((native_inputs / 'input-pins.json').read_text()).items():
        assert checks.digest(Path(path)) == digest, path
    for result in json.loads((native_inputs / 'manifest.json').read_text())['results']:
        assert checks.digest(Path(result['save'])) == result['finalDatabaseSha256']
    audit_path = directory / 'retained-prefix-final.json'
    audit = json.loads(audit_path.read_text())
    assert audit['accepted']
    actual = directory / 'actual-journey-01'
    assert checks.digest(actual / 'journey.sqlite3') == audit['targetSha256']
    assert checks.digest(Path(json.loads((directory.parent / 'PRESERVATION.json').read_text())['sourceDatabase'])) == audit['sourceSha256']
    subprocess.run(['git', 'diff', '--check'], cwd=checks.ROOT, check=True)
    result = {
        'status': 'passed',
        'scope': 'Explicit retained27-to-proposal30 transition and managed original520-to521 journey; full Core and affected Host regression.',
        'evidence': {name: pin(path) for name, path in reports.items()},
        'distinctManagedPasses': len(cases),
        'byProject': dict(collections.Counter(project for project, _ in cases)),
        'executionsInAcceptedRuns': executions,
        'duplicateExecutionsExcluded': executions - len(cases),
        'focusedPassesIncluded': loaded['focused']['outcomes']['Passed'],
        'liveSourceAndAssembliesMatchAcceptedBuild': True,
        'preservation': preservation,
        'retainedPrefixAudit': pin(audit_path),
        'actualJourney': pin(actual / 'metadata.json'),
        'retainedNativeInputPins': pin(native_inputs / 'input-pins.json'),
        'retainedNativeAuthentication': 'Reauthenticated existing native outputs under current assemblies; no new rendered processes.',
        'failureDisposition': 'The interrupted first Host run and unfinished worker regression contribute no passing cases. The corrected-input Host repeat keeps the same selection and600s limit; initial wait cause is not established.',
        'remaining': ['native transitioned-save selection and joined native journey', 'actual transition-boundary process interruption', 'physical devices and OS focus', 'production latency and scale', 'full phase, campaign and human acceptance'],
    }
    (directory / 'FINAL_CHECKS.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({key: result[key] for key in ('status', 'distinctManagedPasses', 'byProject', 'duplicateExecutionsExcluded')}, indent=2))


if __name__ == '__main__':
    main()
