if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  Write-Host "Elevando a Administrador..." -ForegroundColor Yellow
  Start-Process pwsh -ArgumentList "-ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
  exit
}
Set-Location $PSScriptRoot
dotnet run --project src/CyberWall.UI
