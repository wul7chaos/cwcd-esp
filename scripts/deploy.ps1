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
Write-Host "===== 2/2 Publish CwcdEspInjector (Native AOT, win-x64) ====="
# Native AOT 编译为原生机器码，无需 .NET 运行时，exe 约 1.6MB
# 产物在 publish/ 目录，直接输出到 deploy/
dotnet publish (Join-Path $INJ "CwcdEspInjector.csproj") -c Release -r win-x64 -o "$OUT"
if ($LASTEXITCODE -ne 0) { Write-Error "Injector publish failed"; exit 1 }

Write-Host ""
Write-Host "===== Copy CwcdEspLibrary + 0Harmony to deploy/ ====="
Copy-Item "$LIB/bin/Release/CwcdEspLibrary.dll" "$OUT" -Force
Write-Host "[*] Copied CwcdEspLibrary.dll"
Copy-Item "$LIB/bin/Release/0Harmony.dll" "$OUT" -Force
Write-Host "[*] Copied 0Harmony.dll"

# 移除 pdb（AOT 编译的 pdb 仅用于原生调试，分发不需要）
Remove-Item "$OUT/*.pdb" -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "[OK] Deploy complete: $OUT"
$exeSize = [math]::Round((Get-Item "$OUT/CwcdEspInjector.exe").Length / 1MB, 2)
Write-Host "    CwcdEspInjector.exe - Native AOT, $exeSize MB, no .NET runtime required"
Write-Host "    Run as admin: $OUT\CwcdEspInjector.exe"
