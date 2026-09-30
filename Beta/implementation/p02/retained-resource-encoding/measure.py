#!/usr/bin/env python3
"""Complete retained-replay comparison for owned resource-history encoding. Diagnostic only.

Before: the pinned instrumented baseline Host assemblies, copied privately and never edited.
After: a private copy of that same instrumented baseline source tree in which only the owned files that
differ from their baseline source pins are replaced by the current production files, instrumented with
the baseline's exact anchors plus one optional cold-capture stage. Both replay private copies of the
pinned retained world to its recorded head. A timed-out or partial replay is never a complete-workload
result, and wall time under shared machine load is not causal evidence.
"""
import datetime
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import resource
import shutil
import signal
import subprocess
import time
import traceback

PACKET = Path(__file__).resolve().parent
REPO = PACKET.parents[3]
RUNTIME = REPO / 'Beta/Runtime'
PRIVATE = Path('/var/folders/np/sh0qs32n2xlf150ph1vlcvrc0000gn/T/openfantasy-opus-resource-encoding-k37_0m8i')
BASELINE_REPORT = PRIVATE / 'input/baseline-report.json'
BASELINE = Path('/var/folders/np/sh0qs32n2xlf150ph1vlcvrc0000gn/T/openfantasy-retained-profile-98u4svvl')
BASELINE_RUNTIME = BASELINE / 'runtime'
BIN = Path('src/OpenFantasy.Host/bin/Debug/net10.0')
WORLD = PRIVATE / 'world.sqlite'
WORLD_SHA256 = '5d1eb8dd867fd85b0ba7cf95b7a4a84e07516c2f9857d3f7b5e3be898cac718f'
HEAD_REVISION = 1739
HEAD_HASH = 'd4e869e3e9ce1a35625e8ce269596cf07c8ecfa5c9191213f00276704e3d99e4'
DOTNET = Path('/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet')
LIMITS = dict(buildSeconds=240, replaySeconds=120, aggregateSeconds=600)
ASSEMBLIES = ('OpenFantasy.Core.dll', 'OpenFantasy.Hosting.dll', 'OpenFantasy.Host.dll')
OWNED = ('src/OpenFantasy.Core/Worlds/WorldResourceHistory.cs',
         'src/OpenFantasy.Core/Worlds/WorldResourceHistoryCodec.cs',
         'src/OpenFantasy.Core/Worlds/WorldResourceHistoryCodecV1.cs',
         'src/OpenFantasy.Core/Progression/ResourceHistory/ActorResourceHistory.cs',
         'src/OpenFantasy.Core/Progression/ResourceHistory/ActorResourceHistoryCodecV1.cs')
PAYLOAD = '\n        using var nativeStartupCost = global::OpenFantasy.Core.NativeStartupProbe.Begin("%s", false, 0);'
# Absent from the baseline source; it counts cold structural captures in the after build only.
EXTRA_ANCHORS = [('src/OpenFantasy.Core/Worlds/WorldResourceHistoryCodecV1.cs',
                  re.escape('internal static byte[] EncodeStructure(WorldResourceHistory value') + r'(?:(?!=>)[^;{}])*?\)\n    \{',
                  'codec-world-resource-structure')]
COMPARED = ('full-replay', 'replay-prepare-entry', 'commit-preparation', 'step-decide', 'world-root', 'world-encode',
            'canonical-frame-capture', 'codec-world-resource', 'codec-world-resource-body',
            'codec-world-resource-structure', 'codec-actor-resource-history', 'resource-causes')
RETAINED_LOG_BYTES = 16000


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def digest(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1 << 20), b''):
            value.update(block)
    return value.hexdigest()


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def tree_files(root):
    """Relative source files of one runtime tree, excluding build output."""
    return {str(path.relative_to(root)): path for path in root.rglob('*')
            if path.is_file() and not {'bin', 'obj'} & set(path.relative_to(root).parts)}


def production_state(pins):
    """Current production source against the baseline's recorded pins."""
    current = {str(path): digest(path) for path in tree_files(RUNTIME / 'src').values()
               if path.suffix in ('.cs', '.csproj', '.json', '.props', '.targets')}
    changed = sorted(name for name, sha in pins.items() if name in current and current[name] != sha)
    removed = sorted(name for name in pins if name not in current)
    added = sorted(name for name in current if name not in pins)
    return current, dict(changed=changed, added=added, removed=removed)


def world_state():
    wal = Path(str(WORLD) + '-wal')
    return dict(sha256=digest(WORLD), walBytes=wal.stat().st_size if wal.exists() else None)


def pinned_copy(destination):
    state = world_state()
    require(state['sha256'] == WORLD_SHA256 and not state['walBytes'],
            'The private retained world no longer has its pinned closed bytes.')
    shutil.copyfile(WORLD, destination)
    require(digest(destination) == WORLD_SHA256, 'The world changed while it was copied.')


