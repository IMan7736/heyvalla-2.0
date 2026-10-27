#!/usr/bin/env bash
# Runs dotnet for apps/api with an isolated artifacts path.
#   scripts/api.sh watch|run|build|test|datatools <args>
# AGENT (default: atoll) selects .artifacts/<agent>; DOTNET is found in PATH or ~/.dotnet.
set -euo pipefail
cd "$(dirname "$0")/../apps/api"

if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
  export PATH="$HOME/.dotnet:$PATH"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

agent="${AGENT:-atoll}"
artifacts=(--artifacts-path ".artifacts/${agent}")
cmd="${1:-watch}"
shift || true

case "$cmd" in
  watch) exec dotnet watch --project src/Dhathuru.Api "${artifacts[@]}" run "$@" ;;
  run)   exec dotnet run --project src/Dhathuru.Api "${artifacts[@]}" "$@" ;;
  build) exec dotnet build Dhathuru.sln "${artifacts[@]}" "$@" ;;
  datatools) exec dotnet run --project src/Dhathuru.DataTools "${artifacts[@]}" -- "$@" ;;
  test)  exec dotnet test --solution Dhathuru.sln "${artifacts[@]}" "$@" ;;
  *) echo "usage: scripts/api.sh watch|run|build|test|datatools <command>" >&2; exit 2 ;;
esac
