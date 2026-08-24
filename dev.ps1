param([switch]$WithService)
$ErrorActionPreference="Stop"
Push-Location $PSScriptRoot
try {
  if ($WithService) {
    Write-Host ">> Iniciando Service (consola)..." -ForegroundColor Cyan
    $svc = Start-Process pwsh -ArgumentList "-NoExit","-Command","dotnet run --project src/CyberWall.Service" -PassThru
    Start-Sleep 1
    Write-Host ">> Iniciando UI..." -ForegroundColor Cyan
    dotnet run --project src/CyberWall.UI
    if (!$svc.HasExited) { Stop-Process -Id $svc.Id -ErrorAction SilentlyContinue }
  } else {
    Write-Host ">> Dev UI (1 terminal, engine embebido)..." -ForegroundColor Green
    dotnet run --project src/CyberWall.UI
  }
} finally { Pop-Location }
