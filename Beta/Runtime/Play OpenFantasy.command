#!/bin/sh
set -eu
runtime_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec python3 "$runtime_directory/tools/play.py"
