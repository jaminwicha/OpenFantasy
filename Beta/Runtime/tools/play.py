"""Build the pinned native farm client and open the retained local feedback world."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys


def executable(explicit, environment, bundled, names):
    if explicit or os.environ.get(environment):
        candidate = Path(explicit or os.environ[environment]).expanduser().resolve()
        if not candidate.is_file():
            raise RuntimeError(f"The selected {environment} executable does not exist: {candidate}")
        return candidate
    if bundled.is_file():
        return bundled
    for name in names:
        found = shutil.which(name)
        if found:
            return Path(found).resolve()
    raise RuntimeError(f"Supply {environment} or the corresponding command-line option for the pinned tool.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", help="Pinned dotnet executable; alternatively OPENFANTASY_DOTNET")
    parser.add_argument("--godot", help="Pinned Godot .NET executable; alternatively OPENFANTASY_GODOT")
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument("--care", action="store_true", help="Open the separate encounter journey with finite companion care")
    selection.add_argument("--restorative", action="store_true", help="Open the separate ordinary-care journey with a personal Potion and permitted companion aid")
    selection.add_argument("--encounters", action="store_true", help="Open the separate in-place encounter journey save")
    selection.add_argument("--breach", action="store_true", help="Open the separate earned terrain-breach save")
    selection.add_argument("--watercourse", action="store_true", help="Open the separate finite-water farm save")
    selection.add_argument("--worksite", action="store_true", help="Open the separate fresh-content worksite save; preserve every farm save")
    parser.add_argument("--prepare-only", action="store_true", help="Check, build and import without opening or selecting a save")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    pins = json.loads((root / "toolchain.json").read_text())
    installed = Path.home() / ".local/share/openfantasy/toolchains"
    try:
        dotnet = executable(args.dotnet, "OPENFANTASY_DOTNET",
                            installed / ("dotnet-" + pins["sdk"]) / "dotnet", ("dotnet",))
        godot = executable(args.godot, "OPENFANTASY_GODOT",
                           installed / "godot-4.6/Godot_mono.app/Contents/MacOS/Godot",
                           ("godot-mono", "godot", "Godot"))
        env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent),
                   PATH=str(dotnet.parent) + os.pathsep + os.environ.get("PATH", ""))
        for tool, expected in ((dotnet, pins["sdk"]), (godot, pins["godot"])):
            result = subprocess.run([str(tool), "--version"], cwd=root, env=env,
                                    capture_output=True, text=True, check=True, timeout=30)
            if result.stdout.strip() != expected:
                raise RuntimeError(f"{tool.name} version must be {expected}; observed {result.stdout.strip()}")
        print("Building the retained OpenFantasy farm…", flush=True)
        subprocess.run([str(dotnet), "restore", "client/OpenFantasy.Client.csproj", "--locked-mode"],
                       cwd=root, env=env, check=True)
        subprocess.run([str(dotnet), "build", "client/OpenFantasy.Client.csproj", "--no-restore", "-c", "Debug"],
                       cwd=root, env=env, check=True)
        imported = subprocess.run([str(godot), "--headless", "--path", str(root / "client"), "--import"],
                                  cwd=root, env=env, capture_output=True, text=True, check=True, timeout=180)
        if "ERROR:" in imported.stdout + imported.stderr:
            raise RuntimeError("Godot import reported an error:\n" + imported.stdout + imported.stderr)
        if args.prepare_only:
            print("Pinned native client prepared. No gameplay save was opened.", flush=True)
            return 0
        place = "restorative journey" if args.restorative else "care journey" if args.care else "encounters" if args.encounters else "breach" if args.breach else "watercourse" if args.watercourse else "worksite" if args.worksite else "farm"
        print(f"Opening Wren’s {place}. WASD/arrows: walk; E: talk; G: quest; F: gather; Q: target; V: actions; Esc: pause.", flush=True)
        command = [str(godot), "--path", str(root / "client")]
        if args.restorative:
            command += ["--", "--restorative"]
        elif args.care:
            command += ["--", "--care"]
        elif args.encounters:
            command += ["--", "--encounters"]
        elif args.breach:
            command += ["--", "--breach"]
        elif args.watercourse:
            command += ["--", "--watercourse"]
        elif args.worksite:
            command += ["--", "--worksite"]
        return subprocess.run(command, cwd=root, env=env).returncode
    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
