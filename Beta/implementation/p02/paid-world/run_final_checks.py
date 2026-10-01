"""One serial final qualification, with the existing per-check limits and no automatic retries.
Run only after the delegated worker is terminal and Astra has integrated its delivery.
This collects mechanical evidence; it does not grant review, native or campaign acceptance.
"""
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import traceback
import uuid

PACKET = Path(__file__).resolve().parent
CHECKER = PACKET / 'run_integration_checks.py'
PROBE = PACKET / 'verify_process_recovery.py'
ROOT = PACKET.parents[3]
DOTNET = Path('/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet')
HOST = ROOT / 'Beta/Runtime/src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    assert len(sys.argv) == 1, 'This qualification has no mode that skips required checks.'
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    output = PACKET / 'qualification' / ('final-' + stamp + '-' + uuid.uuid4().hex[:5])
    output.mkdir()
    report = {'status': 'running', 'steps': [], 'scope': 'Managed regression and actual process/storage evidence only'}
    print('Final evidence: ' + str(output), flush=True)
    pins = {str(path): digest(path) for path in (Path(__file__), CHECKER, PROBE)}
    spec = importlib.util.spec_from_file_location('paid_checks', CHECKER)
    checker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(checker)
    source = checker.sources()
    try:
        report['preservationBefore'] = checker.preservation()
        results = {}
        for mode in ('build', 'new-host', 'full-core', 'legacy-proposal-host', 'legacy-care-host', 'legacy-consented-host'):
            log = output / (mode + '.log')
            env = dict(os.environ)
            if mode == 'new-host':
                env['OPENFANTASY_PAID_WORLD_FIXTURES'] = str(output / 'fixtures')
            with log.open('x') as stream:
                # The child wrapper enforces 240s/600s, kills its process group and retains failures.
                result = subprocess.run([sys.executable, str(CHECKER), mode], cwd=ROOT, env=env,
                                        stdout=stream, stderr=subprocess.STDOUT)
            paths = [line.removeprefix('evidence: ') for line in log.read_text().splitlines() if line.startswith('evidence: ')]
            assert paths, 'Missing evidence path: ' + mode
            evidence = Path(paths[-1]) / 'report.json'
            step = json.loads(evidence.read_text())
            report['steps'].append({'mode': mode, 'report': str(evidence), 'sha256': digest(evidence)})
            assert result.returncode == 0 and step['accepted'], 'Check failed: ' + str(log)
            assert step['sourceSha256'] == source, 'Source changed during qualification'
            results[mode] = step
            print(mode + ': passed', flush=True)
        ids = set()
        for mode, result in results.items():
            if mode != 'build':
                assert result['outcomes'] == {'Passed': len(result['testIds'])}, 'Unexpected outcome or skip'
                ids.update(result['testIds'])
        report['distinctManagedPasses'] = len(ids)
        report['managedBySelection'] = {mode: result['outcomes'] for mode, result in results.items() if mode != 'build'}
        report['processProofs'] = []
        for label in ('produced', 'continued-raw'):
            fixture = output / 'fixtures' / label
            destination = output / ('process-' + label)
            args = [sys.executable, str(PROBE), '--dotnet', str(DOTNET), '--host', str(HOST),
                    '--before-db', str(fixture / 'before.sqlite3'), '--after-db', str(fixture / 'after.sqlite3'),
                    '--batch', str(fixture / 'batch.ofc'), '--after-root', str(fixture / 'after.ofc'),
                    '--profile', '32', '--expected-new-archives', '0', '--output', str(destination)]
            with (output / ('process-' + label + '.log')).open('x') as stream:
                result = subprocess.run(args, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT)
            evidence = destination / 'result.json'
            proof = json.loads(evidence.read_text())
            assert result.returncode == 0 and proof['status'] == 'passed', 'Process proof failed: ' + str(evidence)
            assert proof['externalKills'] == 2 and len(proof['corruptions']) == 3, 'Incomplete process/corruption proof'
            assert proof['fixturesUnchanged'] and proof['executionUnchanged'], 'Unstable measured input'
            for name in ('OpenFantasy.Core.dll', 'OpenFantasy.Hosting.dll', 'OpenFantasy.Host.dll'):
                built = 'Beta/Runtime/src/OpenFantasy.Host/bin/Debug/net10.0/' + name
                assert proof['executionHashes'][name] == results['build']['assemblySha256'][built], 'Different proof executable'
            report['processProofs'].append({'report': str(evidence), 'sha256': digest(evidence)})
            print(label + ': two actual kills and three corruption refusals passed', flush=True)
        assert checker.sources() == source, 'Final source drift'
        assert checker.assemblies() == results['build']['assemblySha256'], 'Final assembly drift'
        assert all(digest(Path(path)) == value for path, value in pins.items()), 'Qualification script drift'
        report['preservationAfter'] = checker.preservation()
        report.update(status='passed', externalKills=4, sqlCorruptionRefusals=6,
                      sourceSha256=source, assemblySha256=results['build']['assemblySha256'], scriptSha256=pins)
    except Exception:
        report.update(status='failed', error=traceback.format_exc())
        print(report['error'], flush=True)
    (output / 'FINAL_CHECKS.json').write_text(json.dumps(report, indent=2) + '\n')
    print('Final evidence: ' + str(output / 'FINAL_CHECKS.json'), flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
