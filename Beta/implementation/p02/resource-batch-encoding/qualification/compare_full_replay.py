"""Compare complete instrumented Host replays; this is not a native latency acceptance run."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time

q = Path(__file__).resolve().parent
root = q.parents[4]
profile = q.parent.parent / 'native-startup-profile'
output = q / sys.argv[1]
output.mkdir()  # Preserve every run: existing labels are refused.
dotnet = Path('/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet')
source = q.parent.parent / 'coordinated-native/qualification/native-restorative-wwx4qw_9/coordinated-revoke.sqlite3'
source_hash = '8598a2da449339e08ef2a5c4370bbf4ea07cd34fdc645bc9ef418f974c87806d'
head_hash = '63c0f4517e92fbc0c0da8e9c5fe4651b0203b87e427c6f602f0469b886e037da'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def require(condition, message):
    if not condition:
        raise RuntimeError(message)

def source_pins(runtime):
    return {str(p.relative_to(runtime)): sha(p) for p in (runtime / 'src').rglob('*.cs')
            if not any(part in ('bin', 'obj') for part in p.parts)}

record = dict(kind='complete-replay-allocation-comparison', runs=[], status='running',
              startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              source=str(source), sourceSha256=source_hash, expectedHeadRevision=1581,
              expectedHeadHash=head_hash, hostDiagnosticBoundSeconds=90,
              limitations=['Inclusive stage allocations overlap; do not sum them.',
                           'Cumulative thread allocation is not retained heap or peak memory.',
                           'Instrumented Debug Host timing is not native or production latency acceptance.'])
try:
    require(sha(source) == source_hash, 'Retained source differs.')
    originals = []
    for label in ('run-01', 'resource-batch-after-01'):
        evidence = json.loads((profile / label / 'result.json').read_text())
        pins = evidence['instrumentedAssemblies']
        require(all(sha(Path(p)) == value for p, value in pins.items()), 'Instrumented binary differs.')
        host = next(Path(p) for p in pins if p.endswith('/OpenFantasy.Host.dll'))
        runtime = host.parents[5]
        originals.append((label, host, pins, source_pins(runtime)))
    differing = [name for name in sorted(set(originals[0][3]) | set(originals[1][3]))
                 if originals[0][3].get(name) != originals[1][3].get(name)]
    record['instrumentedSourceDifferences'] = differing
    require(differing == ['src/OpenFantasy.Core/Progression/ResourceHistory/ActorResourceHistoryCodecV1.cs',
                          'src/OpenFantasy.Core/Progression/ResourceHistory/ResourceChangeBatch.cs'],
            'The compared builds differ outside the two intended runtime files.')
    for label, host, pins, sources in originals:
        work = Path(tempfile.mkdtemp(prefix='openfantasy-complete-replay-'))
        database = work / 'world.sqlite3'
        shutil.copyfile(source, database)
        require(sha(database) == source_hash, 'Copied source differs.')
        costs = output / (label + '-costs.json')
        env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent),
                   OPENFANTASY_NATIVE_STARTUP_REPORT=str(costs))
        command = [str(dotnet), str(host), '--inspect-recorded-world', str(database), '1581']
        run = dict(label=label, command=command, instrumentedAssemblies=pins,
                   sourceSha256=sources, privateWork=str(work), loadBefore=os.getloadavg())
        record['runs'].append(run)
        start = time.monotonic()
        with (output / (label + '.stdout.log')).open('x') as out, \
                (output / (label + '.stderr.log')).open('x') as err:
            result = subprocess.run(command, cwd=work, env=env, stdout=out, stderr=err, timeout=90)
        run.update(exitCode=result.returncode, elapsedSeconds=time.monotonic() - start,
                   copiedSaveUnchanged=sha(database) == source_hash, loadAfter=os.getloadavg())
        require(result.returncode == 0, 'Replay command failed.')
        state = json.loads((output / (label + '.stdout.log')).read_text())
        run['recoveredHead'] = {key: state[key] for key in
                               ('SourceHeadRevision', 'SourceHeadHash', 'RecordedRevision', 'StateHash')}
        require(state['SourceHeadRevision'] == state['RecordedRevision'] == 1581 and
                state['SourceHeadHash'] == state['StateHash'] == head_hash, 'Recovered head differs.')
        snapshot = json.loads(costs.read_text())
        run['runtime'] = snapshot['runtime']['framework']
        run['stages'] = {row['stage']: {k: row[k] for k in
                          ('calls', 'inclusiveMs', 'threadAllocatedBytes', 'threads')}
                         for row in snapshot['stages']}
        require(run['stages']['full-replay']['calls'] == 1 and
                run['stages']['commit-preparation']['calls'] == 1581 and not snapshot['open'],
                'Incomplete replay cannot support a full-workload comparison.')
        require(run['copiedSaveUnchanged'] and all(sha(Path(p)) == value for p, value in pins.items()) and
                source_pins(host.parents[5]) == sources, 'A pinned input changed.')
        print(label, run['elapsedSeconds'], run['stages']['full-replay'], flush=True)
    before, after = record['runs']
    require({k: v['calls'] for k, v in before['stages'].items()} ==
            {k: v['calls'] for k, v in after['stages'].items()}, 'Stage counts differ.')
    record['comparison'] = {stage: dict(beforeBytes=before['stages'][stage]['threadAllocatedBytes'],
                                        afterBytes=after['stages'][stage]['threadAllocatedBytes'])
                            for stage in ('full-replay', 'world-encode', 'world-root', 'commit-preparation')}
    record['status'] = 'complete'
except Exception as error:
    record.update(status='failed', error=repr(error))
finally:
    record['sourceUnchanged'] = sha(source) == source_hash
    record['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    (output / 'result.json').write_text(json.dumps(record, indent=2) + '\n')
    print({key: record[key] for key in ('status', 'sourceUnchanged')}, flush=True)
raise SystemExit(0 if record['status'] == 'complete' and record['sourceUnchanged'] else 1)
