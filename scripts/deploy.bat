@echo off
setlocal
chcp 65001 >nul
REM ===== Deploy: build library + injector, gather into deploy\ =====
REM Usage: deploy.bat "[game Managed dir]"
REM   不传参时使用 GamePaths.local.props 中的 GameManagedDir

set "GAMEMANAGED=%~1"
set "OUT=%~dp0..\deploy"
set "LIBOUT=%~dp0..\CwcdEspLibrary\bin\x64\Release"
set "INJOUT=%~dp0..\CwcdEspInjector\bin\x64\Release"

if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%" >nul 2>&1

echo ===== 1/2 Build CwcdEspLibrary =====
if "%GAMEMANAGED%"=="" (
  call "%~dp0build_library.bat"
) else (
  call "%~dp0build_library.bat" "%GAMEMANAGED%"
)
if errorlevel 1 (
  echo [!] Library build failed.
  exit /b 1
)

echo ===== 2/2 Build CwcdEspInjector =====
call "%~dp0build_injector.bat"
if errorlevel 1 (
  echo [!] Injector build failed.
  exit /b 1
)

REM Copy library dll + Harmony dependency
if exist "%LIBOUT%\CwcdEspLibrary.dll" (
  copy /y "%LIBOUT%\CwcdEspLibrary.dll" "%OUT%\" >nul
  echo [*] Copied CwcdEspLibrary.dll
) else (
  echo [!] CwcdEspLibrary.dll not found at: %LIBOUT%
)
if exist "%LIBOUT%\0Harmony.dll" (
  copy /y "%LIBOUT%\0Harmony.dll" "%OUT%\" >nul
  echo [*] Copied 0Harmony.dll
) else (
  echo [!] 0Harmony.dll not found — 运行时加载补丁库会失败！
)

REM Copy injector exe + its native deps
if exist "%INJOUT%\CwcdEspInjector.exe" (
  copy /y "%INJOUT%\CwcdEspInjector.exe" "%OUT%\" >nul
  echo [*] Copied CwcdEspInjector.exe
) else (
  echo [!] CwcdEspInjector.exe not found at: %INJOUT%
)

echo.
echo [OK] Deploy complete: %OUT%
echo     Run as admin: %OUT%\CwcdEspInjector.exe
echo     Default target process: NoSuchPlace
exit /b 0
