#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$project_dir"
dotnet build LivingWorld.csproj --nologo
dotnet run --project Tests/LivingWorld.Tests.csproj -c Release
dotnet run --project Headless/LivingWorld.Headless.csproj -c Release -- --seed 1847 --size 96 --npcs 8 --ticks 1440 --json artifacts/smoke.json
