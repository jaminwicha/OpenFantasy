"""Bounded diagnosis of the retained active journey's paused opening; no active journey is replayed."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import traceback

runtime = Path('/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/Runtime')
sys.path.insert(0, str(runtime / 'tools'))
from check_native_adventure import (authenticate, closed_database, retained_rows, verify_journey,
                                    verify_pins, verify_reopen, verify_paused_database, run, digest)
packet = runtime.parent / 'implementation/p02/adventure-journey'
prior_path = packet / 'checks/native-draw-diagnostic-03.json'
prior = json.loads(prior_path.read_text())
old = Path(prior['workDirectory'])
work = Path(tempfile.mkdtemp(prefix='openfantasy-adventure-reopen-rows-'))
report = dict(status='running', workDirectory=str(work), sourceSave=prior['sourceSave'],
              inheritedRun=str(prior_path), inheritedRunSha256=digest(prior_path), runs=[])
output = packet / 'checks/reopen-row-diagnostic-04.json'
assert not output.exists()
try:
    pins = prior['executionPins']
    checker = str(runtime / 'tools/check_native_adventure.py')
    verify_pins({path: expected for path, expected in pins.items() if path != checker})
    report['inheritedExecutionDependencies'] = len(pins) - 1
    report['checkerRevision'] = {'path': checker, 'before': pins[checker], 'after': digest(checker)}
    report['sourceSha256'] = digest(report['sourceSave'])
    assert report['sourceSha256'] == prior['sourceSha256']
    report['executionPins'] = {**pins, checker: digest(checker), str(Path(__file__).resolve()): digest(__file__)}
    observed = json.loads((old / 'keyboard.json').read_text())
    verify_journey(observed)
    report['authentication'] = authenticate(old / 'adventure.sqlite', observed, retained_rows(Path(report['sourceSave'])))
    shutil.copy2(old / 'keyboard.json', work / 'keyboard.json')
    shutil.copy2(old / 'adventure.sqlite', work / 'before.sqlite')
    shutil.copy2(work / 'before.sqlite', work / 'adventure.sqlite')
    before = closed_database(work / 'before.sqlite')
    dotnet = Path('/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet')
    godot = '/Users/alucinor/.local/share/openfantasy/toolchains/godot-4.6/Godot_mono.app/Contents/MacOS/Godot'
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent), PATH=str(dotnet.parent)+os.pathsep+os.environ.get('PATH',''))
    command = [godot, '--path', str(old / 'background-client'), '--', '--adventure-journey',
               str(work / 'adventure.sqlite'), str(work / 'reopen.json'), 'reopen', '--background-render']
    run(command, runtime, env, work, 'reopen', 90, report)
    reopened = json.loads((work / 'reopen.json').read_text())
    verify_reopen(reopened, observed)
    report['pausedReopening'] = verify_paused_database(before, closed_database(work / 'adventure.sqlite'))
    verify_pins(report['executionPins'])
    assert digest(report['sourceSave']) == report['sourceSha256']
    report.update(status='passed', beforeDatabaseSha256=digest(work/'before.sqlite'),
                  finalDatabaseSha256=digest(work/'adventure.sqlite'),
                  activeReport=str(old/'keyboard.json'), reopenReport=str(work/'reopen.json'))
except Exception as error:
    report.update(status='failed', failure=str(error), traceback=traceback.format_exc())
output.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:report[k] for k in ('status','workDirectory','failure','pausedReopening') if k in report}))
raise SystemExit(0 if report['status']=='passed' else 1)
