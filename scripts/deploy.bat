@echo off
chcp 65001 >nul
REM Deploy script - build + publish + copy to deploy/

set "ROOT=%~dp0.."
set "OUT=%ROOT%\deploy"
set "LIB=%ROOT%\CwcdEspLibrary"
set "INJ=%ROOT%\CwcdEspInjector"

echo ===== 1/2 Build CwcdEspLibrary (Release) =====
dotnet build "%LIB%\CwcdEspLibrary.csproj" -c Release
if errorlevel 1 (
  echo [!] Library build failed.
  exit /b 1
)

echo.
echo ===== 2/2 Publish CwcdEspInjector (SingleFile, win-x64) =====
dotnet publish "%INJ%\CwcdEspInjector.csproj" -c Release -r win-x64 --self-contained true -o "%OUT%"
if errorlevel 1 (
  echo [!] Injector publish failed.
  exit /b 1
)

echo.
echo ===== Copy CwcdEspLibrary + 0Harmony to deploy\ =====
if not exist "%OUT%" mkdir "%OUT%"

copy /y "%LIB%\bin\Release\CwcdEspLibrary.dll" "%OUT%\" >nul
echo [*] Copied CwcdEspLibrary.dll
copy /y "%LIB%\bin\Release\0Harmony.dll" "%OUT%\" >nul
echo [*] Copied 0Harmony.dll

REM 移除 pdb（可选）
del /q "%OUT%\*.pdb" 2>nul

echo.
echo [OK] Deploy complete: %OUT%
echo     CwcdEspInjector.exe - single file, no .NET 10 required
echo     Run as admin: %OUT%\CwcdEspInjector.exe
exit /b 0
