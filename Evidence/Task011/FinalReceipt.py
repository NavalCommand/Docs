import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as xml

workspace = Path.cwd()
evidence = workspace / 'Docs/Evidence/Task011'
records = {}
for name in ['Domain.Api', 'Build.CMake', 'Gateway.Api', 'Gateway.Grpc.Api', 'Gateway.Grpc', 'SeaPower.Client', 'SeaPower.Mod']:
    root = workspace / name
    records[name] = {
        'commit': subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip(),
        'status': subprocess.check_output(['git', '-C', str(root), 'status', '--short'], text=True).splitlines(),
    }
    tracked = subprocess.check_output(['git', '-C', str(root), 'ls-files'], text=True).splitlines()
    configuration_files = [relative for relative in tracked if relative.endswith(('.cmake', 'CMakeLists.txt', 'CMakePresets.json', '.csproj', '.props', '.targets'))]
    machine_paths = []
    for relative in configuration_files:
        contents = (root / relative).read_text(encoding='utf-8')
        if 'C:/Software/GitHub/NavalCommand' in contents or 'C:\\Software\\GitHub\\NavalCommand' in contents:
            machine_paths.append(relative)
    assert not machine_paths, (name, machine_paths)
    records[name]['trackedMachineSpecificBuildPaths'] = machine_paths
    records[name]['ignoredLocalOverride'] = subprocess.run(['git', '-C', str(root), 'check-ignore', 'Build.Local.cmake'], capture_output=True, text=True).returncode == 0
records['Domain.Api']['remotes'] = subprocess.check_output(['git', '-C', str(workspace / 'Domain.Api'), 'remote', '-v'], text=True).splitlines()
assert not records['Domain.Api']['remotes']
(evidence / 'final-revisions.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
generated = {}
api = workspace / 'Gateway.Api'
for relative in sorted((api / 'o/windows-msvc-release/src/generated').rglob('*.pb.*')):
    name = relative.relative_to(api / 'o/windows-msvc-release/src/generated')
    debug = api / 'o/windows-msvc-debug/src/generated' / name
    release_hash = hashlib.sha256(relative.read_bytes()).hexdigest()
    assert release_hash == hashlib.sha256(debug.read_bytes()).hexdigest(), name
    generated[name.as_posix()] = {'sha256': release_hash, 'debugReleaseIdentical': True}
(evidence / 'generated-protobuf-hashes.json').write_text(json.dumps(generated, indent=2), encoding='utf-8')
managed = {}
for name in ['Incidents.cs', 'Orders.cs', 'Session.cs', 'World.cs']:
    paths = sorted((api / 'o/managed/Gateway.Api/obj').rglob(name))
    hashes = {hashlib.sha256(path.read_bytes()).hexdigest() for path in paths if path.parent.name == 'generated'}
    assert len(hashes) == 1, (name, hashes)
    managed[name] = {'sha256': hashes.pop(), 'allConfigurationsIdentical': True}
(evidence / 'generated-csharp-hashes.json').write_text(json.dumps(managed, indent=2), encoding='utf-8')

# Compare descriptors generated from the pre-task Git schema to the working schema.
baseline = json.loads((evidence / 'before-inventory.json').read_text(encoding='utf-8'))['Gateway.Api']['head']
schema_fixture = workspace / 'Domain.Api/o/schema-comparison'
schema_fixture.mkdir(parents=True, exist_ok=True)
protoc = xml.parse(api / 'o/managed/Build.Context.props').find('.//Protoc').text
proto_files = subprocess.check_output(['git', '-C', str(api), 'ls-tree', '-r', '--name-only', baseline, 'proto'], text=True).splitlines()
for relative in proto_files:
    destination = schema_fixture / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(subprocess.check_output(['git', '-C', str(api), 'show', baseline + ':' + relative]))
descriptors = {}
for label, source in [('baseline', schema_fixture / 'proto'), ('current', api / 'proto')]:
    output = schema_fixture / f'{label}.pb'
    arguments = [protoc, '--proto_path=' + str(source), '--include_imports', '--descriptor_set_out=' + str(output)]
    arguments += [str(source / Path(relative).relative_to('proto')) for relative in proto_files if relative.endswith('.proto')]
    subprocess.run(arguments, check=True, capture_output=True)
    descriptors[label] = hashlib.sha256(output.read_bytes()).hexdigest()
assert descriptors['baseline'] == descriptors['current']
(evidence / 'descriptor-compatibility.json').write_text(json.dumps(descriptors, indent=2), encoding='utf-8')
print('PASS revision, tracked build-path and generated protobuf consistency audits')
