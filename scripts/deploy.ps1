# ===== Deploy: build + publish + copy to deploy/ =====
# Usage: cd scripts; .\deploy.ps1

$ErrorActionPreference = "Stop"
$ROOT = Split-Path $PSScriptRoot -Parent
$OUT  = Join-Path $ROOT "deploy"
$LIB  = Join-Path $ROOT "CwcdEspLibrary"
$INJ  = Join-Path $ROOT "CwcdEspInjector"

Write-Host "===== 1/2 Build CwcdEspLibrary (Release) ====="
dotnet build (Join-Path $LIB "CwcdEspLibrary.csproj") -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Library build failed"; exit 1 }

Write-Host ""
Write-Host "===== 2/2 Publish CwcdEspInjector (SingleFile, win-x64) ====="
dotnet publish (Join-Path $INJ "CwcdEspInjector.csproj") -c Release -r win-x64 --self-contained true -o "$OUT"
if ($LASTEXITCODE -ne 0) { Write-Error "Injector publish failed"; exit 1 }

Write-Host ""
Write-Host "===== Copy CwcdEspLibrary + 0Harmony to deploy/ ====="
Copy-Item "$LIB/bin/Release/CwcdEspLibrary.dll" "$OUT" -Force
Write-Host "[*] Copied CwcdEspLibrary.dll"
Copy-Item "$LIB/bin/Release/0Harmony.dll" "$OUT" -Force
Write-Host "[*] Copied 0Harmony.dll"

# 移除 pdb（可选）
Remove-Item "$OUT/*.pdb" -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "[OK] Deploy complete: $OUT"
Write-Host "    CwcdEspInjector.exe - single file, no .NET 10 required"
Write-Host "    Run as admin: $OUT\CwcdEspInjector.exe"
