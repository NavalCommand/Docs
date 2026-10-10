from pathlib import Path
import os
import shutil
import subprocess
import tempfile
import json

workspace = Path.cwd()
fixture_root = workspace / "Build.CMake/o/git-source-verification"
fixture_root.mkdir(parents=True, exist_ok=True)
fixture = Path(tempfile.mkdtemp(prefix="run-", dir=fixture_root))
records = {}
environment = os.environ.copy()
config = fixture / "gitconfig"
config.write_text(f'[url "{(fixture / "repositories").as_uri()}/"]\n\tinsteadOf = https://github.com/NavalCommand/\n')
environment.update(GIT_CONFIG_GLOBAL=str(config), GIT_CONFIG_NOSYSTEM="1")


def run(arguments, log, succeeds=True):
    result = subprocess.run(arguments, capture_output=True, text=True, env=environment)
    (workspace / "Docs/Evidence/Task010" / log).write_text(result.stdout + result.stderr)
    if (result.returncode == 0) != succeeds:
        raise RuntimeError(f"{arguments} failed; see {log}")
    return result.stdout + result.stderr


for name in ["Build.CMake", "Gateway.Api", "Gateway.Grpc.Api", "Gateway.Grpc", "SeaPower.Client", "SeaPower.Mod"]:
    source = workspace / name
    mirror = fixture / "repositories" / f"{name}.git"
    mirror.mkdir(parents=True)
    paths = subprocess.check_output(["git", "-C", str(source), "ls-files", "--cached", "--others", "--exclude-standard"], text=True).splitlines()
    for relative in set(paths):
        original = source / relative
        if original.is_file():
            destination = mirror / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(original, destination)
    subprocess.run(["git", "init", "-b", "main", str(mirror)], check=True, capture_output=True)
    subprocess.run(["git", "-C", str(mirror), "add", "."], check=True, capture_output=True)
    subprocess.run(["git", "-C", str(mirror), "-c", "user.name=Build Verification", "-c", "user.email=build@example.invalid", "commit", "-m", "Working source snapshot"], check=True, capture_output=True)
    records[name] = subprocess.check_output(["git", "-C", str(mirror), "rev-parse", "HEAD"], text=True).strip()
root_source = fixture / "SeaPower.Mod"
subprocess.run(["git", "clone", str(fixture / "repositories/SeaPower.Mod.git"), str(root_source)], check=True, capture_output=True)
cmake = "C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/cmake.exe"
binary = root_source / "o/git-release"
run([cmake, "-S", str(root_source), "-B", str(binary), "-G", "Ninja", "-DCMAKE_BUILD_TYPE=Release",
     "-DCMAKE_TOOLCHAIN_FILE=C:/vcpkg/scripts/buildsystems/vcpkg.cmake", "-DVCPKG_TARGET_TRIPLET=x64-windows-static", "-DVCPKG_HOST_TRIPLET=x64-windows-static",
     "-DVCPKG_INSTALLED_DIR=C:/Users/DmitriyPC/AppData/Local/Temp/naval-static-runtime-prefix", "-DVCPKG_MANIFEST_INSTALL=OFF",
     "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded$<$<CONFIG:Debug>:Debug>", "-DSEAPOWER_GAME_ROOT=C:/Games/Sea_Power_8_3"], "git-source-configure.log")
assert not (root_source / "Build.Local.cmake").exists()
assert not list(binary.glob("_deps/*-subbuild"))
for configuration in ["Debug", "Release"]:
    run(["dotnet", "build", str(root_source / "managed/runtime/SeaPower.Mod.csproj"), "-c", configuration, "--nologo"], f"git-source-direct-runtime-{configuration.lower()}.log")
run(["dotnet", "build", str(root_source / "managed/harness/SeaPower.Harness.csproj"), "-c", "Release", "--nologo"], "git-source-direct-harness.log")
records["fixture"] = str(fixture)
records["resolution"] = (binary / "dependency-resolution.log").read_text().splitlines()
records["pass"] = ["actual source snapshot Git FetchContent, no Build.Local.cmake", "native and managed transitive graph configure", "direct runtime Debug/Release ProjectReference builds", "direct harness build", "hashed dependency names and CMP0168"]
records["public_remote_tested"] = False
sdkless_source = fixture / "sdkless/SeaPower.Mod"
subprocess.run(["git", "clone", str(fixture / "repositories/SeaPower.Mod.git"), str(sdkless_source)], check=True, capture_output=True)
sdkless_binary = sdkless_source / "o/native-release"
run([cmake, "-S", str(sdkless_source), "-B", str(sdkless_binary), "-G", "Ninja", "-DCMAKE_BUILD_TYPE=Release",
     "-DCMAKE_TOOLCHAIN_FILE=C:/vcpkg/scripts/buildsystems/vcpkg.cmake", "-DVCPKG_TARGET_TRIPLET=x64-windows-static", "-DVCPKG_HOST_TRIPLET=x64-windows-static",
     "-DVCPKG_INSTALLED_DIR=C:/Users/DmitriyPC/AppData/Local/Temp/naval-static-runtime-prefix", "-DVCPKG_MANIFEST_INSTALL=OFF",
     "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded$<$<CONFIG:Debug>:Debug>"], "sdkless-configure.log")
run([cmake, "--build", str(sdkless_binary), "--target", "SeaPower.Host", "host-tests", "abi-mismatch-fixture", "missing-exports-fixture", "--parallel", "8"], "sdkless-native-build.log")
run([str(Path(cmake).parent / "ctest.exe"), "--test-dir", str(sdkless_binary), "--output-on-failure", "--no-tests=error"], "sdkless-ctest.log")
failure = run([cmake, "--build", str(sdkless_binary), "--target", "managed-harness"], "sdkless-requested-harness.log", succeeds=False)
assert "Configure SEAPOWER_GAME_ROOT" in failure
records["pass"].append("native configure/build/CTest without game SDK; requested game-dependent harness clearly rejected")
(workspace / "Docs/Evidence/Task010/git-source-receipt.json").write_text(json.dumps(records, indent=2))
print("GIT_SOURCE_GRAPH_PASS " + str(fixture))