def execute(report, output, label, command, cwd, env, limit):
    bound = min(limit, report['deadline'] - time.monotonic() - 5)
    require(bound > 10, 'The aggregate bound is exhausted before ' + label)
    entry = dict(label=label, command=[str(part) for part in command], cwd=str(cwd), boundSeconds=round(bound, 1),
                 loadBefore=os.getloadavg(), startedUtc=now())
    report['commands'].append(entry)
    stdout, stderr = cwd / (label + '.stdout.log'), cwd / (label + '.stderr.log')
    before = resource.getrusage(resource.RUSAGE_CHILDREN)
    started = time.monotonic()
    with stdout.open('wb') as out, stderr.open('wb') as err:
        process = subprocess.Popen(entry['command'], cwd=cwd, env=env, stdout=out, stderr=err,
                                   stdin=subprocess.DEVNULL, start_new_session=True)
        try:
            entry['exitCode'] = process.wait(timeout=bound)
        except BaseException as error:
            entry['timedOut'] = isinstance(error, subprocess.TimeoutExpired)
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
            if not entry['timedOut']:
                raise
    after = resource.getrusage(resource.RUSAGE_CHILDREN)
    entry.update(wallSeconds=round(time.monotonic() - started, 3), childUserCpuSeconds=round(after.ru_utime - before.ru_utime, 3),
                 childSystemCpuSeconds=round(after.ru_stime - before.ru_stime, 3), loadAfter=os.getloadavg(), finishedUtc=now())
    for source in (stdout, stderr):
        (output / source.name).write_bytes(source.read_bytes()[-RETAINED_LOG_BYTES:])
    return entry, stdout


def instrument(path, anchors):
    text = path.read_text()
    for pattern, stage in anchors:
        matches = list(re.finditer(pattern, text))
        require(len(matches) == 1, 'Expected one %s anchor in %s, found %d' % (stage, path, len(matches)))
        text = text[:matches[0].end()] + PAYLOAD % stage + text[matches[0].end():]
    path.write_text(text)


def prepare_after(report, baseline, work, output):
    """Baseline instrumented tree with only the changed owned files replaced and identically instrumented."""
    tree = work / 'after-runtime'
    shutil.copytree(BASELINE_RUNTIME, tree, ignore=shutil.ignore_patterns('bin', 'obj'))
    applied = {}
    for item in baseline['applied']:
        applied.setdefault(item['source'], []).append((item['anchor'], item['stage']))
    replaced = []
    for relative in OWNED:
        production = RUNTIME / relative
        if digest(production) == baseline['sourcePins'][str(production)]:
            continue
        shutil.copyfile(production, tree / relative)
        extra = [(pattern, stage) for source, pattern, stage in EXTRA_ANCHORS
                 if source == relative and re.search(pattern, (tree / relative).read_text())]
        instrument(tree / relative, applied.get(relative, []) + extra)
        replaced.append(dict(source=relative, productionSha256=digest(production),
                             stages=[stage for _, stage in applied.get(relative, []) + extra]))
    before, after = tree_files(BASELINE_RUNTIME), tree_files(tree)
    differing = sorted(name for name in set(before) | set(after)
                       if name not in before or name not in after or digest(before[name]) != digest(after[name]))
    require(set(differing) <= set(OWNED), 'The after tree differs outside the owned files: %s' % differing)
    report['afterTree'] = dict(path=str(tree), replacedOwnedFiles=replaced, differingFromBaselineTree=differing)
    env = dict(report['environment'])
    command = [DOTNET, 'build', 'src/OpenFantasy.Host/OpenFantasy.Host.csproj', '-p:RestoreLockedMode=true', '-m:1',
               '-p:UseSharedCompilation=false', '--disable-build-servers', '--verbosity', 'quiet']
    entry, _ = execute(report, output, 'build-after', command, tree, env, LIMITS['buildSeconds'])
    require(entry.get('exitCode') == 0, 'The after build failed or timed out; its retained log explains it.')
    return tree / BIN


def prepare_before(baseline, work):
    binary = work / 'before-bin'
    shutil.copytree(BASELINE_RUNTIME / BIN, binary)
    for name in ASSEMBLIES:
        require(digest(binary / name) == baseline['assemblies'][str(BASELINE_RUNTIME / BIN / name)],
                'The copied baseline assembly %s is not the pinned build.' % name)
    return binary


