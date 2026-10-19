param([switch]$Uninstall)
$svcName="CyberWall"
$svcPath="$PSScriptRoot\CyberWall.Service.exe"
if ($Uninstall) {
  sc.exe stop $svcName | Out-Null
  sc.exe delete $svcName | Out-Null
  Write-Host "Removed"
  exit
}
if (-not (Test-Path $svcPath)) { Write-Error "Not found: $svcPath"; exit 1 }
sc.exe create $svcName binPath= "`"$svcPath`"" start= auto DisplayName= "CyberWall Firewall" | Out-Null
sc.exe description $svcName "CyberWall per-application firewall (default-deny). The service takes over while the desktop application is closed." | Out-Null
sc.exe start $svcName | Out-Null
Write-Host "Installed and started as a SYSTEM service. Launch CyberWall.exe for the interface."
Write-Host "While the desktop application is open it owns the firewall engine; the service resumes protection when it closes."
