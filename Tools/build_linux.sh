#!/usr/bin/env bash
# Local Linux build. Usage: UNITY_PATH=/path/to/Unity ./Tools/build_linux.sh
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_PATH="${UNITY_PATH:-$HOME/Unity/Hub/Editor/2022.3.22f1/Editor/Unity}"
if [ ! -x "$UNITY_PATH" ]; then
  echo "[!] Unity not found at: $UNITY_PATH"
  echo "    Run: UNITY_PATH=/path/to/Unity ./Tools/build_linux.sh"
  exit 1
fi
"$UNITY_PATH" -batchmode -nographics -quit \
  -projectPath "$ROOT" \
  -executeMethod IronDistrict.EditorTools.CiBuild.BuildLinux \
  -logFile "$ROOT/Tools/build_linux.log"
echo "Done: build/StandaloneLinux64/IronDistrict.x86_64"