def summarize(costs):
    snapshot = json.loads(costs.read_text())
    stages = {row['stage']: dict(calls=row['calls'], wallMs=round(row['inclusiveMs'], 1), threadCpuMs=round(row['threadCpuMs'], 1),
                                 threadAllocatedBytes=row['threadAllocatedBytes']) for row in snapshot['stages']}
    final = snapshot['state']
    return dict(reason=snapshot['reason'], open=snapshot['open'], stages=stages,
                process=dict(allocatedBytes=final['allocatedBytes'], processCpuMs=final['processCpuMs'], gen0=final['gen0'],
                             gen1=final['gen1'], gen2=final['gen2'], gcPauseMs=final['gcPauseMs'], jitMs=final['jitMs']),
                runtime=dict(framework=snapshot['runtime']['framework'], serverGc=snapshot['runtime']['serverGc'],
                             assemblies=[[a['name'], a['location'], a['jitOptimizerDisabled']] for a in snapshot['runtime']['assemblies']]))


def replay(report, output, work, label, binary):
    run = work / label
    run.mkdir()
    database = run / 'world.sqlite'
    pinned_copy(database)
    costs = run / 'costs.json'
    env = dict(report['environment'], OPENFANTASY_NATIVE_STARTUP_REPORT=str(costs))
    entry, stdout = execute(report, output, label, [DOTNET, binary / 'OpenFantasy.Host.dll', '--inspect-recorded-world',
                                                    database, str(HEAD_REVISION)], run, env, LIMITS['replaySeconds'])
    result = dict(label=label, binary=str(binary), exitCode=entry.get('exitCode'), timedOut=entry.get('timedOut', False),
                  wallSeconds=entry['wallSeconds'], childCpuSeconds=round(entry['childUserCpuSeconds'] + entry['childSystemCpuSeconds'], 3),
                  assemblies={name: digest(binary / name) for name in ASSEMBLIES}, copiedWorldUnchanged=digest(database) == WORLD_SHA256)
    if entry.get('exitCode') == 0:
        recorded = json.loads(stdout.read_text().strip().splitlines()[-1])
        result['recorded'] = {key: recorded.get(key) for key in ('SourceHeadRevision', 'SourceHeadHash', 'RecordedRevision', 'StateHash', 'ProfileVersion')}
    if costs.exists():
        shutil.copyfile(costs, output / (label + '.costs.json'))
        result['probe'] = summarize(costs)
    probe = result.get('probe', {})
    result['complete'] = (result['exitCode'] == 0 and not result['timedOut'] and result['copiedWorldUnchanged']
                          and result.get('recorded') == dict(SourceHeadRevision=HEAD_REVISION, SourceHeadHash=HEAD_HASH,
                                                             RecordedRevision=HEAD_REVISION, StateHash=HEAD_HASH, ProfileVersion=30)
                          and probe.get('reason') == 'process-exit' and not probe.get('open')
                          and probe.get('stages', {}).get('full-replay', {}).get('calls') == 1)
    report['replays'].append(result)
    return result


def compare(before, after):
    rows = {}
    for stage in COMPARED:
        a, b = before['probe']['stages'].get(stage), after['probe']['stages'].get(stage)
        if a is None and b is None:
            continue
        row = dict(before=a, after=b)
        if a and b:
            for key in ('threadAllocatedBytes', 'threadCpuMs', 'wallMs'):
                row[key + 'Delta'] = round(b[key] - a[key], 1)
                row[key + 'Ratio'] = round(b[key] / a[key], 4) if a[key] else None
        rows[stage] = row
    rows['process'] = dict(before=before['probe']['process'], after=after['probe']['process'])
    return dict(before=before['label'], after=after['label'], stages=rows)


