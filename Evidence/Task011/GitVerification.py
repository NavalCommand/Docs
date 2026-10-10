import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

workspace = Path.cwd()
evidence = workspace / 'Docs/Evidence/Task011'
fixture = Path(tempfile.mkdtemp(prefix='run-', dir=workspace / 'Domain.Api/o'))
environment = os.environ.copy()
config = fixture / 'gitconfig'
config.write_text(f'[url "{(fixture / "repos").as_uri()}/"]\n\tinsteadOf = https://github.com/NavalCommand/\n', encoding='utf-8')
environment.update(GIT_CONFIG_GLOBAL=str(config), GIT_CONFIG_NOSYSTEM='1')
cmake = 'C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/cmake.exe'
ctest = str(Path(cmake).with_name('ctest.exe'))
records = {}


def run(arguments, log):
    result = subprocess.run(arguments, capture_output=True, text=True, env=environment)
    (evidence / log).write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(f'{arguments} failed: {log}')
    return result.stdout


for name in ['Build.CMake', 'Domain.Api', 'Gateway.Api', 'Gateway.Grpc.Api', 'Gateway.Grpc', 'SeaPower.Client', 'SeaPower.Mod']:
    source = workspace / name
    mirror = fixture / 'repos' / f'{name}.git'
    mirror.mkdir(parents=True)
    paths = subprocess.check_output(['git', '-C', str(source), 'ls-files', '--cached', '--others', '--exclude-standard'], text=True).splitlines()
    for relative in set(paths):
        original = source / relative
        if original.is_file():
            destination = mirror / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(original, destination)
    subprocess.run(['git', 'init', '-b', 'main', str(mirror)], check=True, capture_output=True)
    subprocess.run(['git', '-C', str(mirror), 'add', '.'], check=True, capture_output=True)
    subprocess.run(['git', '-C', str(mirror), '-c', 'user.name=Build Verification', '-c', 'user.email=build@example.invalid', 'commit', '-m', 'Current source snapshot'], check=True, capture_output=True)
    records[name] = subprocess.check_output(['git', '-C', str(mirror), 'rev-parse', 'HEAD'], text=True).strip()
for name in ['Gateway.Api', 'SeaPower.Mod']:
    source = fixture / name
    subprocess.run(['git', 'clone', str(fixture / 'repos' / f'{name}.git'), str(source)], check=True, capture_output=True)
    binary = source / 'o/git-release'
    run([cmake, '-S', str(source), '-B', str(binary), '-G', 'Ninja', '-DCMAKE_BUILD_TYPE=Release',
         '-DCMAKE_TOOLCHAIN_FILE=C:/vcpkg/scripts/buildsystems/vcpkg.cmake', '-DVCPKG_TARGET_TRIPLET=x64-windows-static', '-DVCPKG_HOST_TRIPLET=x64-windows-static',
         '-DVCPKG_INSTALLED_DIR=C:/Users/DmitriyPC/AppData/Local/Temp/naval-static-runtime-prefix', '-DVCPKG_MANIFEST_INSTALL=OFF',
         '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded$<$<CONFIG:Debug>:Debug>', '-DSEAPOWER_GAME_ROOT=C:/Games/Sea_Power_8_3'], f'git-{name}-configure.log')
    assert not (source / 'Build.Local.cmake').exists()
    resolution = (binary / 'dependency-resolution.log').read_text(encoding='utf-8')
    assert 'navalcommand.domain.api|main|' in resolution
    records[f'{name}-resolution'] = resolution.splitlines()
    if name == 'Gateway.Api':
        run([cmake, '--build', str(binary), '--parallel', '8'], 'git-api-build.log')
        run([ctest, '--test-dir', str(binary), '--output-on-failure'], 'git-api-test.log')
    else:
        run(['dotnet', 'build', str(source / 'managed/runtime/SeaPower.Mod.csproj'), '-c', 'Release', '--nologo'], 'git-direct-runtime.log')
records['fixture'] = str(fixture)
records['publicRemoteVerified'] = False
records['result'] = 'PASS isolated Git FetchContent with no local override; direct and transitive Domain.Api and managed graph'
(evidence / 'git-resolution.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
print(records['result'])
