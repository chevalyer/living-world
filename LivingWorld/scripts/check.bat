@echo off
setlocal
cd /d "%~dp0.."
dotnet build LivingWorld.csproj --nologo
if errorlevel 1 exit /b 1
dotnet run --project Tests/LivingWorld.Tests.csproj -c Release
if errorlevel 1 exit /b 1
dotnet run --project Headless/LivingWorld.Headless.csproj -c Release -- --seed 1847 --size 96 --npcs 8 --ticks 1440 --json artifacts/smoke.json
if errorlevel 1 exit /b 1
