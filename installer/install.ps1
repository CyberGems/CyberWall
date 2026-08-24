param([switch]$Uninstall)
$svcName="CyberWall"
$svcPath="$PSScriptRoot\CyberWall.Service.exe"
if ($Uninstall) {
  sc.exe stop $svcName | Out-Null
  sc.exe delete $svcName | Out-Null
  Write-Host "Desinstalado"
  exit
}
if (-not (Test-Path $svcPath)) { Write-Error "No se encontró $svcPath"; exit 1 }
sc.exe create $svcName binPath= "`"$svcPath`"" start= auto DisplayName= "CyberWall Firewall" | Out-Null
sc.exe description $svcName "WFP firewall por programa - whitelist por defecto" | Out-Null
sc.exe start $svcName | Out-Null
Write-Host "Instalado y iniciado como servicio SYSTEM. Lanza CyberWall.UI.exe para la interfaz."
