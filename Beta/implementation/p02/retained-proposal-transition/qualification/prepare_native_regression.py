"""Copy retained native gift outputs before reauthenticating with current Hosting assemblies.
This does not launch Godot or manufacture a new rendered result.
"""
import hashlib
import json
import shutil
import sys
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source = Path(sys.argv[1]).resolve()
    output = Path(sys.argv[2]).resolve()
    retained = json.loads(source.read_text())
    assert retained['status'] == 'passed'
    assert not output.exists(), 'Use a new evidence directory'
    output.mkdir(parents=True)
    pins = {str(source): digest(source)}
    manifest = {'results': []}
    for result in retained['results']:
        copied = dict(result)
        directory = output / result['mode']
        directory.mkdir()
        for key in ('save', 'genesis', 'before', 'metadata', 'report', 'reopenReport'):
            path = Path(result[key])
            if key == 'save':
                assert not Path(str(path) + '-wal').exists(), 'Source must be closed/checkpointed'
                assert digest(path) == result['finalDatabaseSha256']
            target = directory / (key + path.suffix)
            pins[str(path)] = digest(path)
            shutil.copyfile(path, target)
            assert digest(target) == pins[str(path)]
            copied[key] = str(target)
        manifest['results'].append(copied)
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    (output / 'input-pins.json').write_text(json.dumps(pins, indent=2) + '\n')
    print(output / 'manifest.json')


if __name__ == '__main__':
    main()
