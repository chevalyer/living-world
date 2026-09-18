#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$project_dir"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0" >&2
  exit 1
fi
godot_executable="${1:-${GODOT_BIN:-godot}}"
if ! command -v "$godot_executable" >/dev/null 2>&1; then
  echo "Pass the path to the Godot 4.7.1 .NET executable as the first argument." >&2
  exit 1
fi
dotnet build LivingWorld.csproj --nologo
exec "$godot_executable" --path "$project_dir"
