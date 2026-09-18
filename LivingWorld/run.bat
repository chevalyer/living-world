@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install .NET 8 SDK first: https://dotnet.microsoft.com/download/dotnet/8.0
  exit /b 1
)
if not "%~1"=="" set "GODOT_BIN=%~1"
if not defined GODOT_BIN set "GODOT_BIN=godot"
dotnet build LivingWorld.csproj --nologo
if errorlevel 1 exit /b 1
"%GODOT_BIN%" --path "%~dp0."
if errorlevel 1 (
  echo Launch failed. Pass the path to the Godot 4.5 .NET executable as the first argument.
  exit /b 1
)
