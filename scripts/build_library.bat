@echo off
setlocal
chcp 65001 >nul
REM ===== Build CwcdEspLibrary (net472, needs game Managed dir) =====
REM Usage: build_library.bat "<game Managed dir>"
REM   e.g. build_library.bat "D:\SteamLibrary\steamapps\common\CWCD\CWCD_Data\Managed"

set "GAMEMANAGED=%~1"
if "%GAMEMANAGED%"=="" (
  if exist "%~dp0..\GamePaths.local.props" (
    echo [*] GameManagedDir not given, using GamePaths.local.props
  ) else (
    echo [!] Usage: build_library.bat "<game Managed dir>"
    echo     e.g. build_library.bat "D:\SteamLibrary\steamapps\common\CWCD\CWCD_Data\Managed"
    echo     Or create GamePaths.local.props from the .example file.
    exit /b 1
  )
)

pushd "%~dp0.."
if not "%GAMEMANAGED%"=="" (
  dotnet build CwcdEspLibrary\CwcdEspLibrary.csproj -c Release -p:Platform=AnyCPU -p:GameManagedDir="%GAMEMANAGED%"
) else (
  dotnet build CwcdEspLibrary\CwcdEspLibrary.csproj -c Release -p:Platform=AnyCPU
)
set "RC=%ERRORLEVEL%"
popd
exit /b %RC%