def main():
    started = time.monotonic()
    stamp = time.strftime('%Y%m%dT%H%M%SZ', time.gmtime())
    output = PACKET / 'qualification' / ('measure-' + stamp)
    work = PRIVATE / 'measure' / stamp
    output.mkdir(parents=True)
    work.mkdir(parents=True)
    sdk = DOTNET.parent
    report = dict(kind='retained-resource-encoding-replay-v1', status='preparing', startedUtc=now(), limits=LIMITS,
                  deadline=started + LIMITS['aggregateSeconds'], work=str(work), commands=[], replays=[], comparisons=[],
                  environment=dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''),
                                   DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1', MSBUILDDISABLENODEREUSE='1',
                                   DOTNET_CLI_USE_MSBUILD_SERVER='0'),
                  limitation='Isolated instrumented diagnostic; not native, deadline or production acceptance. Inclusive '
                             'stages overlap; thread CPU/allocation belong to the replay thread. Cumulative allocation is not peak memory.')
    try:
        baseline = json.loads(BASELINE_REPORT.read_text())
        require(baseline.get('complete') and baseline['databaseSha256'] == WORLD_SHA256, 'Unexpected baseline report.')
        production, drift = production_state(baseline['sourcePins'])
        report['pinsBefore'] = dict(world=world_state(), baselineReportSha256=digest(BASELINE_REPORT),
                                    baselineAssemblies={name: digest(BASELINE_RUNTIME / BIN / name) for name in ASSEMBLIES})
        report['productionSinceBaselinePins'] = drift
        report['productionSha256'] = production
        require(not drift['added'] and not drift['removed']
                and set(drift['changed']) <= {str(RUNTIME / name) for name in OWNED},
                'Production changed outside the owned source scope.')
        report['buildConfigurationSha256'] = {}
        for name in ('Directory.Build.props', 'global.json', 'NuGet.Config'):
            require(digest(RUNTIME / name) == digest(BASELINE_RUNTIME / name),
                    'Production build configuration differs from the baseline: ' + name)
            report['buildConfigurationSha256'][name] = digest(RUNTIME / name)
        report['status'] = 'building'
        after = prepare_after(report, baseline, work, output)
        before = prepare_before(baseline, work)
        report['status'] = 'replaying'
        pairs = [('before-1', before), ('after-1', after), ('after-2', after), ('before-2', before)]
        slowest = 0.0
        for index, (label, binary) in enumerate(pairs):
            remaining = report['deadline'] - time.monotonic()
            if index >= 2 and remaining < 2 * slowest + 30:
                report.setdefault('skipped', []).append(dict(labels=[name for name, _ in pairs[index:]],
                                                             reason='aggregate bound cannot fit a complete second pair'))
                break
            result = replay(report, output, work, label, binary)
            slowest = max(slowest, result['wallSeconds'])
        complete = {result['label']: result for result in report['replays'] if result['complete']}
        for first, second in (('before-1', 'after-1'), ('before-2', 'after-2')):
            if first in complete and second in complete:
                report['comparisons'].append(compare(complete[first], complete[second]))
        report['status'] = 'diagnostic-complete' if report['comparisons'] else 'incomplete'
    except Exception as error:
        report.update(status='failed', failure=repr(error), traceback=traceback.format_exc())
    finally:
        try:
            report['pinsAfter'] = dict(world=world_state(), baselineReportSha256=digest(BASELINE_REPORT),
                                       baselineAssemblies={name: digest(BASELINE_RUNTIME / BIN / name) for name in ASSEMBLIES})
            report['pinsUnchanged'] = report['pinsAfter'] == report.get('pinsBefore')
            if 'productionSinceBaselinePins' in report:
                current, drift = production_state(json.loads(BASELINE_REPORT.read_text())['sourcePins'])
                report['productionUnchangedDuringMeasurement'] = current == report['productionSha256']
                report['configurationUnchangedDuringMeasurement'] = all(
                    digest(RUNTIME / name) == pin for name, pin in report.get('buildConfigurationSha256', {}).items())
            if (not report['pinsUnchanged'] or report.get('productionUnchangedDuringMeasurement') is False
                    or report.get('configurationUnchangedDuringMeasurement') is False):
                report['status'] = 'failed'
                report.setdefault('failure', 'A pinned input or production source changed during the diagnostic.')
        except Exception as error:
            report.update(status='failed', preservationFailure=repr(error))
        report['elapsedSeconds'] = round(time.monotonic() - started, 1)
        report['finishedUtc'] = now()
        del report['deadline']
        report['environment'] = {key: report['environment'][key] for key in
                                 ('DOTNET_ROOT', 'MSBUILDDISABLENODEREUSE', 'DOTNET_CLI_USE_MSBUILD_SERVER')}
        (output / 'result.json').write_text(json.dumps(report, indent=1) + '\n')
    print(json.dumps({key: report.get(key) for key in ('status', 'failure', 'elapsedSeconds', 'pinsUnchanged',
                                                       'productionUnchangedDuringMeasurement', 'productionSinceBaselinePins',
                                                       'skipped')}, indent=1))
    if report.get('traceback'):
        print(report['traceback'])
    for result in report['replays']:
        print(json.dumps({key: result.get(key) for key in ('label', 'complete', 'exitCode', 'timedOut', 'wallSeconds', 'recorded')}))
    for comparison in report['comparisons']:
        print('== %s -> %s ==' % (comparison['before'], comparison['after']))
        for stage, row in comparison['stages'].items():
            if stage == 'process':
                print('process', json.dumps(row))
                continue
            a, b = row['before'] or {}, row['after'] or {}
            print('%-32s calls %6s -> %-6s alloc %14s -> %-14s cpuMs %9s -> %-9s' % (
                stage, a.get('calls'), b.get('calls'), a.get('threadAllocatedBytes'), b.get('threadAllocatedBytes'),
                a.get('threadCpuMs'), b.get('threadCpuMs')))
    print('report:', output / 'result.json')
    return 0 if report['status'] == 'diagnostic-complete' else 1


if __name__ == '__main__':
    # Same lease as the integration runner: measurements must not overlap its heavy checks.
    with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lease:
        fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
        raise SystemExit(main())
