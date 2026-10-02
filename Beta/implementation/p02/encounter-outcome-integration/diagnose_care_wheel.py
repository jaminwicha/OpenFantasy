"""One isolated care-wheel diagnostic; does not replace the aggregate native or journal gates."""
import fcntl
import importlib.util
import json
import os
from pathlib import Path
import shutil
import tempfile
import time
import run_checks as checks

packet = Path(__file__).resolve().parent
runtime = checks.R
spec = importlib.util.spec_from_file_location('outcome_checker', runtime / 'tools/check_native_encounter_outcome.py')
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)
work = Path(tempfile.mkdtemp(prefix='care-wheel-diagnostic-', dir=packet / 'qualification'))
print(work, flush=True)
report = dict(status='running', aggregateGateSatisfied=False, runs=[], workDirectory=str(work))
start = time.monotonic()
with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    try:
        report['preservationBefore'] = checks.preservation()
        bp, build = checks.latest('build')
        source = checks.sources()
        assemblies = checks.assemblies()
        assert build['sourceSha256'] == source and build['assemblySha256'] == assemblies
        report.update(buildReport=str(bp), sourceSha256=source, assemblySha256=assemblies)
        fp, fixtures = checks.latest('outcome-fixtures')
        checks.verify_fixture_dependencies(fixtures, source, report)
        assert all(checks.sha(fp.parent / n) == h for n, h in fixtures['fixtureSha256'].items())
        fixture = Path(fixtures['outcomeFixtures']) / 'care'
        save = work / 'care-wheel.sqlite'
        shutil.copyfile(fixture / 'save.sqlite', save)
        client = runtime / 'client'
        launch = work / 'background-client'
        launch.mkdir()
        for path in client.iterdir():
            if path.name != 'project.godot':
                (launch / path.name).symlink_to(path, target_is_directory=path.is_dir())
        config = (client / 'project.godot').read_text()
        assert config.count('[display]') == 1 and 'window/size/no_focus' not in config
        (launch / 'project.godot').write_text(config.replace('[display]', '[display]\nwindow/size/no_focus=true', 1))
        result = work / 'care-wheel.json'
        sdk = str(Path(checks.DOTNET).parent)
        env = dict(os.environ, DOTNET_ROOT=sdk, PATH=sdk + os.pathsep + os.environ.get('PATH', ''))
        command = [checks.GODOT, '--path', str(launch), checker.ENTRY_SCENE, '--', '--cargo-outcomes-probe',
                   str(save), str(result), str(fixture / 'genesis.ofc'), str(fixture / 'metadata.json'),
                   'care-wheel', '--background-render']
        checker.run(command, runtime, env, work, 'care-wheel-active', checker.LIMITS['deviceProcessSeconds'], report)
        observed = json.loads(result.read_text())
        checker.verify(observed, 'care-wheel', True)
        checker.verify_submissions(observed, 'care-wheel')
        checker.journey(observed, 'care-wheel')
        assert not any(Path(str(save) + suffix).exists() for suffix in ('-wal', '-shm'))
        assert source == checks.sources() and assemblies == checks.assemblies()
        assert all(checks.sha(fp.parent / n) == h for n, h in fixtures['fixtureSha256'].items())
        report['preservationAfter'] = checks.preservation()
        report['status'] = 'passed_diagnostic_only'
    except Exception as error:
        report['status'] = 'failed'
        report['error'] = repr(error)
    report['elapsedSeconds'] = time.monotonic() - start
    (work / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('sourceSha256','assemblySha256','runs')}), flush=True)
raise SystemExit(0 if report['status'] == 'passed_diagnostic_only' else 1)
