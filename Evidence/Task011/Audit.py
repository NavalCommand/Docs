import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

workspace = Path.cwd()
evidence = workspace / 'Docs/Evidence/Task011'
repositories = ['Domain.Api', 'Build.CMake', 'Gateway.Api', 'Gateway.Grpc.Api', 'Gateway.Grpc', 'SeaPower.Client', 'SeaPower.Mod']
records = {}
for name in repositories:
    source = workspace / name
    unexpected = []
    for directory, children, files in os.walk(source):
        current = Path(directory)
        if current == source:
            children[:] = [child for child in children if child not in ('o', '.git')]
        for child in children:
            if child in ('bin', 'obj', 'out', 'packages', 'vcpkg_installed', 'CMakeFiles'):
                unexpected.append(str((current / child).relative_to(source)))
        for filename in files:
            if filename.endswith(('.dll', '.exe', '.lib', '.obj', '.pdb', '.deps.json', '.runtimeconfig.json', '.cache')) or filename in ('project.assets.json', 'CMakeCache.txt', 'build.ninja', 'compile_commands.json'):
                unexpected.append(str((current / filename).relative_to(source)))
    assert not unexpected, (name, unexpected)
    flags = []
    for configuration in ['debug', 'release']:
        commands = source / f'o/windows-msvc-{configuration}/compile_commands.json'
        if commands.exists():
            rows = json.loads(commands.read_text(encoding='utf-8'))
            runtime_pattern = r'[-/]MTd\b' if configuration == 'debug' else r'[-/]MT\b'
            assert all(re.search(runtime_pattern, row['command']) and not re.search(r'[-/]MDd?\b', row['command']) for row in rows)
            flags.append({'configuration': configuration, 'translationUnits': len(rows), 'staticRuntime': True})
    records[name] = {'unexpectedOutputsOutsideO': unexpected, 'runtimeFlags': flags}
    references = subprocess.run(['git', '-C', str(source), 'grep', '-n', '-E', 'Orders::Api|World::Api|NavalCommand/(Orders|World)/Api'], capture_output=True, text=True).stdout
    (evidence / f'{name}-references.txt').write_text(references, encoding='utf-8')
    if name not in ['Domain.Api']:
        tracked = subprocess.check_output(['git', '-C', str(source), 'ls-files'], text=True).splitlines()
        protected = [relative for relative in tracked if relative.endswith(('.proto', '.cs', '.csproj')) or relative.startswith('src/') and relative.endswith(('.cpp', '.h')) or relative.startswith('public/') and '/Orders/' not in relative and '/World/' not in relative]
        for relative in protected:
            original = subprocess.check_output(['git', '-C', str(source), 'show', 'HEAD:' + relative])
            canonical = (source / relative).read_bytes().replace(b'\r\n', b'\n')
            assert original.replace(b'\r\n', b'\n') == canonical, (name, relative)
        records[name]['unchangedProtectedFiles'] = len(protected)
assert not list((workspace / 'Gateway.Api/public/NavalCommand/Orders').rglob('*.h'))
assert not list((workspace / 'Gateway.Api/public/NavalCommand/World').rglob('*.h'))
for entry in json.loads((evidence / 'header-hashes.json').read_text(encoding='utf-8')):
    current_hash = hashlib.sha256((workspace / 'Domain.Api' / entry['path']).read_bytes()).hexdigest()
    assert current_hash == entry['before'] == entry['after']
(evidence / 'source-and-output-audit.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
packages = {}
for kind in ['package', 'harness']:
    stage = workspace / 'SeaPower.Mod/o' / kind / '0.1.0'
    entries = json.loads((stage / 'manifest.json').read_text(encoding='utf-8-sig'))
    for entry in entries:
        assert hashlib.sha256((stage / entry['file']).read_bytes()).hexdigest().upper() == entry['sha256']
    files = sorted(path.relative_to(stage).as_posix() for path in stage.rglob('*') if path.is_file())
    assert set(files) - {'manifest.json'} == {entry['file'] for entry in entries}
    if kind == 'package':
        allowed = {'SeaPower.Mod.dll', 'SeaPower.Client.dll', 'Gateway.Api.dll', 'Google.Protobuf.dll', 'SeaPower.Host.dll', 'config.json', 'manifest.json'}
        assert all(path in allowed or path.startswith('licenses/') for path in files)
        assert not json.loads((stage / 'config.json').read_text(encoding='utf-8'))['enabledByDefault']
    else:
        assert all(path in ('SeaPower.Harness.dll', 'manifest.json') or path.startswith('scenarios/') for path in files)
    packages[kind] = {'files': files, 'manifest': entries, 'checksumAndExclusionChecks': 'PASS'}
configuration = os.environ.get('AUDIT_CONFIGURATION', 'Release')
(evidence / f'package-audit-{configuration}.json').write_text(json.dumps(packages, indent=2), encoding='utf-8')
print('PASS header identity, unchanged schemas/managed/native logic, static CRT, output containment and package audits')
