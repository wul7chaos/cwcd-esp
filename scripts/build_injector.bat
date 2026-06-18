@echo off
setlocal
chcp 65001 >nul
REM ===== Build CwcdEspInjector (net10.0, self-contained, no game deps) =====

set "OUT=%~dp0..\deploy"
pushd "%~dp0.."
dotnet publish CwcdEspInjector\CwcdEspInjector.csproj -c Release -o "%OUT%"
set "RC=%ERRORLEVEL%"
popd
exit /b %RC%
