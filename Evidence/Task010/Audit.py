from pathlib import Path
import subprocess
import re
import json
import hashlib
import os

workspace=Path.cwd()
evidence=workspace/'Docs/Evidence/Task010'
initial_inventory=json.loads((evidence/'before-inventory.json').read_text())
repositories=['Build.CMake','Gateway.Api','Gateway.Grpc.Api','Gateway.Grpc','SeaPower.Client','SeaPower.Mod']
before_targets={}
audit={}
for name in repositories:
    source=workspace/name
    baseline_revision=initial_inventory[name]['head']
    baseline_files=subprocess.check_output(['git','-C',str(source),'ls-tree','-r','--name-only',baseline_revision],text=True).splitlines()
    before_targets[name]={}
    for relative in baseline_files:
        if relative.endswith('CMakeLists.txt'):
            text=subprocess.check_output(['git','-C',str(source),'show',baseline_revision+':'+relative],text=True)
            targets=re.findall(r'\b(add_library|add_executable|add_custom_target|add_test)\s*\(\s*(?:NAME\s+)?([A-Za-z0-9_.:-]+)',text)
            if targets:
                before_targets[name][relative]=targets
    unexpected=[]
    for directory, children, files in os.walk(source):
        current=Path(directory)
        if current==source:
            children[:]=[child for child in children if child not in ('o','.git')]
        for child in children:
            if child in ('bin','obj','out','packages','vcpkg_installed','CMakeFiles'):
                unexpected.append(str((current/child).relative_to(source)))
        for filename in files:
            if filename.endswith(('.dll','.exe','.lib','.obj','.pdb','.deps.json','.runtimeconfig.json','.cache')) or filename in ('project.assets.json','CMakeCache.txt','build.ninja','compile_commands.json'):
                unexpected.append(str((current/filename).relative_to(source)))
    assert not unexpected, (name,unexpected)
    handwritten_cs=[relative for relative in subprocess.check_output(['git','-C',str(source),'ls-files','--cached','--others','--exclude-standard'],text=True).splitlines() if relative.endswith('.cs') and (source/relative).exists()]
    assert all(relative.startswith(('managed/','test/managed/')) for relative in handwritten_cs),handwritten_cs
    audit[name]={'unexpectedGeneratedOutsideO':unexpected,'handwrittenCs':handwritten_cs,'gitStatus':subprocess.check_output(['git','-C',str(source),'status','--short'],text=True).splitlines()}
    compile_records=[]
    for configuration in ('debug','release'):
        commands=source/f'o/windows-msvc-{configuration}/compile_commands.json'
        if commands.exists():
            rows=json.loads(commands.read_text())
            expected='/MTd' if configuration=='debug' else '/MT'
            assert rows and all(re.search(r'[-/]MTd\b' if configuration == 'debug' else r'[-/]MT\b', row['command']) and not re.search(r'[-/]MDd?\b', row['command']) for row in rows)
            compile_records.append({'configuration':configuration,'translationUnits':len(rows),'matchingRuntime':expected,'sampleCommand':rows[0]['command']})
    audit[name]['nativeRuntimeFlags']=compile_records
(evidence/'before-cmake-targets.json').write_text(json.dumps(before_targets,indent=2))
(evidence/'generated-artifact-audit.json').write_text(json.dumps(audit,indent=2))

# Source relocations must preserve handwritten behavior byte-for-byte.
relocations={}
client=workspace/'SeaPower.Client'
baseline_revision=initial_inventory['SeaPower.Client']['head']
for relative in subprocess.check_output(['git','-C',str(client),'ls-tree','-r','--name-only',baseline_revision],text=True).splitlines():
    if relative.endswith('.cs'):
        destination=('managed/'+relative if relative.startswith('src/') else relative.replace('test/','test/managed/',1))
        original=subprocess.check_output(['git','-C',str(client),'show',baseline_revision+':'+relative])
        current=(client/destination).read_bytes()
        canonical = current.replace(b'\r\n', b'\n')
        assert original==canonical,relative
        relocations[relative]={'destination':destination,'sha256':hashlib.sha256(current).hexdigest(),'canonicalGitBlobSha256':hashlib.sha256(canonical).hexdigest(),'unchangedAfterLineEndingNormalization':True}
(evidence/'source-relocation-hashes.json').write_text(json.dumps(relocations,indent=2))

packages={}
for kind in ('package','harness'):
    stage=workspace/'SeaPower.Mod/o'/kind/'0.1.0'
    entries=json.loads((stage/'manifest.json').read_text(encoding='utf-8-sig'))
    for entry in entries:
        assert hashlib.sha256((stage/entry['file']).read_bytes()).hexdigest().upper()==entry['sha256']
    files=sorted(str(path.relative_to(stage)).replace('\\','/') for path in stage.rglob('*') if path.is_file())
    assert set(files)-{'manifest.json'}=={entry['file'] for entry in entries}
    if kind=='package':
        allowed={'SeaPower.Mod.dll','SeaPower.Client.dll','Gateway.Api.dll','Google.Protobuf.dll','SeaPower.Host.dll','config.json','manifest.json'}
        assert all(path in allowed or path.startswith('licenses/') for path in files)
        assert not json.loads((stage/'config.json').read_text())['enabledByDefault']
    else:
        assert all(path in ('SeaPower.Harness.dll','manifest.json') or path.startswith('scenarios/') for path in files)
    packages[kind]={'stage':str(stage),'files':files,'checksums':'PASS','configuration':'Release (latest stage)'}
(evidence/'package-audit.json').write_text(json.dumps(packages,indent=2))
print('PASS generated artifact, source move, static CRT and package audits')
