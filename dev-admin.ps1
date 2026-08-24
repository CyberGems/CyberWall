if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Write-Host "Elevando a Administrador..." -ForegroundColor Yellow
  $exe = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }
  Start-Process $exe -ArgumentList "-NoExit -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
  exit
}
Set-Location $PSScriptRoot
Write-Host "Iniciando CyberWall en modo Administrador..." -ForegroundColor Cyan
Stop-Process -Name CyberWall,CyberWall.UI -Force -ErrorAction SilentlyContinue
dotnet run --project src/CyberWall.UI
