"""Bounded integration contrasts, with actual terminal output and retained failures."""
from pathlib import Path
import datetime
import fcntl
import hashlib
import json
import subprocess
import time

packet = Path(__file__).resolve().parent
root = packet.parents[4]
runtime = root / 'Beta/Runtime'
out = packet / 'qualification' / ('focused-' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
out.mkdir(parents=True)
test = runtime / 'tests/OpenFantasy.Hosting.Tests/Simulation/RoadThreat/RoadThreatIntegrationContractTests.cs'
source_hash = hashlib.sha256(test.read_bytes()).hexdigest()
command = ['/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet', 'test',
           str(runtime / 'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'), '--no-restore',
           '-m:1', '-p:UseSharedCompilation=false', '--filter', 'FullyQualifiedName~RoadThreatIntegrationContractTests',
           '--logger', 'console;verbosity=detailed', '--logger', 'trx;LogFileName=focused.trx', '--results-directory', str(out)]
with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    started = time.monotonic()
    with (out / 'test.log').open('w') as log:
        result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=600)
    unchanged = hashlib.sha256(test.read_bytes()).hexdigest() == source_hash
    (out / 'result.json').write_text(json.dumps(dict(command=command, exitCode=result.returncode,
        elapsedSeconds=time.monotonic() - started, testSha256=source_hash, testUnchanged=unchanged), indent=2) + '\n')
    print(out)
    print('\n'.join((out / 'test.log').read_text().splitlines()[-85:]))
    assert unchanged, 'Test source changed during its run'
    raise SystemExit(result.returncode)
